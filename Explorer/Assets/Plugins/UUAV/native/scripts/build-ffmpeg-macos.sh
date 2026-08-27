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

# configure resolves libxml2 (below) exclusively through pkg-config
if ! command -v pkg-config > /dev/null; then
    echo "error: pkg-config not found; install it with 'brew install pkg-config'" >&2
    exit 1
fi

# consistent LC_BUILD_VERSION across both slices
export MACOSX_DEPLOYMENT_TARGET=11.0

if [[ ! -d "$SRC_DIR" ]]; then
    git clone --depth 1 --branch "$FFMPEG_TAG" https://github.com/FFmpeg/FFmpeg.git "$SRC_DIR"
fi

# out-of-tree builds refuse to configure over a stale in-tree configuration
[[ -f "$SRC_DIR/config.h" ]] && make -C "$SRC_DIR" distclean

# wipe previous outputs so a thin prefix from an older run can't leak through
rm -rf "$PREFIX" "$BUILD_DIR"

# securetransport is the schannel analog: https support with zero extra
# dylibs. If a future FFmpeg drops it, switch to --enable-openssl (adds a
# homebrew openssl runtime dependency). Run from $BUILD_DIR: even --help
# drops an ffbuild/ dir into the cwd, which Unity would import from native/.
mkdir -p "$BUILD_DIR"
if ! (cd "$BUILD_DIR" && "$SRC_DIR/configure" --help | grep -q securetransport); then
    echo "error: this FFmpeg has no --enable-securetransport; use --enable-openssl instead" >&2
    exit 1
fi

# The DASH demuxer hard-requires libxml2 (dash_demuxer_deps in configure);
# without it this build silently ships unable to open .mpd manifests while
# the BtbN Windows build (--enable-libxml2) can - a gap that only surfaces
# as mac-only playback failures. macOS bundles libxml2 in the dyld shared
# cache on every install and the SDK carries the headers and tbd, but no
# pkg-config file; synthesize one pointing at the SDK so configure's
# require_pkg_config resolves with zero Homebrew runtime dependency.
SDKROOT="$(xcrun --show-sdk-path)"
XML2_VERSION="$(awk -F'"' '/define LIBXML_DOTTED_VERSION/ {print $2}' "$SDKROOT/usr/include/libxml2/libxml/xmlversion.h")"
if [[ -z "$XML2_VERSION" ]]; then
    echo "error: could not read LIBXML_DOTTED_VERSION from the SDK's xmlversion.h" >&2
    exit 1
fi
mkdir -p "$BUILD_DIR/pkgconfig"
# Both include roots on purpose: the demuxer includes <libxml/parser.h>
# (needs usr/include/libxml2) while configure's probe compiles
# <libxml2/libxml/xmlversion.h> (needs usr/include, which the cross slice
# may not have on its default search path).
cat > "$BUILD_DIR/pkgconfig/libxml-2.0.pc" <<EOF
Name: libXML
Description: libxml2 from the macOS SDK
Version: $XML2_VERSION
Libs: -lxml2
Cflags: -I$SDKROOT/usr/include/libxml2 -I$SDKROOT/usr/include
EOF
export PKG_CONFIG_PATH="$BUILD_DIR/pkgconfig${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"

# LGPL is the default (no --enable-gpl). Do NOT disable avdevice/avfilter:
# ffmpeg-sys-next's default features link all seven libraries.
#
# --disable-xlib / --disable-libxcb: both are X11 screen-grab indev/outdevs
# (xv/x11grab) we never use. configure autodetects them from a build host's
# Homebrew libx11/libxcb and, worse, leaks -lX11 into avutil's extralibs so
# every dylib ends up hard-linked to /opt/homebrew/opt/libx11/.../libX11.6.dylib
# - a path absent on clean machines, so the helper fails dyld load there.
# Disabling both keeps the shipped dylibs free of any Homebrew absolute path.
build_arch() {
    local arch="$1" prefix="$2"
    shift 2

    mkdir -p "$BUILD_DIR/$arch"
    cd "$BUILD_DIR/$arch"

    "$SRC_DIR/configure" \
        --prefix="$prefix" \
        --install-name-dir='@rpath' \
        --arch="$arch" \
        --enable-shared \
        --disable-static \
        --disable-programs \
        --disable-doc \
        --disable-xlib \
        --disable-libxcb \
        --enable-videotoolbox \
        --enable-securetransport \
        --enable-libxml2 \
        "$@"

    make -j"$(sysctl -n hw.ncpu)"
    make install
}

# arm64 installs into the final prefix and stays the base (symlink chains,
# headers, .pc files); x86_64 goes to a staging prefix and only its dylib
# slices are merged in. --cc covers linking too; nasm is auto-detected.
build_arch arm64 "$PREFIX"
build_arch x86_64 "$PREFIX_X86" \
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

# regression gate: no dylib may depend on a build-host absolute path
# (Homebrew/Cellar or /usr/local) - those resolve only on this machine and
# fail dyld load on a clean one. Only @rpath refs and system frameworks/libs
# under /System, /usr/lib are portable.
leaked=0
for lib in "$PREFIX"/lib/lib*.dylib; do
    [[ -L "$lib" ]] && continue
    if bad="$(otool -L "$lib" | tail -n +2 | grep -E '/opt/homebrew|/usr/local|/opt/local')"; then
        echo "error: $(basename "$lib") links a non-portable absolute path:" >&2
        echo "$bad" >&2
        leaked=1
    fi
done
if [[ "$leaked" -ne 0 ]]; then
    echo "error: FFmpeg build leaked build-host library paths; see above" >&2
    exit 1
fi
echo "All dylibs are free of build-host absolute paths."
