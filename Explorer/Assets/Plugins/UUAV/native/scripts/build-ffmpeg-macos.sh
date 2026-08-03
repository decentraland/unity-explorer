#!/bin/bash
# Builds FFmpeg 8.1 (LGPL, shared) from source as universal (arm64 + x86_64)
# fat dylibs into native/.third_party/ffmpeg - the macOS analog of the BtbN
# win64 build the Windows side consumes from the same path.
#
# The build host must be Apple Silicon: the arm64 slice builds natively and
# the x86_64 slice is cross-compiled (clang -arch x86_64), then both are
# merged in place with lipo.
#
# --install-name-dir='@rpath' stamps every dylib's install name (and the
# inter-library references) as @rpath/lib*.dylib, so libuuav.dylib resolves
# the whole set from its own folder via its LC_RPATH @loader_path entry -
# no install_name_tool post-processing, works in-editor and in built players.

set -euo pipefail

# FFmpeg release in lockstep with ffmpeg-sys-next in Cargo.toml
FFMPEG_TAG="n8.1"

NATIVE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="$NATIVE_DIR/.ffmpeg-src"
BUILD_DIR="$NATIVE_DIR/.ffmpeg-build"
PREFIX="$NATIVE_DIR/.third_party/ffmpeg"
PREFIX_X86="$BUILD_DIR/prefix-x86_64"

if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
    echo "error: this script must run on Apple Silicon macOS (the x86_64 slice is cross-compiled from arm64)" >&2
    exit 1
fi

# nasm assembles the x86_64 SIMD kernels (the swscale/swresample/vp8/vp9
# software paths depend on them) and cross-assembles macho64 on arm64 hosts
if ! command -v nasm > /dev/null; then
    echo "error: nasm not found; install it with 'brew install nasm'" >&2
    exit 1
fi

# consistent LC_BUILD_VERSION across both slices
export MACOSX_DEPLOYMENT_TARGET=11.0

if [[ ! -d "$SRC_DIR" ]]; then
    git clone --depth 1 --branch "$FFMPEG_TAG" https://github.com/FFmpeg/FFmpeg.git "$SRC_DIR"
fi

# The child does no networking: our uuav_protocol.c registers a custom
# URLProtocol under the http/https NAMES (replacing the real network ones), whose
# read/seek/open route to the trusted parent over shared memory. Compiled into
# libavformat and enabled as the uuavhttp/uuavhttps protocols. Because FFmpeg's
# configure derives its protocol list from the `extern const URLProtocol
# ff_*_protocol;` declarations in protocols.c, adding ours there both registers
# the symbols and makes --enable-protocol=uuavhttp,uuavhttps valid. Idempotent so
# a cached $SRC_DIR is not double-patched. Keeping http/https OFF compiles out
# hls.c's CONFIG_HTTP_PROTOCOL keepalive path, so it does fresh opens through our
# protocol — exactly the no-child-caching behaviour the live-reload case needs.
register_uuav_protocol() {
    cp -f "$NATIVE_DIR/ffmpeg/uuav_protocol.c" "$SRC_DIR/libavformat/uuav_protocol.c"
    if ! grep -q ff_uuavhttp_protocol "$SRC_DIR/libavformat/protocols.c"; then
        sed -i.bak '/extern const URLProtocol ff_data_protocol;/a\
extern const URLProtocol ff_uuavhttp_protocol;\
extern const URLProtocol ff_uuavhttps_protocol;' "$SRC_DIR/libavformat/protocols.c"
        rm -f "$SRC_DIR/libavformat/protocols.c.bak"
    fi
    if ! grep -q 'CONFIG_UUAVHTTP_PROTOCOL' "$SRC_DIR/libavformat/Makefile"; then
        # One object provides both symbols; reference it once (both protocols are
        # always enabled together) so the linker never sees a duplicate.
        printf 'OBJS-$(CONFIG_UUAVHTTP_PROTOCOL) += uuav_protocol.o\n' \
            >> "$SRC_DIR/libavformat/Makefile"
    fi
}
register_uuav_protocol

# out-of-tree builds refuse to configure over a stale in-tree configuration
[[ -f "$SRC_DIR/config.h" ]] && make -C "$SRC_DIR" distclean

# wipe previous outputs so a thin prefix from an older run can't leak through
rm -rf "$PREFIX" "$BUILD_DIR"

# No TLS backend: the child no longer terminates TLS. Its only protocols are the
# uuav RPC (under the http/https names), crypto (local AES-128-CBC over the RPC),
# data, and file (registered for the hls scheme-name gate, its I/O unused). The
# parent's Unity HTTP stack terminates TLS with the OS trust store, so
# securetransport/schannel are gone from the child's attack surface entirely.
mkdir -p "$BUILD_DIR"

# LGPL is the default (no --enable-gpl). Do NOT disable avdevice/avfilter:
# ffmpeg-sys-next's default features link all seven libraries.
#
# --disable-everything plus an explicit enable list is the primary sandbox: an
# unbuilt demuxer cannot be reached by any url whatever the runtime whitelist
# says. Keep in lockstep with FORMAT_WHITELIST / CODEC_WHITELIST in ffutil.rs,
# and pass to every arch - one unsandboxed slice reintroduces the whole surface.
SANDBOX_FLAGS=(
    --disable-everything
    --enable-protocol=uuavhttp,uuavhttps,crypto,data,file
    --enable-demuxer=mov,matroska,hls,dash,mpegts,mp3,wav,ogg,flac,aac
    --enable-decoder=h264,hevc,vp9,av1,aac,mp3,mp3float,opus,vorbis,flac,pcm_s16le,pcm_s16be,pcm_f32le
    --enable-parser=h264,hevc,vp9,av1,aac,mpegaudio,flac,opus,vorbis
    --enable-bsf=h264_mp4toannexb,hevc_mp4toannexb,extract_extradata,aac_adtstoasc
    --enable-hwaccel=h264_videotoolbox,hevc_videotoolbox,vp9_videotoolbox,av1_videotoolbox
)

