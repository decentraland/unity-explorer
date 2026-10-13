#!/usr/bin/env bash
# Extracts a release zip into <out dir> and prints the build root on stdout.
# Windows zips hold the build files directly. macOS zips hold `build.tar` (tar keeps
# permissions and symlinks), which is unpacked in place; its `build/` folder is the root.
#
# Usage: extract-build.sh <zip file> <out dir>
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ $# -eq 2 ] || die "usage: extract-build.sh <zip file> <out dir>"
[ -f "$1" ] || die "zip not found: $1"
require_tool unzip tar

mkdir -p "$2"
[ -z "$(ls -A "$2")" ] || die "out dir is not empty: $2"
OUT="$(abs_dir "$2")"

unzip -q "$1" -d "$OUT"
if [ "$(ls -A "$OUT")" = "build.tar" ]; then
  tar -xf "$OUT/build.tar" -C "$OUT"
  rm -f "$OUT/build.tar"
fi
if [ "$(ls -A "$OUT")" = "build" ] && [ -d "$OUT/build" ]; then
  echo "$OUT/build"
else
  echo "$OUT"
fi
