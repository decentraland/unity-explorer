/*
 * uuav_protocol.c -- the child's FFmpeg protocol layer for the parent-side
 * fetch redesign.
 *
 * Compiled INTO the pinned libavformat by scripts/build-ffmpeg-{macos,windows}.sh
 * and registered under the `http` and `https` NAMES, replacing the real network
 * protocols. Because URLs keep their `https://` (and `http://`) schemes, the
 * frozen core opens them unchanged (`native/src` stays byte-identical) and the
 * HLS demuxer's hard-coded {file,http,https,data} scheme gate passes; every
 * sub-resource open -- playlists, segments, AES keys, init sections, live
 * reloads -- resolves here. The child does no networking: read/seek/open route
 * to the trusted parent (uuav-adapter) over shared memory, which hands them to
 * Unity's managed HTTP stack.
 *
 * This is OUR code following FFmpeg's URLProtocol conventions, not a
 * modification of FFmpeg's logic and NOT derived from any proprietary source.
 *
 * The interception is: the adapter installs three RPC stubs through
 * av_uuav_fetch_register at startup; url_open/read/close call them. The blocking
 * loop lives here, not in the stubs, so it can check h->interrupt_callback each
 * turn -- a Close or a dead host aborts a parked read promptly, and FFmpeg never
 * sees AVERROR(EAGAIN), which would abort avformat_open_input on the first read.
 */

#include <errno.h>
#include <stdint.h>
#include <stdio.h>

#include "libavformat/avformat.h"
#include "libavformat/url.h"
#include "libavutil/error.h"
#include "libavutil/time.h"

/* Export av_uuav_fetch_register so the adapter executable can bind it out of the
 * dylib/DLL. FFmpeg's Windows build only exports its documented API through the
 * generated .def, so the attribute is load-bearing there; harmless elsewhere.
 *
 * The av prefix is not cosmetic: every library is linked
 * -exported_symbols_list lib<name>.ver, which holds the single pattern `_av*`.
 * Under any other name the symbol is not merely hidden but dead-stripped out of
 * libavformat entirely, and the adapter fails to link. */
#if defined(_WIN32)
#define UUAV_EXPORT __declspec(dllexport)
#elif defined(__GNUC__)
#define UUAV_EXPORT __attribute__((visibility("default")))
#else
#define UUAV_EXPORT
#endif

/* Status codes returned by the poll stub. Mirror the adapter's fetch.rs. */
#define UUAV_FETCH_OK 0
#define UUAV_FETCH_WOULDWAIT 1
#define UUAV_FETCH_EOF 2
#define UUAV_FETCH_ERR 3

/* Fetch ops. Mirror uuav_ipc::protocol::fetch_op. */
#define UUAV_OP_OPEN 1
#define UUAV_OP_READ 2
#define UUAV_OP_CLOSE 3

/* Microseconds parked between polls while the parent services a request. Short
 * next to an HTTP round trip; the interrupt is checked once per turn regardless. */
#define UUAV_POLL_INTERVAL_US 200

typedef uint64_t (*uuav_fetch_begin_fn)(uint32_t op, uint32_t handle, uint64_t offset,
                                        uint32_t len, const char *url);
typedef int (*uuav_fetch_poll_fn)(uint64_t generation, uint8_t *buf, uint32_t cap,
                                  uint32_t *out_n, int64_t *out_size, uint32_t *out_handle);

static uuav_fetch_begin_fn uuav_begin;
static uuav_fetch_poll_fn uuav_poll_stub;

/* Installed by the adapter before it opens any media. The separate prototype is
 * for FFmpeg's -Werror -Wmissing-prototypes, which the Apple clang build enforces
 * and the clang-cl one does not. */
UUAV_EXPORT void av_uuav_fetch_register(uuav_fetch_begin_fn begin, uuav_fetch_poll_fn poll);

UUAV_EXPORT void av_uuav_fetch_register(uuav_fetch_begin_fn begin, uuav_fetch_poll_fn poll)
{
    uuav_begin = begin;
    uuav_poll_stub = poll;
}

typedef struct UuavContext {
    uint32_t handle;
    int64_t size;   /* total length from open; -1 unknown / not seekable */
    int64_t offset; /* current read position; a seek moves it lazily */
    int opened;
} UuavContext;