build_arch() {
    local arch="$1" prefix="$2"
    shift 2

    mkdir -p "$BUILD_DIR/$arch"
    cd "$BUILD_DIR/$arch"

    "$SRC_DIR/configure" \
        --prefix="$prefix" \
        --install-name-dir='@rpath' \
        `# Since Xcode 15 the linker folds each object's mtime into the debug` \
        `# map and derives LC_UUID from content that includes it. $BUILD_DIR is` \
        `# wiped above, so every run gets new mtimes; strip -x then drops the` \
        `# debug map but keeps the UUID, leaving two builds of identical` \
        `# sources differing in the UUID and the signature page over it.` \
        `# -reproducible makes the linker ignore those mtimes, which is what` \
        `# makes the shipped bytes a function of the sources. Do not use` \
        `# -no_uuid instead: dyld refuses to load an image without LC_UUID.` \
        --extra-ldflags=-Wl,-reproducible \
        --arch="$arch" \
        --enable-shared \
        --disable-static \
        --disable-programs \
        --disable-doc \
        --enable-videotoolbox \
        `# required by the dash demuxer: an XML parser reachable from` \
        `# attacker-controlled manifests, the cost of DASH support` \
        --enable-libxml2 \
        "$@"

    # The uuav protocol is the child's only path to bytes; a configure that
    # silently dropped it (e.g. a botched protocols.c patch) would ship a build
    # that opens nothing. Fail loud, as the dash assertion does on Windows.
    if ! grep -qx "CONFIG_UUAVHTTP_PROTOCOL=yes" ffbuild/config.mak; then
        echo "error: the uuav fetch protocol was not enabled; see register_uuav_protocol and ffbuild/config.log" >&2
        exit 1
    fi

    make -j"$(sysctl -n hw.ncpu)"
    make install
}

# Recent macOS ships no on-disk libxml2 dylib and no libxml-2.0.pc, but the SDK
# carries the headers and a .tbd stub the linker resolves; --enable-libxml2
# requires pkg-config, so synthesize the .pc. Without it the dash demuxer
# silently drops out. Cflags carries BOTH the base include dir and .../libxml2:
# configure probes `<libxml2/libxml/xmlversion.h>` while the dash sources
# include `<libxml/tree.h>`, and the nix clang that cross-compiles the x86_64
# slice does not add the SDK base implicitly the way Apple clang does.
SDKROOT="${SDKROOT:-$(xcrun --show-sdk-path)}"
LIBXML2_INC="$SDKROOT/usr/include/libxml2"
if [[ ! -f "$LIBXML2_INC/libxml/xmlversion.h" ]]; then
    echo "error: SDK libxml2 headers not found at $LIBXML2_INC (needed for --enable-libxml2/DASH)" >&2
    exit 1
fi
LIBXML2_VER="$(sed -n 's/^#define LIBXML_DOTTED_VERSION "\(.*\)"/\1/p' "$LIBXML2_INC/libxml/xmlversion.h")"
PKGCFG_DIR="$BUILD_DIR/pkgconfig"
mkdir -p "$PKGCFG_DIR"
cat > "$PKGCFG_DIR/libxml-2.0.pc" << PC
prefix=$SDKROOT/usr
includedir=\${prefix}/include
libdir=\${prefix}/lib
Name: libXML
Version: ${LIBXML2_VER:-2.9.0}
Description: libxml2 from the macOS SDK (headers + .tbd stub; runtime via the dyld shared cache)
Cflags: -I\${includedir} -I\${includedir}/libxml2
Libs: -L\${libdir} -lxml2
PC
export PKG_CONFIG_PATH="$PKGCFG_DIR${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"

# arm64 installs into the final prefix and stays the base (symlink chains,
# headers, .pc files); x86_64 goes to a staging prefix and only its dylib
# slices are merged in. --cc covers linking too; nasm is auto-detected.
build_arch arm64 "$PREFIX" "${SANDBOX_FLAGS[@]}"
build_arch x86_64 "$PREFIX_X86" "${SANDBOX_FLAGS[@]}" \
    --enable-cross-compile \
    --target-os=darwin \
    --cc="clang -arch x86_64"

# everything downstream compiles against the single arm64-installed include
# dir; assert the generated headers really are arch-independent so an FFmpeg
# upgrade can't silently break that assumption
diff -r "$PREFIX/include" "$PREFIX_X86/include"

# fatten only real files - lipo dereferences symlinks and would break the
# libX.dylib -> libX.62.dylib -> libX.62.x.100.dylib chains build.sh relies
# on. lipo with output == input is unsafe, hence the temp file + mv.
for lib in "$PREFIX"/lib/lib*.dylib; do
    [[ -L "$lib" ]] && continue
    lipo -create "$lib" "$PREFIX_X86/lib/$(basename "$lib")" -output "$lib.fat"
    mv "$lib.fat" "$lib"
done

echo
echo "Installed into: $PREFIX"
echo "Install names (all must be @rpath/...) and slices (all must be arm64 x86_64):"
for lib in "$PREFIX"/lib/lib*.dylib; do
    [[ -L "$lib" ]] && continue
    printf '%s   [%s]\n' "$(otool -D "$lib" | tail -1)" "$(lipo -archs "$lib")"
done
