#!/bin/bash
# Builds FFmpeg 8.1 (LGPL, shared) from source for Windows x64 into
# native/.third_party/ffmpeg - the Windows analog of
# scripts/build-ffmpeg-macos.sh, replacing the prebuilt BtbN drop that used
# to be unpacked there by hand.
#
# clang-cl with -guard:cf is the reason this exists. Control Flow Guard is a
# whole-process property: an indirect call is only checked if the module
# making it was instrumented, so every DLL that gets loaded has to be built
# with it. mingw cannot emit CFG at all, which is what moved the Windows
# target to x86_64-pc-windows-msvc; this script covers the FFmpeg half and
# -C control-flow-guard=yes in .cargo/config.toml covers uuav.dll.
#
# FFmpeg's configure needs a POSIX shell, so this runs under MSYS2 with a
# Visual Studio x64 environment inherited from the parent process.
# scripts/build-ffmpeg-windows.cmd sets both up and is the entry point.

set -euo pipefail

# FFmpeg release in lockstep with ffmpeg-sys-next in Cargo.toml
FFMPEG_TAG="n8.1"

NATIVE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="$NATIVE_DIR/.ffmpeg-src"
PREFIX="$NATIVE_DIR/.third_party/ffmpeg"

# libxml2 pinned release. macOS gets libxml2 from the system SDK; Windows has
# none, so the dash demuxer's XML dependency is built from source here as a
# STATIC library and baked into avformat-62.dll by FFmpeg's linker. That keeps
# the shipped set at 8 DLLs (no libxml2.dll): the XML object code rides inside
# avformat, still under the same clang-cl / static-CRT / -guard:cf recipe.
LIBXML2_VER="2.13.8"
LIBXML2_SHA256="277294cb33119ab71b2bc81f2f445e9bc9435b893ad15bb2cd2b0e859a0ee84a"
LIBXML2_PREFIX="$NATIVE_DIR/.third_party/libxml2"

case "$(uname -s)" in
MSYS* | MINGW*) ;;
*)
    echo "error: this script must run under MSYS2; use scripts/build-ffmpeg-windows.cmd" >&2
    exit 1
    ;;
esac

for tool in cl.exe clang-cl.exe nasm make cmake pkg-config; do
    if ! command -v "$tool" >/dev/null; then
        echo "error: $tool is not on PATH; launch through scripts/build-ffmpeg-windows.cmd" >&2
        echo "       (pkg-config/cmake are needed for libxml2: pacman -S pkgconf, install CMake)" >&2
        exit 1
    fi
done

# MSYS2 ships its own /usr/bin/link, which shadows the MSVC linker of the
# same name and makes every configure link test fail. Putting the directory
# that holds cl.exe first fixes both link.exe and lib.exe in one move.
MSVC_BIN="$(dirname "$(command -v cl.exe)")"
export PATH="$MSVC_BIN:$PATH"

# Build the STATIC libxml2 the dash demuxer needs, into $LIBXML2_PREFIX.
# clang-cl with a static CRT (/MT, cmake's MSVC default is /MD - overridden
# below) and -guard:cf keeps it in lockstep with the FFmpeg DLLs: no VC++
# redistributable, CFG enforced. Minimal surface: only the XML parser/tree, no
# iconv/zlib/lzma/icu/http/python/programs/tests. BUILD_SHARED_LIBS=OFF makes
# a static archive (no libxml2.dll); FFmpeg links its object code straight into
# avformat-62.dll. Skipped if already installed.
build_libxml2() {
    if [[ -f "$LIBXML2_PREFIX/lib/xml2.lib" ]]; then
        echo "libxml2 already built at $LIBXML2_PREFIX"
        return
    fi
    local tp="$NATIVE_DIR/.third_party"
    local tarball="$tp/libxml2-$LIBXML2_VER.tar.xz"
    local src="$tp/libxml2-src" bld="$tp/libxml2-build"
    mkdir -p "$tp"
    if [[ ! -f "$tarball" ]]; then
        curl -fsSL -o "$tarball" \
            "https://download.gnome.org/sources/libxml2/${LIBXML2_VER%.*}/libxml2-$LIBXML2_VER.tar.xz"
    fi
    echo "$LIBXML2_SHA256 *$tarball" | sha256sum -c -
    rm -rf "$src" "$bld" "$LIBXML2_PREFIX"
    tar xJf "$tarball" -C "$tp"
    mv "$tp/libxml2-$LIBXML2_VER" "$src"
    # cmake is the native Windows build; feed it Windows-style (-m) paths even
    # though we drive it from an MSYS2 shell.
    local pfx_w src_w bld_w
    pfx_w="$(cygpath -m "$LIBXML2_PREFIX")"
    src_w="$(cygpath -m "$src")"
    bld_w="$(cygpath -m "$bld")"
    cmake -S "$src_w" -B "$bld_w" -G "NMake Makefiles" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_C_COMPILER=clang-cl \
        -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded \
        -DCMAKE_C_FLAGS="-guard:cf" \
        -DCMAKE_EXE_LINKER_FLAGS="/guard:cf" \
        -DCMAKE_INSTALL_PREFIX="$pfx_w" \
        -DBUILD_SHARED_LIBS=OFF \
        -DLIBXML2_WITH_ICONV=OFF \
        -DLIBXML2_WITH_ZLIB=OFF \
        -DLIBXML2_WITH_LZMA=OFF \
        -DLIBXML2_WITH_ICU=OFF \
        -DLIBXML2_WITH_PYTHON=OFF \
        -DLIBXML2_WITH_HTTP=OFF \
        -DLIBXML2_WITH_MODULES=OFF \
        -DLIBXML2_WITH_PROGRAMS=OFF \
        -DLIBXML2_WITH_TESTS=OFF
    cmake --build "$bld_w"
    cmake --install "$bld_w"
    # A static Release build names the archive libxml2s.lib (PREFIX=lib,
    # OUTPUT_NAME=xml2, RELEASE_POSTFIX=s). FFmpeg's msvc flag filter maps
    # pkg-config's -lxml2 to xml2.lib, so provide that spelling.
    cp -f "$LIBXML2_PREFIX/lib/libxml2s.lib" "$LIBXML2_PREFIX/lib/xml2.lib"
}

