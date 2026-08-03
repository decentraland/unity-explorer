#!/usr/bin/env bash
# Generates every media asset the UUAV sandbox harness serves, from synthetic
# sources only (testsrc2 / sine). No binary fixture is ever committed: this
# script is the fixture.
#
# Usage: generate-media.sh OUTDIR
#
# Emits OUTDIR/<assets> plus OUTDIR/cases.json - the media-side half of the
# manifest (ids, relative paths, containers, codecs, expectations). The runner
# turns the relative paths into URLs; nothing here knows a hostname or a port,
# which is what lets one build serve from any address.
set -euo pipefail

OUT="${1:?usage: generate-media.sh OUTDIR}"
mkdir -p "$OUT"

# Short by design - the harness proves format coverage, not content.
DUR=4
SIZE=320x180
FPS=15

vsrc() { printf -- '-f lavfi -i testsrc2=size=%s:rate=%s:duration=%s' "$SIZE" "$FPS" "$DUR"; }
asrc() { printf -- '-f lavfi -i sine=frequency=440:sample_rate=48000:duration=%s' "$DUR"; }

# shellcheck disable=SC2086  # word splitting of the source args is intended
ff() { ffmpeg -hide_banner -loglevel error -y "$@"; }

step() { printf '  %-24s' "$1"; }
ok() { printf 'ok\n'; }

echo "uuav-test: generating media into $OUT"

# --- plain files, whitelisted containers and codecs -------------------------

step h264-aac.mp4
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest -movflags +faststart "$OUT/h264-aac.mp4"
ok

step hevc-aac.mp4
# hvc1 tag, not hev1: VideoToolbox and the Windows d3d11va path both want the
# sample entry Apple's tooling writes.
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx265 -preset ultrafast -crf 32 -pix_fmt yuv420p -g "$FPS" \
    -tag:v hvc1 -c:a aac -b:a 64k -shortest -movflags +faststart "$OUT/hevc-aac.mp4"
ok

step vp9-opus.webm
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libvpx-vp9 -deadline realtime -cpu-used 8 -b:v 200k -pix_fmt yuv420p \
    -c:a libopus -b:a 48k -shortest "$OUT/vp9-opus.webm"
ok

step av1-opus.mp4
# Deliberately the smallest asset here: AV1 encode dominates the build.
ff -f lavfi -i "testsrc2=size=160x90:rate=10:duration=2" \
    -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=2" \
    -c:v libsvtav1 -preset 12 -crf 50 -pix_fmt yuv420p -g 10 \
    -c:a libopus -b:a 32k -shortest -movflags +faststart "$OUT/av1-opus.mp4"
ok

step h264-aac.mkv
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest -f matroska "$OUT/h264-aac.mkv"
ok

step h264-aac.ts
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest -f mpegts "$OUT/h264-aac.ts"
ok

# --- HLS --------------------------------------------------------------------

step hls-ts/index.m3u8
mkdir -p "$OUT/hls-ts"
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest \
    -f hls -hls_time 1 -hls_playlist_type vod -hls_segment_type mpegts \
    -hls_segment_filename "$OUT/hls-ts/seg%03d.ts" "$OUT/hls-ts/index.m3u8"
ok

step hls-fmp4/index.m3u8
mkdir -p "$OUT/hls-fmp4"
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest \
    -f hls -hls_time 1 -hls_playlist_type vod -hls_segment_type fmp4 \
    -hls_fmp4_init_filename "init.mp4" \
    -hls_segment_filename "$OUT/hls-fmp4/seg%03d.m4s" "$OUT/hls-fmp4/index.m3u8"
ok

step hls-master/master.m3u8
mkdir -p "$OUT/hls-master"
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) \
    -map 0:v -map 1:a -map 0:v -map 1:a \
    -c:v libx264 -preset veryfast -pix_fmt yuv420p -g "$FPS" -c:a aac -b:a 64k -shortest \
    -filter:v:1 scale=160:90 -b:v:0 400k -b:v:1 120k \
    -var_stream_map "v:0,a:0 v:1,a:1" \
    -f hls -hls_time 1 -hls_playlist_type vod -hls_segment_type mpegts \
    -master_pl_name master.m3u8 \
    -hls_segment_filename "$OUT/hls-master/v%v/seg%03d.ts" "$OUT/hls-master/v%v/index.m3u8"
ok

