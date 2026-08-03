#!/bin/bash
# Regenerates crates/uuav-abi-smoke/assets/frame-check-640x360.mp4, the known
# clip uuav-frame-check asserts against: four flat quadrants (TL red, TR green,
# BL blue, BR white), 1s @ 30fps, H.264 yuv420p, faststart. The quadrant
# geometry and the YCbCr tolerances live in
# crates/uuav-abi-smoke/src/bin/uuav-frame-check/windows.rs; change one, change
# both.
set -e
cd "$(dirname "$0")/.."

OUT="crates/uuav-abi-smoke/assets/frame-check-640x360.mp4"
mkdir -p "$(dirname "$OUT")"

ffmpeg -y -loglevel error \
    -f lavfi -i "color=c=0xFF0000:size=320x180:rate=30" \
    -f lavfi -i "color=c=0x00FF00:size=320x180:rate=30" \
    -f lavfi -i "color=c=0x0000FF:size=320x180:rate=30" \
    -f lavfi -i "color=c=0xFFFFFF:size=320x180:rate=30" \
    -filter_complex "[0:v][1:v]hstack[top];[2:v][3:v]hstack[bot];[top][bot]vstack,format=yuv420p" \
    -t 1 -c:v libx264 -preset veryfast -crf 18 \
    -colorspace smpte170m -color_primaries smpte170m -color_trc smpte170m \
    -movflags +faststart \
    "$OUT"

ls -la "$OUT"
