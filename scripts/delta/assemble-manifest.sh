#!/usr/bin/env bash
# Merges the per-base results of make-delta.sh into the delta manifest, printed on stdout:
#   { version, platform, zip: { path, size, sha256 }, files: [...], bases: { <tag>: {...} } }
# `files` is hash-tree.sh of the new build, `zip` describes the published full zip.
#
# Usage: assemble-manifest.sh <version> <platform> <new dir> <zip file> <base-*.json>...
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/lib.sh"

[ $# -ge 4 ] || die "usage: assemble-manifest.sh <version> <platform> <new dir> <zip file> <base-*.json>..."
VERSION="$1"
PLATFORM="$2"
NEW_DIR="$3"
ZIP="$4"
shift 4
[ -d "$NEW_DIR" ] || die "new dir not found: $NEW_DIR"
[ -f "$ZIP" ] || die "zip not found: $ZIP"
require_tool jq

FILES="$(mktemp)"
trap 'rm -f "$FILES"' EXIT
bash "$SCRIPT_DIR/hash-tree.sh" "$NEW_DIR" > "$FILES"

# With no base files, read /dev/null so `inputs` is empty instead of reading stdin.
[ $# -gt 0 ] || set -- /dev/null
jq -n --arg version "$VERSION" --arg platform "$PLATFORM" \
  --arg zip_path "$(basename "$ZIP")" --argjson zip_size "$(size_of "$ZIP")" --arg zip_sha "$(sha256_of "$ZIP")" \
  --slurpfile files "$FILES" '
  {
    version: $version,
    platform: $platform,
    zip: {path: $zip_path, size: $zip_size, sha256: $zip_sha},
    files: $files[0],
    bases: (reduce inputs as $b ({}; . + {($b.base): ($b | del(.base))}))
  }' "$@"
