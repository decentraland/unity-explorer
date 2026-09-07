#!/usr/bin/env bash
# Builds FFmpeg 8.1 (LGPL, shared) from source for x86_64 Linux into
# native/.third_party/ffmpeg - the Linux analog of build-ffmpeg-macos.sh and
# of the BtbN win64 release the Windows side consumes from the same path.
#
# Third-party inputs are pinned: FFmpeg by release tag, OpenSSL / libxml2 by
# release-tarball sha256. OpenSSL (TLS - Linux has no OS TLS stack FFmpeg can
# use, unlike SecureTransport/Schannel) and libxml2 (DASH manifests, parity
# with the mac and BtbN builds) are linked statically so they ship zero extra
# .so files; Apache-2.0 and MIT are both LGPL-compatible.
#
# libva/libva-drm are host-provided, like libdrm/libvulkan/zlib/glibc: FFmpeg
# links them by bare soname (DT_NEEDED libva.so.2 / libva-drm.so.2) and the
# loader resolves them from the host's default library path, after which libva
# dlopens the host's VAAPI driver. They are deliberately NOT bundled: libva's
# driver ABI is one-directional, so a shipped-older libva probes only
# __vaDriverInit_1_N..1_0 and fails on any host whose VAAPI driver is newer
# (a 2.22 copy cannot call a host driver's __vaDriverInit_1_24). libva-dev is
# therefore a build prerequisite (the FFmpeg vaapi configure resolves it via
# pkg-config) and libva a runtime prerequisite on the player host.
#
# Every shipped .so gets DT_RUNPATH=$ORIGIN via patchelf after install
# (RUNPATH is not transitive, so the inter-library NEEDED entries must
# resolve from each library's own runpath, not just the helper's) - the
# FFmpeg set resolves from the plugin folder, and the host libva resolves
# from the default search path once $ORIGIN has no match.
#
# Dev box (NixOS): run inside
#   nix-shell -p nasm pkg-config libdrm libva patchelf perl
# CI (ubuntu): apt-get install nasm pkg-config libdrm-dev libva-dev patchelf
# Canonical shipped bytes are minted by CI; local builds are for testing
# (glibc/toolchain divergence keeps them out of the binary lock by design).
# To run local binaries against the host GPU stack:
#   LD_LIBRARY_PATH=/run/opengl-driver/lib plus a host libva.so.2 (the
#   nix-shell's libva); /run/opengl-driver/lib provides libdrm and the driver.

set -euo pipefail

# FFmpeg release in lockstep with ffmpeg-sys-next in Cargo.toml
FFMPEG_TAG="n8.1"

OPENSSL_VERSION="3.5.1"
OPENSSL_SHA256="529043b15cffa5f36077a4d0af83f3de399807181d607441d734196d889b641f"
LIBXML2_VERSION="2.13.8"
LIBXML2_SHA256="277294cb33119ab71b2bc81f2f445e9bc9435b893ad15bb2cd2b0e859a0ee84a"

NATIVE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
SRC_DIR="$NATIVE_DIR/.ffmpeg-src"
BUILD_DIR="$NATIVE_DIR/.ffmpeg-build"
PREFIX="$NATIVE_DIR/.third_party/ffmpeg"
DEPS_PREFIX="$BUILD_DIR/deps"

if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
    echo "error: this script must run on x86_64 Linux" >&2
    exit 1
fi

for tool in gcc make nasm pkg-config curl git patchelf perl nm; do
    if ! command -v "$tool" > /dev/null; then
        echo "error: $tool not found; see the dependency line in this script's header" >&2
        exit 1
    fi
done

# --enable-libdrm resolves libdrm through pkg-config
if ! pkg-config --exists libdrm; then
    echo "error: libdrm development files not found (pkg-config libdrm)" >&2
    exit 1
fi

# libva is host-provided, not bundled, so its dev files must be present at
# configure time for --enable-vaapi to resolve the host libva/libva-drm.
if ! pkg-config --exists libva libva-drm; then
    echo "error: libva development files not found (pkg-config libva libva-drm); install libva-dev (Debian/Ubuntu) or add 'libva' to the nix-shell" >&2
    exit 1
fi

# pinned for reproducible archive members and generated timestamps
export SOURCE_DATE_EPOCH=1704067200

if [[ ! -d "$SRC_DIR" ]]; then
    git clone --depth 1 --branch "$FFMPEG_TAG" https://github.com/FFmpeg/FFmpeg.git "$SRC_DIR"
