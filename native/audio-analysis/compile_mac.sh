#!/bin/bash

# This script is used to compile the Rust code on macOS for both x86_64 and aarch64
cargo build --release --target x86_64-apple-darwin
cargo build --release --target aarch64-apple-darwin

lipo -create -output ../../Explorer/Assets/Plugins/NativeAudioAnalysis/Libraries/audio-analysis.dylib target/x86_64-apple-darwin/release/libaudio_analysis.dylib target/aarch64-apple-darwin/release/libaudio_analysis.dylib
