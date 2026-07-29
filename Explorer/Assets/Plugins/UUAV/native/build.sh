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

    cargo build --release --target aarch64-apple-darwin
    cargo build --release --target x86_64-apple-darwin

    mkdir -p "$DEST_DIR"
    lipo -create \
        ".target/aarch64-apple-darwin/release/libuuav.dylib" \
        ".target/x86_64-apple-darwin/release/libuuav.dylib" \
        -output "$DEST_DIR/libuuav.dylib"

    # deploy each FFmpeg dylib under its major-version name (dereferencing
    # the libavutil.60.dylib -> libavutil.60.26.100.dylib symlink): that is
    # the exact name the @rpath install-name references resolve to
    for lib in avcodec avdevice avfilter avformat avutil swresample swscale; do
        major=$(find "$FFMPEG_LIB" -type l -name "lib$lib.*.dylib" | grep -E "lib$lib\.[0-9]+\.dylib$")
        cp -L "$major" "$DEST_DIR/"
    done

    # ad-hoc code signing is mandatory on arm64; lipo and the copies above
    # invalidate whatever signature the build produced, so sign last
    codesign -f -s - "$DEST_DIR"/*.dylib

    echo "Deployed to: $DEST_DIR"

    # verification gate: fails the build (set -e) if any deployed dylib is thin
    "$DEST_DIR/doctor-libs.sh"
    ;;
*)
    TARGET="x86_64-pc-windows-gnu" # linker configured in .cargo/config.toml
    DEST_DIR="../Packages/UUAV/Runtime/Plugins/x86_64"

    cargo build --release --target "$TARGET"

    mkdir -p "$DEST_DIR"
    cp ".target/$TARGET/release/uuav.dll" "$DEST_DIR/"

    echo "Deployed to: $DEST_DIR"
    echo "Make sure to provied libwinpthread-1.dll and ffmpeg binaries mentioned in the readme file"
    ;;
esac