step hls-aes128/index.m3u8
mkdir -p "$OUT/hls-aes128"
openssl rand 16 >"$OUT/hls-aes128/enc.key"
# The key URI stays relative so the playlist carries no hostname: FFmpeg's HLS
# demuxer resolves it against the playlist url, so the key rides the same https
# origin the manifest came from. This is the case that exercises the crypto and
# data protocols in the retail whitelist.
{
    printf 'enc.key\n'
    printf '%s\n' "$OUT/hls-aes128/enc.key"
} >"$OUT/hls-aes128/keyinfo"
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest \
    -f hls -hls_time 1 -hls_playlist_type vod -hls_segment_type mpegts \
    -hls_key_info_file "$OUT/hls-aes128/keyinfo" \
    -hls_segment_filename "$OUT/hls-aes128/seg%03d.ts" "$OUT/hls-aes128/index.m3u8"
rm -f "$OUT/hls-aes128/keyinfo"
grep -q 'METHOD=AES-128' "$OUT/hls-aes128/index.m3u8" || {
    echo "FATAL: AES-128 playlist carries no EXT-X-KEY" >&2
    exit 1
}
ok

step hls-live/index.m3u8
mkdir -p "$OUT/hls-live"
# A rolling/event playlist standing in for a live stream: an EVENT playlist with
# EXT-X-ENDLIST stripped, so a player keeps reloading it instead of stopping.
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest \
    -f hls -hls_time 1 -hls_playlist_type event -hls_segment_type mpegts \
    -hls_segment_filename "$OUT/hls-live/seg%03d.ts" "$OUT/hls-live/index.m3u8"
grep -v '^#EXT-X-ENDLIST' "$OUT/hls-live/index.m3u8" >"$OUT/hls-live/index.m3u8.tmp"
mv "$OUT/hls-live/index.m3u8.tmp" "$OUT/hls-live/index.m3u8"
ok

# --- DASH -------------------------------------------------------------------

step dash/manifest.mpd
mkdir -p "$OUT/dash"
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p -g "$FPS" \
    -c:a aac -b:a 64k -shortest \
    -f dash -seg_duration 1 -use_template 1 -use_timeline 1 "$OUT/dash/manifest.mpd"
ok

# --- audio-only -------------------------------------------------------------

step audio.mp3
# shellcheck disable=SC2046
ff $(asrc) -c:a libmp3lame -b:a 64k "$OUT/audio.mp3"
ok

step audio.aac
# shellcheck disable=SC2046
ff $(asrc) -c:a aac -b:a 64k -f adts "$OUT/audio.aac"
ok

step audio.wav
# shellcheck disable=SC2046
ff $(asrc) -c:a pcm_s16le -f wav "$OUT/audio.wav"
ok

step audio-vorbis.ogg
# shellcheck disable=SC2046
ff $(asrc) -c:a libvorbis -q:a 2 -f ogg "$OUT/audio-vorbis.ogg"
ok

step audio.flac
# shellcheck disable=SC2046
ff $(asrc) -c:a flac -f flac "$OUT/audio.flac"
ok

step audio-opus.ogg
# shellcheck disable=SC2046
ff $(asrc) -c:a libopus -b:a 48k -f ogg "$OUT/audio-opus.ogg"
ok

# --- assets that must be refused -------------------------------------------

mkdir -p "$OUT/deny"

step deny/h264-aac.avi
# Whitelisted codec in a container that is not in FORMAT_WHITELIST: isolates
# the format gate from the codec gate.
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p \
    -c:a aac -b:a 64k -shortest -f avi "$OUT/deny/h264-aac.avi"
ok

step deny/h264-aac.flv
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libx264 -preset veryfast -crf 30 -pix_fmt yuv420p \
    -c:a aac -b:a 64k -shortest -f flv "$OUT/deny/h264-aac.flv"
ok

step deny/mpeg4.mp4
# The mirror of the AVI case: whitelisted container, MPEG-4 Part 2 video, which
# is not in CODEC_WHITELIST. Isolates the codec gate.
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v mpeg4 -q:v 10 -pix_fmt yuv420p \
    -c:a aac -b:a 64k -shortest -movflags +faststart "$OUT/deny/mpeg4.mp4"
ok

step deny/theora-vorbis.ogg
# shellcheck disable=SC2046
ff $(vsrc) $(asrc) -c:v libtheora -q:v 3 -pix_fmt yuv420p \
    -c:a libvorbis -q:a 2 -shortest -f ogg "$OUT/deny/theora-vorbis.ogg"
