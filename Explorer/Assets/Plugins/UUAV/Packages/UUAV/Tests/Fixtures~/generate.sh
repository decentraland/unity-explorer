#!/usr/bin/env bash
# Regenerates the committed media fixtures used by the UUAV PlayMode tests.
# The outputs are committed so test runs never depend on a local ffmpeg;
# rerun this script only when a fixture needs to change, then commit the
# results. Requires ffmpeg with libx264 + aac.
#
# tone_color_bands.mp4 layout (the tests assert against these constants,
# keep Fixtures.cs in sync):
#   duration 6 s, 320x240 @ 30 fps, keyframe every second
#   video: solid red [0,2) s, green [2,4) s, blue [4,6) s
#   audio: 440 Hz sine boosted +18 dB (peak ~-3 dB), stereo 48 kHz aac
set -euo pipefail
cd "$(dirname "$0")"

ffmpeg -hide_banner -loglevel error -y \
    -f lavfi -i "color=c=red:size=320x240:rate=30:duration=2" \
    -f lavfi -i "color=c=green:size=320x240:rate=30:duration=2" \
    -f lavfi -i "color=c=blue:size=320x240:rate=30:duration=2" \
    -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=6" \
    -filter_complex "[0:v][1:v][2:v]concat=n=3:v=1:a=0[v];[3:a]volume=18dB,aformat=sample_fmts=fltp:channel_layouts=stereo[a]" \
    -map "[v]" -map "[a]" \
    -c:v libx264 -preset veryfast -profile:v baseline -pix_fmt yuv420p \
    -g 30 -keyint_min 30 \
    -c:a aac -b:a 96k \
    -movflags +faststart \
    tone_color_bands.mp4

ffmpeg -hide_banner -loglevel error -y \
    -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=6" \
    -af "volume=18dB,aformat=sample_fmts=fltp:channel_layouts=stereo" \
    -c:a aac -b:a 96k -movflags +faststart \
    audio_only.m4a

ffmpeg -hide_banner -loglevel error -y \
    -f lavfi -i "color=c=red:size=320x240:rate=30:duration=6" \
    -c:v libx264 -preset veryfast -profile:v baseline -pix_fmt yuv420p \
    -g 30 -keyint_min 30 \
    -movflags +faststart \
    video_only.mp4

# deterministic non-media bytes behind an .mp4 name: no demuxer matches
python3 -c "import sys; sys.stdout.buffer.write(b'UUAV-GARBAGE-FIXTURE-NOT-A-MEDIA-FILE\n' * 128)" > garbage.mp4

# valid ftyp followed by a cut-off moov: probing fails, open must error.
# FFmpeg's tolerance of truncated moovs is non-monotonic in the cut size
# (1500 bytes opened fine!), so verify the file really does not open -
# the RejectTruncatedContainer test depends on it.
head -c 600 tone_color_bands.mp4 > truncated.mp4
if ffprobe -v error truncated.mp4 2>/dev/null; then
    echo "ERROR: truncated.mp4 still opens; pick a different cut size" >&2
    exit 1
fi

ls -la *.mp4 *.m4a