fi

# out-of-tree builds refuse to configure over a stale in-tree configuration
[[ -f "$SRC_DIR/config.h" ]] && make -C "$SRC_DIR" distclean

# wipe previous outputs so a thin prefix from an older run can't leak through
rm -rf "$PREFIX" "$BUILD_DIR"
mkdir -p "$BUILD_DIR/downloads" "$DEPS_PREFIX"

fetch() {
    local url="$1" sha256="$2" out
    out="$BUILD_DIR/downloads/$(basename "$url")"
    curl -fsSL "$url" -o "$out"
    echo "$sha256  $out" | sha256sum -c - > /dev/null
    echo "$out"
}

# ---- OpenSSL (static; -fPIC because its objects land inside shared libs) ----
openssl_tar="$(fetch "https://github.com/openssl/openssl/releases/download/openssl-$OPENSSL_VERSION/openssl-$OPENSSL_VERSION.tar.gz" "$OPENSSL_SHA256")"
tar -xzf "$openssl_tar" -C "$BUILD_DIR"
(
    cd "$BUILD_DIR/openssl-$OPENSSL_VERSION"
    ./Configure linux-x86_64 no-shared no-apps no-docs no-tests \
        --prefix="$DEPS_PREFIX" --libdir=lib -fPIC > /dev/null
    make -j"$(nproc)" > /dev/null
    make install_sw > /dev/null
)

# ---- libxml2 (static, minimal: the DASH demuxer only parses UTF-8 XML) ----
libxml2_tar="$(fetch "https://download.gnome.org/sources/libxml2/${LIBXML2_VERSION%.*}/libxml2-$LIBXML2_VERSION.tar.xz" "$LIBXML2_SHA256")"
tar -xJf "$libxml2_tar" -C "$BUILD_DIR"
(
    cd "$BUILD_DIR/libxml2-$LIBXML2_VERSION"
    ./configure --prefix="$DEPS_PREFIX" --disable-shared --enable-static \
        --without-python --without-zlib --without-lzma CFLAGS=-fPIC > /dev/null
    make -j"$(nproc)" > /dev/null
    make install > /dev/null
)
# Both include roots on purpose (same lesson as the mac script): the DASH
# demuxer includes <libxml/parser.h> (needs include/libxml2) while FFmpeg's
# configure probe compiles <libxml2/libxml/xmlversion.h> (needs include).
sed -i 's|^Cflags: .*|& -I${includedir}|' "$DEPS_PREFIX/lib/pkgconfig/libxml-2.0.pc"

# ---- FFmpeg ----
# vaapi resolves the host libva/libva-drm through pkg-config (not bundled);
# see the header for why libva is host-provided rather than shipped.
# LGPL is the default (no --enable-gpl). Do NOT disable avdevice/avfilter/
# swscale: ffmpeg-sys-next's default features link all seven libraries.
#
# --disable-devices: every in/outdev is capture/playback machinery we never
# use; alsa/sndio/xlib/libxcb are additionally disabled by name so configure
# cannot autodetect a build-host library into the shipped set (the mac
# script's Homebrew-leak lesson). --disable-bzlib/--disable-lzma: their
# sonames differ across distros (libbz2.so.1 vs .1.0), which would make the
# shipped set host-dependent; zlib stays (libz.so.1 is universal). The
# hardware stacks other than VAAPI are pinned off so the configure line -
# recorded in the binary lock - does not drift with whatever headers a build
# host happens to have.
export PKG_CONFIG_PATH="$DEPS_PREFIX/lib/pkgconfig:$PREFIX/lib/pkgconfig${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"
# the nixpkgs pkg-config wrapper consults only the _FOR_TARGET variant of
# the search path; inert everywhere else
export PKG_CONFIG_PATH_FOR_TARGET="$DEPS_PREFIX/lib/pkgconfig:$PREFIX/lib/pkgconfig${PKG_CONFIG_PATH_FOR_TARGET:+:$PKG_CONFIG_PATH_FOR_TARGET}"
mkdir -p "$BUILD_DIR/ffmpeg"
(
    cd "$BUILD_DIR/ffmpeg"
    "$SRC_DIR/configure" \
        --prefix="$PREFIX" \
        --pkg-config-flags="--static" \
        --enable-shared \
        --disable-static \
        --disable-programs \
        --disable-doc \
        --disable-devices \
        --disable-alsa \
        --disable-sndio \
        --disable-xlib \
        --disable-libxcb \
        --disable-bzlib \
        --disable-lzma \
        --disable-vdpau \
        --disable-cuda-llvm \
        --disable-cuvid \
        --disable-nvdec \
        --disable-nvenc \
        --disable-opencl \
        --disable-vulkan \
        --disable-amf \
        --enable-vaapi \
        --enable-libdrm \
        --enable-openssl \
        --enable-libxml2

    make -j"$(nproc)"
    make install
)

