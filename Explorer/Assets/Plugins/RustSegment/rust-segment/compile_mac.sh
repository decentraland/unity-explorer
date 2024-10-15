#!/bin/bash

# This script is used to compile the Rust code on macOS for both x86_64 and aarch64
cargo build --release --target x86_64-apple-darwin
cargo build --release --target aarch64-apple-darwin

lipo -create -output ../SegmentServerWrap/Libraries/Mac/segment-server.dylib target/x86_64-apple-darwin/release/librust_segment.dylib target/aarch64-apple-darwin/release/librust_segment.dylib

# Cleanup target folder to avoid confusion from Unity
rm -rf target