/* One request through the RPC, blocking (interrupt-checked) until the parent
 * answers. Never returns AVERROR(EAGAIN): "not ready yet" loops here. */
static int uuav_request(URLContext *h, uint32_t op, uint32_t handle, uint64_t offset,
                        uint32_t len, const char *url, uint8_t *buf, uint32_t cap,
                        uint32_t *out_n, int64_t *out_size, uint32_t *out_handle)
{
    if (!uuav_begin || !uuav_poll_stub)
        return AVERROR(EACCES);

    uint64_t generation = uuav_begin(op, handle, offset, len, url);
    if (generation == 0)
        return AVERROR(EIO);

    for (;;) {
        if (ff_check_interrupt(&h->interrupt_callback))
            return AVERROR_EXIT;
        int status = uuav_poll_stub(generation, buf, cap, out_n, out_size, out_handle);
        switch (status) {
        case UUAV_FETCH_WOULDWAIT:
            av_usleep(UUAV_POLL_INTERVAL_US);
            continue;
        case UUAV_FETCH_OK:
            return 0;
        case UUAV_FETCH_EOF:
            return AVERROR_EOF;
        default:
            return AVERROR(EIO);
        }
    }
}

static int uuav_open(URLContext *h, const char *url, int flags, AVDictionary **options)
{
    (void)options;
    (void)flags; /* the parent fetches read-only; AVIO open flags carry nothing it needs */
    UuavContext *c = h->priv_data;
    int64_t size = -1;
    uint32_t handle = 0;
    int ret =
        uuav_request(h, UUAV_OP_OPEN, 0, 0, 0, url, NULL, 0, NULL, &size, &handle);
    if (ret < 0)
        return ret;
    c->handle = handle;
    c->size = size;
    c->offset = 0;
    c->opened = 1;
    return 0;
}

static int uuav_read(URLContext *h, unsigned char *buf, int size)
{
    UuavContext *c = h->priv_data;
    if (!c->opened)
        return AVERROR(EIO);
    if (size <= 0)
        return 0;
    uint32_t n = 0;
    int ret = uuav_request(h, UUAV_OP_READ, c->handle, (uint64_t)c->offset, (uint32_t)size, NULL,
                           buf, (uint32_t)size, &n, NULL, NULL);
    if (ret < 0)
        return ret; /* AVERROR_EOF at end, a real error otherwise */
    c->offset += (int64_t)n;
    return (int)n;
}

static int64_t uuav_seek(URLContext *h, int64_t pos, int whence)
{
    UuavContext *c = h->priv_data;
    if (whence == AVSEEK_SIZE)
        return c->size; /* -1 answers "not seekable", as the core's realtime path expects */
    whence &= ~AVSEEK_FORCE;
    int64_t np;
    switch (whence) {
    case SEEK_SET:
        np = pos;
        break;
    case SEEK_CUR:
        np = c->offset + pos;
        break;
    case SEEK_END:
        if (c->size < 0)
            return AVERROR(EINVAL);
        np = c->size + pos;
        break;
    default:
        return AVERROR(EINVAL);
    }
    if (np < 0)
        return AVERROR(EINVAL);
    c->offset = np; /* lazy: the next read fetches this range, no seek RPC */
    return np;
}

static int uuav_close(URLContext *h)
{
    UuavContext *c = h->priv_data;
    if (c->opened)
        uuav_request(h, UUAV_OP_CLOSE, c->handle, 0, 0, NULL, NULL, 0, NULL, NULL, NULL);
    c->opened = 0;
    return 0;
}

const URLProtocol ff_uuavhttp_protocol = {
    .name = "http",
    .url_open2 = uuav_open,
    .url_read = uuav_read,
    .url_seek = uuav_seek,
    .url_close = uuav_close,
    .priv_data_size = sizeof(UuavContext),
    .default_whitelist = "http,https,crypto,data,file",
};

const URLProtocol ff_uuavhttps_protocol = {
    .name = "https",
    .url_open2 = uuav_open,
    .url_read = uuav_read,
    .url_seek = uuav_seek,
    .url_close = uuav_close,
    .priv_data_size = sizeof(UuavContext),
    .default_whitelist = "http,https,crypto,data,file",
};