# $ORIGIN runpath on every real .so - patchelf after install instead of
# ldflags because a literal $ORIGIN does not survive the configure->make
# quoting layers intact
while IFS= read -r lib; do
    patchelf --set-rpath '$ORIGIN' "$lib"
done < <(find "$PREFIX/lib" -name 'lib*.so*' -type f)

echo
echo "Installed into: $PREFIX"
echo "Sonames and runpaths (all runpaths must be \$ORIGIN):"
while IFS= read -r lib; do
    soname="$(patchelf --print-soname "$lib" 2>/dev/null || basename "$lib")"
    runpath="$(patchelf --print-rpath "$lib")"
    printf '%s   [runpath %s]\n' "$soname" "$runpath"
done < <(find "$PREFIX/lib" -name 'lib*.so*' -type f | sort)

# regression gate: shipped libraries may depend only on each other and on
# host-universal libraries. Anything else (a build-host library configure
# autodetected) resolves only on this machine and fails to load on a clean
# one.
allowed='^(libavcodec|libavdevice|libavfilter|libavformat|libavutil|libswresample|libswscale|libva|libva-drm|libc|libm|libpthread|libdl|librt|libgcc_s|libz|libdrm|ld-linux-x86-64)\.so'
leaked=0
while IFS= read -r lib; do
    while IFS= read -r needed; do
        if ! grep -qE "$allowed" <<< "$needed"; then
            echo "error: $(basename "$lib") links non-portable library: $needed" >&2
            leaked=1
        fi
    done < <(patchelf --print-needed "$lib")
done < <(find "$PREFIX/lib" -name 'lib*.so*' -type f)
if [[ "$leaked" -ne 0 ]]; then
    echo "error: FFmpeg build leaked build-host libraries; see above" >&2
    exit 1
fi
echo "All libraries are free of build-host dependencies."

# Build-time convenience, never shipped (build.sh deploys an explicit
# list): a libdrm symlink inside the prefix lets any later link against
# these libraries resolve their DT_NEEDED closure through the prefix
# itself, host linker configuration notwithstanding (NixOS keeps libdrm
# out of every default search path).
drmlib="$(pkg-config --variable=libdir libdrm)/libdrm.so.2"
ln -sf "$drmlib" "$PREFIX/lib/libdrm.so.2"

# VAAPI must actually have been compiled in: the shipped FFmpeg libraries
# must declare a runtime dependency on the host libva. If --enable-vaapi had
# silently failed to find libva at configure, this would be absent and decode
# would fall back nowhere (hardware decode is mandatory in the Linux column).
needed="$(patchelf --print-needed "$PREFIX/lib/libavutil.so")"
if ! grep -qx 'libva.so.2' <<< "$needed"; then
    echo "error: libavutil.so has no libva.so.2 dependency; --enable-vaapi did not resolve the host libva at configure" >&2
    exit 1
fi

# The decode design stands on this one libva entry point. libva is host-
# provided, so verify the copy this build host would load - the same one an
# end-user host must supply. Capture nm's output into a variable first:
# piping it straight into `grep -q` lets grep close the pipe on the first
# match, and under `set -o pipefail` the SIGPIPE'd nm turns a found symbol
# into a spurious build failure.
host_libva="$(pkg-config --variable=libdir libva)/libva.so.2"
[[ -f "$host_libva" ]] || host_libva="$(cc -print-file-name=libva.so.2)"
if [[ ! -f "$host_libva" ]]; then
    echo "error: cannot locate the host libva.so.2 the player will load at runtime" >&2
    exit 1
fi
va_symbols="$(nm -D "$host_libva")"
if ! grep -q ' vaExportSurfaceHandle$' <<< "$va_symbols"; then
    echo "error: host libva ($host_libva) does not export vaExportSurfaceHandle (need libva >= 2.1)" >&2
    exit 1
fi
echo "Host libva ($host_libva) exports vaExportSurfaceHandle."
