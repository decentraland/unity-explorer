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

# LGPL is the default (no --enable-gpl). Do NOT disable avdevice/avfilter:
# ffmpeg-sys-next's default features link all seven libraries.
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
        --enable-videotoolbox \
        --enable-securetransport \
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
