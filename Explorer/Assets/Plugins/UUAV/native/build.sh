#!/bin/bash

set -e # Stop on error

BUILD_ADAPTER="${UUAV_BUILD_ADAPTER:-1}"

META_DIR="../../../../../scripts/uuav/unity"

case "$(uname -s)" in
Darwin)
    DEST_DIR="../Packages/UUAV/Runtime/Plugins/macOS"
    FFMPEG_LIB=".third_party/ffmpeg/lib"

    export MACOSX_DEPLOYMENT_TARGET=11.0

    if ! lipo "$FFMPEG_LIB/libavutil.dylib" -verify_arch arm64 x86_64 2> /dev/null; then
        echo "error: $FFMPEG_LIB dylibs are missing or not universal (arm64 + x86_64); re-run scripts/build-ffmpeg-macos.sh" >&2
        exit 1
    fi

    cargo build --release --target aarch64-apple-darwin -p uuav-client
    cargo build --release --target x86_64-apple-darwin -p uuav-client

    if [[ "$BUILD_ADAPTER" == "1" ]]; then
        cargo build --release --target aarch64-apple-darwin -p uuav-adapter
        cargo build --release --target x86_64-apple-darwin -p uuav-adapter
    fi

    mkdir -p "$DEST_DIR"
    lipo -create \
        ".target/aarch64-apple-darwin/release/libuuav.dylib" \
        ".target/x86_64-apple-darwin/release/libuuav.dylib" \
        -output "$DEST_DIR/libuuav.dylib"

    for lib in avcodec avdevice avfilter avformat avutil swresample swscale; do
        major=$(find "$FFMPEG_LIB" -type l -name "lib$lib.*.dylib" | grep -E "lib$lib\.[0-9]+\.dylib$")
        cp -L "$major" "$DEST_DIR/"
    done

    codesign -f -s - "$DEST_DIR"/*.dylib

    if [[ "$BUILD_ADAPTER" == "1" ]]; then
        lipo -create \
            ".target/aarch64-apple-darwin/release/uuav-adapter" \
            ".target/x86_64-apple-darwin/release/uuav-adapter" \
            -output "$DEST_DIR/uuav-adapter"
        chmod +x "$DEST_DIR/uuav-adapter"

        codesign -f -s - "$DEST_DIR/uuav-adapter"
        lipo "$DEST_DIR/uuav-adapter" -verify_arch arm64 x86_64

        if ! otool -l "$DEST_DIR/uuav-adapter" | grep -q '@loader_path'; then
            echo "error: $DEST_DIR/uuav-adapter carries no @loader_path LC_RPATH; it cannot resolve the FFmpeg dylibs beside it" >&2
            exit 1
        fi

        cp "$META_DIR/uuav-adapter.meta" "$DEST_DIR/uuav-adapter.meta"
    fi

    echo "Deployed to: $DEST_DIR"

    "$DEST_DIR/doctor-libs.sh"
    ;;
*)
    TARGET="x86_64-pc-windows-msvc" # Control Flow Guard flags in .cargo/config.toml
    DEST_DIR="../Packages/UUAV/Runtime/Plugins/x86_64"
    FFMPEG_BIN=".third_party/ffmpeg/bin"

    if [[ ! -d "$FFMPEG_BIN" ]]; then
        echo "error: $FFMPEG_BIN is missing; run scripts/build-ffmpeg-windows.cmd first" >&2
        exit 1
    fi

    if [[ -z "${LIBCLANG_PATH:-}" ]]; then
        export LIBCLANG_PATH="${LLVM_ROOT:-C:\\Program Files\\LLVM}\\bin"
    fi

    LINK_ON_PATH="$(command -v link 2> /dev/null || true)"
    if [[ -z "${VCINSTALLDIR:-}${VSINSTALLDIR:-}" || "$LINK_ON_PATH" == /usr/bin/link* || "$LINK_ON_PATH" == /bin/link* ]]; then
        echo "error: cannot reach MSVC's link.exe (found ${LINK_ON_PATH:-none on PATH}); cargo would fail with \"link: missing operand\"" >&2
        echo "       enter vcvars64.bat, then from inside bash put the MSVC linker first:" >&2
        echo "         export PATH=\"\$VCToolsInstallDir/bin/Hostx64/x64:\$PATH\"" >&2
        echo "       and invoke Git Bash by absolute path - a bare 'bash' may be the WSL stub, which cannot build this target" >&2
        exit 1
    fi

    cargo build --release --target "$TARGET" -p uuav-client

    if [[ "$BUILD_ADAPTER" == "1" ]]; then
        cargo build --release --target "$TARGET" -p uuav-adapter
    fi

    mkdir -p "$DEST_DIR"
    cp ".target/$TARGET/release/uuav.dll" "$DEST_DIR/"
    cp "$FFMPEG_BIN"/*.dll "$DEST_DIR/"

    if [[ "$BUILD_ADAPTER" == "1" ]]; then
        cp ".target/$TARGET/release/uuav-adapter.exe" "$DEST_DIR/"
        cp "$META_DIR/uuav-adapter.exe.meta" "$DEST_DIR/uuav-adapter.exe.meta"
    fi

    echo "Deployed to: $DEST_DIR"

    PYTHON_BIN="$(command -v python3 || command -v python || true)"
    if [[ -z "$PYTHON_BIN" ]]; then
        echo "error: no python on PATH; scripts/doctor-libs-windows.py cannot verify the deployed modules" >&2
        exit 1
    fi
    "$PYTHON_BIN" "scripts/doctor-libs-windows.py" "$DEST_DIR"
    ;;
esac