ok

step deny/oversize.mp4
# 8192x4320 = 35,389,440 pixels, over the 33,177,600 max_pixels ceiling. One
# frame is enough: the gate fires on the parsed dimensions, before any decode.
ff -f lavfi -i "testsrc2=size=8192x4320:rate=1:duration=1" \
    -frames:v 1 -c:v libx264 -preset ultrafast -qp 40 -pix_fmt yuv420p \
    -movflags +faststart "$OUT/deny/oversize.mp4"
ok

step deny/hls-file/index.m3u8
# THE nested-protocol case. A perfectly ordinary HLS playlist whose segments are
# file:/// urls - the pivot the protocol whitelist exists to stop. The media
# directory is not known until serve time, so the runner substitutes
# @@MEDIA_DIR@@ into the copy it serves.
mkdir -p "$OUT/deny/hls-file"
{
    printf '#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:2\n#EXT-X-MEDIA-SEQUENCE:0\n'
    for seg in "$OUT"/hls-ts/seg*.ts; do
        printf '#EXTINF:1.000,\nfile://@@MEDIA_DIR@@/hls-ts/%s\n' "$(basename "$seg")"
    done
    printf '#EXT-X-ENDLIST\n'
} >"$OUT/deny/hls-file/index.m3u8.template"
ok

# --- the manifest's media half ---------------------------------------------
#
# expected / expected_editor are the two builds the plugin ships: retail
# (protocol_whitelist https,tls,tcp,crypto,data) and the Unity Editor, which
# appends file,http. gate names which of the sandbox's gates does the refusing.