build_libxml2

# Point pkg-config (FFmpeg requires it for libxml2) at the private prefix.
export PKG_CONFIG_PATH="$LIBXML2_PREFIX/lib/pkgconfig${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"
LIBXML2_PREFIX_W="$(cygpath -m "$LIBXML2_PREFIX")"

if [[ ! -d "$SRC_DIR" ]]; then
    # the Windows git may default to autocrlf; configure and the makefiles
    # are read by bash and must stay LF
    git -c core.autocrlf=false -c core.eol=lf \
        clone --depth 1 --branch "$FFMPEG_TAG" https://github.com/FFmpeg/FFmpeg.git "$SRC_DIR"
fi

cd "$SRC_DIR"

# The child does no networking: our uuav_protocol.c registers a custom
# URLProtocol under the http/https NAMES (replacing the real network ones), whose
# read/seek/open route to the trusted parent over shared memory. Compiled into
# libavformat and enabled as the uuavhttp/uuavhttps protocols. FFmpeg's configure
# derives its protocol list from the `extern const URLProtocol ff_*_protocol;`
# lines in protocols.c, so adding ours there registers the symbols and makes
# --enable-protocol=uuavhttp,uuavhttps valid. Idempotent for a cached $SRC_DIR.
# uuav_protocol.c dllexport's av_uuav_fetch_register so the adapter binds it out of
# avformat-62.dll — FFmpeg's Windows build otherwise exports only its own API.
register_uuav_protocol() {
    cp -f "$NATIVE_DIR/ffmpeg/uuav_protocol.c" "$SRC_DIR/libavformat/uuav_protocol.c"
    if ! grep -q ff_uuavhttp_protocol "$SRC_DIR/libavformat/protocols.c"; then
        sed -i '/extern const URLProtocol ff_data_protocol;/a\
extern const URLProtocol ff_uuavhttp_protocol;\
extern const URLProtocol ff_uuavhttps_protocol;' "$SRC_DIR/libavformat/protocols.c"
    fi
    if ! grep -q 'CONFIG_UUAVHTTP_PROTOCOL' "$SRC_DIR/libavformat/Makefile"; then
        printf 'OBJS-$(CONFIG_UUAVHTTP_PROTOCOL) += uuav_protocol.o\n' \
            >> "$SRC_DIR/libavformat/Makefile"
    fi
}
register_uuav_protocol

# No TLS backend: the child no longer terminates TLS. Its only protocols are the
# uuav RPC (under the http/https names), crypto (local AES-128-CBC over the RPC),
# data, and file (registered for the hls scheme-name gate, its I/O unused). The
# parent's Unity HTTP stack terminates TLS with the OS trust store, so schannel
# is gone from the child's attack surface entirely. Keeping http/https OFF
# compiles out hls.c's CONFIG_HTTP_PROTOCOL keepalive path, so it does fresh
# opens through our protocol — the no-child-caching behaviour live reload needs.

