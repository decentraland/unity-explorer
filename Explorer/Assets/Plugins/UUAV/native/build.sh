#!/bin/bash

set -e # Stop on error

case "$(uname -s)" in
Darwin)
    # rpath flags for both targets configured in .cargo/config.toml
    DEST_DIR="../Packages/UUAV/Runtime/Plugins/macOS"
    FFMPEG_LIB=".third_party/ffmpeg/lib"

    # Rust's x86_64-apple-darwin default is 10.12; match FFmpeg's slices
    export MACOSX_DEPLOYMENT_TARGET=11.0

    # macOS binaries ship universal; a thin FFmpeg means a stale prefix
    if ! lipo "$FFMPEG_LIB/libavutil.dylib" -verify_arch arm64 x86_64 2> /dev/null; then
        echo "error: $FFMPEG_LIB dylibs are missing or not universal (arm64 + x86_64); re-run scripts/build-ffmpeg-macos.sh" >&2
        exit 1
    fi

    cargo build --release --workspace --target aarch64-apple-darwin
    cargo build --release --workspace --target x86_64-apple-darwin

    mkdir -p "$DEST_DIR"
    # libuuav.dylib is the uuav-client middleware; uuav-helper hosts the
    # decode core out of process and ships next to it so the FFmpeg dylibs
    # resolve via the same @loader_path rpath
    lipo -create \
        ".target/aarch64-apple-darwin/release/libuuav.dylib" \
        ".target/x86_64-apple-darwin/release/libuuav.dylib" \
        -output "$DEST_DIR/libuuav.dylib"
    lipo -create \
        ".target/aarch64-apple-darwin/release/uuav-helper" \
        ".target/x86_64-apple-darwin/release/uuav-helper" \
        -output "$DEST_DIR/uuav-helper"
    # ad-hoc in-process alternative to libuuav.dylib: same FFI surface, no
    # helper process/IPC, selected from Unity via the UUAV_NO_IPC_LAYER define
    lipo -create \
        ".target/aarch64-apple-darwin/release/libuuav_core.dylib" \
        ".target/x86_64-apple-darwin/release/libuuav_core.dylib" \
        -output "$DEST_DIR/libuuav_core.dylib"

    # deploy each FFmpeg dylib under its major-version name (dereferencing
    # the libavutil.60.dylib -> libavutil.60.26.100.dylib symlink): that is
    # the exact name the @rpath install-name references resolve to
    for lib in avcodec avdevice avfilter avformat avutil swresample swscale; do
        major=$(find "$FFMPEG_LIB" -type l -name "lib$lib.*.dylib" | grep -E "lib$lib\.[0-9]+\.dylib$")
        cp -L "$major" "$DEST_DIR/"
    done

    # ad-hoc code signing is mandatory on arm64; lipo and the copies above
    # invalidate whatever signature the build produced, so sign last
    codesign -f -s - "$DEST_DIR"/*.dylib "$DEST_DIR/uuav-helper"

    echo "Deployed to: $DEST_DIR"

    # verification gate: fails the build (set -e) if any deployed dylib is thin
    "$DEST_DIR/doctor-libs.sh"
    ;;
Linux)
    TARGET="x86_64-unknown-linux-gnu" # $ORIGIN runpath configured in .cargo/config.toml
    DEST_DIR="../Packages/UUAV/Runtime/Plugins/linux-x86_64"
    FFMPEG_LIB=".third_party/ffmpeg/lib"

    # deploying runtime libraries from a different FFmpeg build than the
    # one the helper just linked is exactly the mismatch this guard catches
    if [ ! -f "$FFMPEG_LIB/libavutil.so" ]; then
        echo "error: $FFMPEG_LIB is missing; run scripts/build-ffmpeg-linux.sh" >&2
        exit 1
    fi

    cargo build --release --workspace --target "$TARGET"

    mkdir -p "$DEST_DIR"
    cp ".target/$TARGET/release/libuuav.so" "$DEST_DIR/"
    # the helper ships next to libuuav.so so the FFmpeg sonames resolve from
    # its own directory via the $ORIGIN runpath
    cp ".target/$TARGET/release/uuav-helper" "$DEST_DIR/"
    chmod +x "$DEST_DIR/uuav-helper"
    # ad-hoc in-process alternative to libuuav.so: same FFI surface, no
    # helper process/IPC, selected from Unity via the UUAV_NO_IPC_LAYER
    # define (unsupported in Unity on Linux; ships for tooling parity)
    cp ".target/$TARGET/release/libuuav_core.so" "$DEST_DIR/"

    # deploy each shipped FFmpeg library under its soname (dereferencing the
    # libavutil.so.60 -> libavutil.so.60.26.100 symlink): that is the exact
    # name the DT_NEEDED references resolve to. libva/libva-drm are NOT
    # shipped - FFmpeg links them by bare soname and the loader resolves them
    # from the host (see build-ffmpeg-linux.sh); libdrm/libvulkan are
    # host-provided the same way, like on every Linux game.
    for lib in avcodec avdevice avfilter avformat avutil swresample swscale; do
        soname=$(find "$FFMPEG_LIB" -maxdepth 1 -type l -name "lib$lib.so.*" | grep -E "lib$lib\.so\.[0-9]+$")
        cp -L "$soname" "$DEST_DIR/"
    done

    echo "Deployed to: $DEST_DIR"
    ;;
*)
    TARGET="x86_64-pc-windows-gnu" # linker configured in .cargo/config.toml
    DEST_DIR="../Packages/UUAV/Runtime/Plugins/x86_64"
    FFMPEG_BIN=".third_party/ffmpeg/bin"

    # deploying runtime DLLs from a different FFmpeg build than the import
    # libs the helper just linked is exactly the mismatch this guard catches
    if [ ! -d "$FFMPEG_BIN" ]; then
        echo "error: $FFMPEG_BIN is missing; drop the BtbN LGPL shared build into .third_party/ffmpeg (README)" >&2
        exit 1
    fi

    cargo build --release --workspace --target "$TARGET"

    mkdir -p "$DEST_DIR"
    cp ".target/$TARGET/release/uuav.dll" "$DEST_DIR/"
    # the helper ships next to uuav.dll so the FFmpeg DLLs resolve from its
    # own directory
    cp ".target/$TARGET/release/uuav-helper.exe" "$DEST_DIR/"
    # ad-hoc in-process alternative to uuav.dll: same FFI surface, no helper
    # process/IPC, selected from Unity via the UUAV_NO_IPC_LAYER define
    cp ".target/$TARGET/release/uuav_core.dll" "$DEST_DIR/"

    # FFmpeg runtime DLLs from the exact build the helper linked against.
    # The helper's import closure is these four: avfilter/avdevice/swscale
    # are never imported (NV12-only pipeline), and the BtbN shared build
    # links its support libs (bz2/iconv/lzma/zlib/winpthread) statically.
    for lib in avcodec avformat avutil swresample; do
        cp "$FFMPEG_BIN/$lib"-*.dll "$DEST_DIR/"
    done

    echo "Deployed to: $DEST_DIR"
    ;;
esac
