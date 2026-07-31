#!/bin/bash

# Builds libzmq as a shared library into .third_party/libzmq (the machine's
# platform only), mirroring the FFmpeg provisioning flow. The uuav client
# dylib and the uuav-helper both link it dynamically; build.sh deploys it
# next to them so it resolves via @loader_path (macOS) / same-dir (Windows).

set -e

# zeromq 4.3.x declares a cmake_minimum_required older than CMake 4
# accepts; this floor lets modern CMake configure it unchanged
export CMAKE_POLICY_VERSION_MINIMUM=3.5

ZMQ_VERSION="4.3.5"
ZMQ_SHA256="6653ef5910f17954861fe72332e68b03ca6e4d9c7160eb3a8de5a5a913bfab43"
ZMQ_URL="https://github.com/zeromq/libzmq/releases/download/v$ZMQ_VERSION/zeromq-$ZMQ_VERSION.tar.gz"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$ROOT/.libzmq-build"
PREFIX="$ROOT/.third_party/libzmq"
SRC="$WORK/zeromq-$ZMQ_VERSION"

mkdir -p "$WORK"
TARBALL="$WORK/zeromq-$ZMQ_VERSION.tar.gz"

if [ ! -f "$TARBALL" ]; then
    curl -sL -o "$TARBALL" "$ZMQ_URL"
fi
echo "$ZMQ_SHA256  $TARBALL" | shasum -a 256 -c - > /dev/null

rm -rf "$SRC"
tar -xzf "$TARBALL" -C "$WORK"

# curve/draft/tools stay off: the uuav transport is plain local sockets, and
# less surface means fewer platform quirks
COMMON_FLAGS=(
    -DBUILD_SHARED=ON
    -DBUILD_STATIC=OFF
    -DZMQ_BUILD_TESTS=OFF
    -DENABLE_CURVE=OFF
    -DENABLE_DRAFTS=OFF
    -DWITH_PERF_TOOL=OFF
    -DWITH_DOCS=OFF
    -DWITH_LIBSODIUM=OFF
    -DCMAKE_BUILD_TYPE=Release
)

case "$(uname -s)" in
Darwin)
    # universal dylib, same deployment target as the FFmpeg build
    export MACOSX_DEPLOYMENT_TARGET=11.0

    for arch in arm64 x86_64; do
        cmake -S "$SRC" -B "$WORK/build-$arch" \
            "${COMMON_FLAGS[@]}" \
            -DCMAKE_OSX_ARCHITECTURES="$arch" \
            -DCMAKE_INSTALL_PREFIX="$WORK/out-$arch" \
            -DCMAKE_INSTALL_LIBDIR=lib \
            -DCMAKE_INSTALL_NAME_DIR=@rpath > /dev/null
        cmake --build "$WORK/build-$arch" --target install --parallel > /dev/null
    done

    mkdir -p "$PREFIX/lib" "$PREFIX/include"

    # deploy under the major-version name the install-name references
    # (libzmq.5.dylib), plus the bare-name symlink the -lzmq link line needs
    major=$(find "$WORK/out-arm64/lib" -type l -name "libzmq.*.dylib" | grep -E "libzmq\.[0-9]+\.dylib$")
    major_name=$(basename "$major")
    lipo -create \
        "$WORK/out-arm64/lib/$major_name" \
        "$WORK/out-x86_64/lib/$major_name" \
        -output "$PREFIX/lib/$major_name"
    ln -sf "$major_name" "$PREFIX/lib/libzmq.dylib"

    cp "$WORK/out-arm64/include/zmq.h" "$WORK/out-arm64/include/zmq_utils.h" "$PREFIX/include/"
    cp "$SRC/LICENSE" "$PREFIX/LICENSE.txt"

    lipo -archs "$PREFIX/lib/$major_name"
    echo "Deployed universal $major_name to $PREFIX/lib"
    ;;
*)
    # native MSYS2/mingw64 build on the Windows box (same environment
    # build.sh already requires). ZMQ_HAVE_IPC is force-disabled: libzmq
    # 4.3.x gates afunix.h on _MSC_VER, breaking mingw builds, and ipc://
    # is unused on Windows (the uuav transport is loopback TCP there).
    # The gcc/stdc++ runtimes are linked statically so the shipped
    # libzmq.dll resolves on machines without MSYS2 on PATH.
    cmake -S "$SRC" -B "$WORK/build" \
        "${COMMON_FLAGS[@]}" \
        -G "MinGW Makefiles" \
        -DZMQ_HAVE_IPC=OFF \
        -DCMAKE_SHARED_LINKER_FLAGS="-static-libstdc++ -static-libgcc -static -lpthread" \
        -DCMAKE_INSTALL_PREFIX="$WORK/out" \
        -DCMAKE_INSTALL_LIBDIR=lib > /dev/null
    cmake --build "$WORK/build" --target install --parallel > /dev/null

    mkdir -p "$PREFIX/lib" "$PREFIX/include" "$PREFIX/bin"
    cp "$WORK/out/lib/"libzmq*.dll.a "$PREFIX/lib/"
    cp "$WORK/out/bin/"libzmq*.dll "$PREFIX/bin/"
    # symbols are dead weight in the shipped DLL (-40%)
    strip "$PREFIX/bin/"libzmq*.dll
    cp "$WORK/out/include/zmq.h" "$WORK/out/include/zmq_utils.h" "$PREFIX/include/"
    cp "$SRC/LICENSE" "$PREFIX/LICENSE.txt"

    echo "Deployed libzmq.dll + import lib to $PREFIX"
    ;;
esac
