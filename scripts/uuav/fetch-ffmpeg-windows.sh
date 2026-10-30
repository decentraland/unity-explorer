#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOCK="$REPO_ROOT/scripts/uuav/uuav-binaries.lock.json"
PREFIX="${1:-$REPO_ROOT/Explorer/Assets/Plugins/UUAV/native/.third_party/ffmpeg}"

read -r URL EXPECTED_SHA ASSET RELEASE_TAG PROVENANCE < <(
    python3 -c '
import json, sys
# Windows Python writes CRLF in text mode; read would keep the \r on the
# last variable and break the provenance comparison below.
sys.stdout.reconfigure(newline="\n")
ffmpeg = json.load(open(sys.argv[1]))["targets"]["windows-x86_64"]["ffmpeg"]
print(ffmpeg.get("url", "-"), ffmpeg.get("asset_sha256", "-"),
      ffmpeg.get("asset", "-"), ffmpeg.get("release_tag", "-"),
      ffmpeg["provenance"])' "$LOCK"
)

if [[ "$PROVENANCE" != "third-party-release-asset" ]]; then
    echo "error: the lock records windows-x86_64 FFmpeg as '$PROVENANCE', not a" >&2
    echo "       downloadable release asset. Build it from source instead; see" >&2
    echo "       Explorer/Assets/Plugins/UUAV/README.md." >&2
    exit 1
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "Release: $RELEASE_TAG"
echo "Asset:   $ASSET"
curl --fail --location --show-error --silent --output "$WORK/ffmpeg.zip" "$URL"

ACTUAL_SHA="$(sha256sum "$WORK/ffmpeg.zip" | cut -d' ' -f1)"
if [[ "$ACTUAL_SHA" != "$EXPECTED_SHA" ]]; then
    echo "error: archive does not match the lock - refusing to unpack." >&2
    echo "  expected sha256 $EXPECTED_SHA" >&2
    echo "  actual   sha256 $ACTUAL_SHA" >&2
    exit 1
fi
echo "sha256:  $ACTUAL_SHA (matches lock)"

unzip -q "$WORK/ffmpeg.zip" -d "$WORK/unpacked"
SRC="$(find "$WORK/unpacked" -mindepth 1 -maxdepth 1 -type d)"

rm -rf "$PREFIX"
mkdir -p "$PREFIX"
cp -R "$SRC"/. "$PREFIX"/

echo "Unpacked into: $PREFIX"