# LGPL is the default (no --enable-gpl). Do NOT disable avdevice/avfilter:
# ffmpeg-sys-next's default features link all seven libraries.
#
# --disable-everything plus an explicit enable list is the primary sandbox:
# a demuxer or decoder that is not built cannot be reached by any url, no
# matter what the runtime whitelists say. The lists below are the build-side
# half of FORMAT_WHITELIST / CODEC_WHITELIST in native/src/ffutil.rs - keep
# the two in lockstep, and prefer removing from both over adding to either.
# They are identical to the macOS script's; only the hwaccel and tls backend
# differ (d3d11va/schannel here, videotoolbox/securetransport there).
#
# Two Windows caveats:
# - the legacy `_d3d11va` hwaccels are enabled alongside the `_d3d11va2` ones
#   the decoder actually uses, because libavcodec/Makefile only compiles
#   dxva2_h264.o and friends for the legacy name; asking for `_d3d11va2`
#   alone configures fine and then fails to link avcodec.
# - dxva2, mediafoundation and d3d12va are hwaccel autodetect entries: naming
#   only `--enable-d3d11va` leaves the other three switched on, and they are
#   what makes the DLLs import USER32 and ole32 (`GetDesktopWindow` from
#   libavutil/hwcontext_dxva2.c, ole32 from libavcodec/mf_utils.c). A process
#   running under MITIGATION_WIN32K_SYSTEM_CALL_DISABLE_ALWAYS_ON cannot
#   initialise user32.dll and dies at 0xC0000142 before its first instruction,
#   so the out-of-process decoder's software mode cannot take the win32k
#   lockdown while these are linked in. None of the three is used by either
#   decode mode, so they are disabled explicitly.
#
# --enable-libxml2 pulls in the dash demuxer (built above, static). The
# libxml2 flags add its headers/import lib on top of what pkg-config reports,
# so the check works regardless of pkg-config's MSYS-vs-Windows path spelling.
# -DLIBXML_STATIC stops libxml2's headers from tagging its symbols
# __declspec(dllimport) - the static-link gotcha; without it avformat still
# tries to import them from a libxml2.dll that no longer exists. libxml2's
# only external deps in this minimal build are bcrypt (RNG for its hash seed)
# and ws2_32 - added via --extra-libs so both configure's link test and the
# final avformat link resolve them.
#
# The CRT is linked statically (clang-cl's default, not overridden): the
# resulting DLLs import nothing but OS libraries, so the plugin folder needs
# no VC++ redistributable and no sidecar runtime DLLs at all. Every FFmpeg
# allocation goes through avutil's allocator, so nothing frees across a
# per-DLL heap boundary.
./configure \
    --prefix="$PREFIX" \
    --toolchain=msvc \
    --cc=clang-cl \
    --enable-shared \
    --disable-static \
    --disable-programs \
    --disable-doc \
    --enable-d3d11va \
    --disable-dxva2 \
    --disable-mediafoundation \
    --disable-d3d12va \
    --enable-libxml2 \
    --disable-everything \
    --enable-protocol=uuavhttp,uuavhttps,crypto,data,file \
    --enable-demuxer=mov,matroska,hls,dash,mpegts,mp3,wav,ogg,flac,aac \
    --enable-decoder=h264,hevc,vp9,av1,aac,mp3,mp3float,opus,vorbis,flac,pcm_s16le,pcm_s16be,pcm_f32le \
    --enable-parser=h264,hevc,vp9,av1,aac,mpegaudio,flac,opus,vorbis \
    --enable-bsf=h264_mp4toannexb,hevc_mp4toannexb,extract_extradata,aac_adtstoasc \
    --enable-hwaccel=h264_d3d11va,h264_d3d11va2,hevc_d3d11va,hevc_d3d11va2,vp9_d3d11va,vp9_d3d11va2,av1_d3d11va,av1_d3d11va2 \
    --extra-cflags="-guard:cf -DLIBXML_STATIC -I$LIBXML2_PREFIX_W/include -I$LIBXML2_PREFIX_W/include/libxml2" \
    --extra-ldflags="-guard:cf -libpath:$LIBXML2_PREFIX_W/lib" \
    --extra-libs="-lbcrypt -lws2_32"

# dash needs libxml2; fail loudly if configure silently dropped it (e.g. a
# future pkg-config regression) rather than shipping a build that 404s on dash.
if ! grep -qx "CONFIG_DASH_DEMUXER=yes" ffbuild/config.mak; then
    echo "error: dash demuxer was not enabled (libxml2 not picked up); see ffbuild/config.log" >&2
    exit 1
fi

# The uuav protocol is the child's only path to bytes; a configure that silently
# dropped it (e.g. a botched protocols.c patch) would ship a build that opens
# nothing at all.
if ! grep -qx "CONFIG_UUAVHTTP_PROTOCOL=yes" ffbuild/config.mak; then
    echo "error: the uuav fetch protocol was not enabled; see register_uuav_protocol and ffbuild/config.log" >&2
    exit 1
fi

make -j"$(nproc)"
make install

# FFmpeg's msvc install treats the import libraries as companions of the DLL
# and drops them in bin/. ffmpeg-sys-next only adds $FFMPEG_DIR/lib to the
# link search path, which is also where the BtbN layout kept them.
mv -f "$PREFIX"/bin/*.lib "$PREFIX/lib/"

echo
echo "Installed into: $PREFIX"
echo "Runtime DLLs (deploy these next to uuav.dll):"
ls -1 "$PREFIX"/bin/*.dll
