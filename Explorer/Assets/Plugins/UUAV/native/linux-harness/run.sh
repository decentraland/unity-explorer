#!/bin/bash
# Runs the harness tests on a Linux box against an FFmpeg 8.1 install.
#
# FFMPEG_DIR must be a prefix holding include/ and lib/ for FFmpeg 8.1 (the
# release the core pins via scripts/build-ffmpeg-*.sh). On NixOS, compose one
# from the store's ffmpeg-full dev+lib outputs:
#
#   mkdir -p /tmp/uuav-ffprefix
#   ln -sfn <ffmpeg-full-8.1-dev>/include /tmp/uuav-ffprefix/include
#   ln -sfn <ffmpeg-full-8.1-lib>/lib     /tmp/uuav-ffprefix/lib
#
# LIBCLANG_PATH must point at a libclang for bindgen (any recent clang-lib).
set -e
cd "$(dirname "$0")"

: "${FFMPEG_DIR:?point FFMPEG_DIR at an FFmpeg 8.1 prefix (include/ + lib/)}"
export FFMPEG_DIR
export LD_LIBRARY_PATH="$FFMPEG_DIR/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

exec cargo test "$@"
