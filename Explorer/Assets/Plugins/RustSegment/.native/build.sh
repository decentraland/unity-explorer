#!/bin/bash

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

# --locked: Cargo.lock is a hashed build input, so a manifest change that would rewrite it must fail.
CARGO_FLAGS=(--release --locked)

case "$(uname -s)" in
Darwin)
    DEST="../SegmentServerWrap/Libraries/Mac/segment-server.dylib"

    cargo build "${CARGO_FLAGS[@]}" --target aarch64-apple-darwin
    cargo build "${CARGO_FLAGS[@]}" --target x86_64-apple-darwin

    mkdir -p "$(dirname "$DEST")"
    lipo -create \
        target/aarch64-apple-darwin/release/librust_segment.dylib \
        target/x86_64-apple-darwin/release/librust_segment.dylib \
        -output "$DEST"
    ;;
*)
    # MSVC, not GNU: the shipped DLL has always imported the UCRT that Plugins/.VCRedist deploys.
    TARGET="x86_64-pc-windows-msvc"
    DEST="../SegmentServerWrap/Libraries/Windows/segment-server.dll"

    cargo build "${CARGO_FLAGS[@]}" --target "$TARGET"

    mkdir -p "$(dirname "$DEST")"
    cp "target/$TARGET/release/rust_segment.dll" "$DEST"
    ;;
esac

echo "Deployed to: $DEST"