cat >"$OUT/cases.json" <<'JSON'
{
  "cases": [
    { "id": "h264-aac-mp4",          "path": "h264-aac.mp4",              "container": "mov,mp4,m4a,3gp,3g2,mj2", "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "faststart" },
    { "id": "hevc-aac-mp4",          "path": "hevc-aac.mp4",              "container": "mov,mp4,m4a,3gp,3g2,mj2", "video": "hevc",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "hvc1 sample entry" },
    { "id": "vp9-opus-webm",         "path": "vp9-opus.webm",             "container": "matroska,webm",           "video": "vp9",   "audio": "opus",      "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },
    { "id": "av1-opus-mp4",          "path": "av1-opus.mp4",              "container": "mov,mp4,m4a,3gp,3g2,mj2", "video": "av1",   "audio": "opus",      "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "sim_decoder": "av1", "sim_decoder_hazard": false, "note": "160x90, kept tiny: AV1 encode dominates the build. sim_decoder pins the native av1 decoder ONLY to match the plugin's minimal build inside a full test FFmpeg: a full FFmpeg auto-selects libdav1d (name not whitelisted), but the plugin's build ships no libdav1d and avcodec_find_decoder returns the native av1, whose name IS whitelisted. No hazard in the real build" },
    { "id": "h264-aac-mkv",          "path": "h264-aac.mkv",              "container": "matroska,webm",           "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },
    { "id": "h264-aac-ts",           "path": "h264-aac.ts",               "container": "mpegts",                  "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },
    { "id": "hls-ts",                "path": "hls-ts/index.m3u8",         "container": "hls",                     "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "mpegts segments" },
    { "id": "hls-fmp4",              "path": "hls-fmp4/index.m3u8",       "container": "hls",                     "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "fMP4 segments, needs the mov demuxer for the segments" },
    { "id": "hls-master",            "path": "hls-master/master.m3u8",    "container": "hls",                     "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "multi-variant master playlist, 2 renditions" },
    { "id": "hls-aes128",            "path": "hls-aes128/index.m3u8",     "container": "hls",                     "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "AES-128, relative key URI: exercises the crypto protocol" },
    { "id": "hls-live-event",        "path": "hls-live/index.m3u8",       "container": "hls",                     "video": "h264",  "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "EVENT playlist with EXT-X-ENDLIST stripped: stands in for a live stream" },
    { "id": "dash-mpd",              "path": "dash/manifest.mpd",         "container": "dash",                    "video": "h264",  "audio": "aac",       "expected": "BUILD_DEPENDENT", "expected_editor": "BUILD_DEPENDENT", "gate": "build_demuxer", "note": "dash is in FORMAT_WHITELIST but the demuxer needs libxml2; a from-source build per native/scripts/build-ffmpeg-*.sh has no libxml2 and drops it" },
    { "id": "audio-mp3",             "path": "audio.mp3",                 "container": "mp3",                     "video": null,    "audio": "mp3",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "sim_decoder": "mp3", "sim_decoder_hazard": true, "note": "HAZARD IN SHIPPED CODE: CODEC_WHITELIST lists the token mp3, but avcodec_find_decoder(AV_CODEC_ID_MP3) returns the decoder NAMED mp3float (first in the decoder list; the build enables both mp3 and mp3float). The codec_whitelist FFmpeg option av_match_lists against the decoder NAME, so avformat_find_stream_info's probe decoder is rejected and a legitimate mp3 fails to open. sim_decoder pins the fixed-point mp3 the whitelist token does accept; sim_decoder_hazard makes the check WARN so the bug stays visible" },
    { "id": "audio-aac-adts",        "path": "audio.aac",                 "container": "aac",                     "video": null,    "audio": "aac",       "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": "raw ADTS" },
    { "id": "audio-wav-pcm16",       "path": "audio.wav",                 "container": "wav",                     "video": null,    "audio": "pcm_s16le", "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },
    { "id": "audio-ogg-vorbis",      "path": "audio-vorbis.ogg",          "container": "ogg",                     "video": null,    "audio": "vorbis",    "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },
    { "id": "audio-flac",            "path": "audio.flac",                "container": "flac",                    "video": null,    "audio": "flac",      "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },
    { "id": "audio-opus-ogg",        "path": "audio-opus.ogg",            "container": "ogg",                     "video": null,    "audio": "opus",      "expected": "PLAYS",   "expected_editor": "PLAYS",   "gate": null,                 "note": null },

    { "id": "deny-container-avi",    "path": "deny/h264-aac.avi",         "container": "avi",                     "video": "h264",  "audio": "aac",       "expected": "REFUSED", "expected_editor": "REFUSED", "gate": "format_whitelist",   "note": "whitelisted codec, non-whitelisted container: isolates the format gate" },
    { "id": "deny-container-flv",    "path": "deny/h264-aac.flv",         "container": "flv",                     "video": "h264",  "audio": "aac",       "expected": "REFUSED", "expected_editor": "REFUSED", "gate": "format_whitelist",   "note": null },
    { "id": "deny-codec-mpeg4",      "path": "deny/mpeg4.mp4",            "container": "mov,mp4,m4a,3gp,3g2,mj2", "video": "mpeg4", "audio": "aac",       "expected": "REFUSED", "expected_editor": "REFUSED", "gate": "codec_whitelist",    "note": "whitelisted container, MPEG-4 Part 2 video: isolates the codec gate" },
    { "id": "deny-codec-theora",     "path": "deny/theora-vorbis.ogg",    "container": "ogg",                     "video": "theora","audio": "vorbis",    "expected": "REFUSED", "expected_editor": "REFUSED", "gate": "codec_whitelist",    "note": "audio codec is whitelisted, video codec is not" },
    { "id": "deny-oversize-pixels",  "path": "deny/oversize.mp4",         "container": "mov,mp4,m4a,3gp,3g2,mj2", "video": "h264",  "audio": null,        "expected": "REFUSED", "expected_editor": "REFUSED", "gate": "max_pixels",         "note": "8192x4320 = 35389440 px, over the 33177600 ceiling" },
    { "id": "deny-hls-file-segments","path": "deny/hls-file/index.m3u8",  "container": "hls",                     "video": "h264",  "audio": "aac",       "expected": "REFUSED", "expected_editor": "PLAYS",   "gate": "protocol_whitelist", "verify": "playlist", "note": "nested-protocol pivot: a valid playlist whose segments are file:/// urls. The Editor build adds file to the whitelist, so it PLAYS there - that difference is the point of the retail list" }
  ]
}
JSON

# Fail the build rather than ship a manifest that lies about what exists.
missing=0
while read -r rel; do
    [ -e "$OUT/$rel" ] || [ -e "$OUT/$rel.template" ] || {
        echo "FATAL: cases.json references missing $rel" >&2
        missing=1
    }
done < <(sed -n 's/.*"path": "\([^"]*\)".*/\1/p' "$OUT/cases.json")
[ "$missing" -eq 0 ] || exit 1

echo "uuav-test: $(sed -n 's/.*"id": .*/x/p' "$OUT/cases.json" | wc -l) file-backed cases generated"
du -sh "$OUT" | sed 's/^/  total /'
