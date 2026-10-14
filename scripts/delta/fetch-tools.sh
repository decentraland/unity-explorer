#!/usr/bin/env bash
# Installs the pinned HDiffPatch release (hdiffz, hpatchz) for the current OS into
# <tools dir>, checking the download against its pinned sha256, and prints the folder
# holding the binaries on stdout (add it to PATH). Pinned so CI and local runs produce
# identical patches.
#
# Usage: fetch-tools.sh <tools dir>
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

HDIFFPATCH_VERSION="v5.1.3"

[ $# -eq 1 ] || die "usage: fetch-tools.sh <tools dir>"
require_tool curl unzip

case "$(uname -s)/$(uname -m)" in
  Linux/x86_64)
    ASSET=linux64
    SHA256=628963bf2ee9108a97260fa5eef44acd9ec94369b76090a957c9182b3abbb558 ;;
  Darwin/*)
    ASSET=macos
    SHA256=9e9d7318db1ea5607dbdde41e6614cfe5b73c589034b38ee91a79f4ca80a940c ;;
  MINGW*/*|MSYS*/*|CYGWIN*/*)
    ASSET=windows64
    SHA256=77f141386e5d8f785c1c846e10fbbc19b6c05aa00e3f59cc44670fb3f0e2ae94 ;;
  *)
    die "no pinned HDiffPatch build for $(uname -s)/$(uname -m)" ;;
esac

mkdir -p "$1"
TOOLS="$(abs_dir "$1")"
BIN="$TOOLS/$ASSET"
ZIP="$TOOLS/hdiffpatch_${HDIFFPATCH_VERSION}_bin_$ASSET.zip"

if [ ! -f "$ZIP" ] || [ "$(sha256_of "$ZIP")" != "$SHA256" ]; then
  log "downloading HDiffPatch $HDIFFPATCH_VERSION ($ASSET)"
  curl -fsSL --retry 3 -o "$ZIP" \
    "https://github.com/sisong/HDiffPatch/releases/download/$HDIFFPATCH_VERSION/hdiffpatch_${HDIFFPATCH_VERSION}_bin_$ASSET.zip"
fi
ACTUAL="$(sha256_of "$ZIP")"
[ "$ACTUAL" = "$SHA256" ] || { rm -f "$ZIP"; die "HDiffPatch sha256 mismatch: got $ACTUAL, want $SHA256"; }

rm -rf "$BIN"
unzip -q -o "$ZIP" -d "$TOOLS"
[ -d "$BIN" ] || die "unexpected HDiffPatch archive layout: no $ASSET/ folder"
chmod +x "$BIN"/hdiffz* "$BIN"/hpatchz*
echo "$BIN"
