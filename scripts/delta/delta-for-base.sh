#!/usr/bin/env bash
# Produces and proves the patch set from one base release: extracts the base zip, runs
# make-delta.sh, replays it with verify-delta.sh, and checks that the rebuilt build's
# signatures verify exactly like the new build's (signature-status.sh).
#
# Usage: delta-for-base.sh <version> <platform> <new root> <new zip> <base tag> <base zip> <out dir>
#
# On success <out dir> holds from/<base tag>/ and base-<base tag>.json. On any failure
# both are removed and the exit code is non-zero. Scratch space comes from mktemp (TMPDIR).
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/lib.sh"

[ $# -eq 7 ] || die "usage: delta-for-base.sh <version> <platform> <new root> <new zip> <base tag> <base zip> <out dir>"
VERSION="$1" PLATFORM="$2" NEW_ROOT="$3" NEW_ZIP="$4" BASE_TAG="$5" BASE_ZIP="$6" OUT="$7"
case "$BASE_TAG" in ''|*/*|.*) die "invalid base tag: '$BASE_TAG'" ;; esac
[ -d "$NEW_ROOT" ] || die "new root not found: $NEW_ROOT"
[ -f "$NEW_ZIP" ] || die "new zip not found: $NEW_ZIP"
[ -f "$BASE_ZIP" ] || die "base zip not found: $BASE_ZIP"
mkdir -p "$OUT"

TMP="$(mktemp -d)"
cleanup() {
  local rc=$?
  rm -rf "$TMP"
  if [ $rc -ne 0 ]; then
    rm -rf "${OUT:?}/from/$BASE_TAG" "$OUT/base-$BASE_TAG.json"
    log "$BASE_TAG: failed, its patch set was removed"
  fi
}
trap cleanup EXIT

BASE_ROOT="$(bash "$SCRIPT_DIR/extract-build.sh" "$BASE_ZIP" "$TMP/base")"
bash "$SCRIPT_DIR/make-delta.sh" "$BASE_ROOT" "$NEW_ROOT" "$BASE_TAG" "$OUT"
bash "$SCRIPT_DIR/assemble-manifest.sh" "$VERSION" "$PLATFORM" "$NEW_ROOT" "$NEW_ZIP" \
  "$OUT/base-$BASE_TAG.json" > "$TMP/manifest.json"
REBUILT="$(bash "$SCRIPT_DIR/verify-delta.sh" "$BASE_ROOT" "$OUT" "$TMP/manifest.json" "$BASE_TAG" "$TMP/rebuild")"

bash "$SCRIPT_DIR/signature-status.sh" "$PLATFORM" "$NEW_ROOT" > "$TMP/signatures-new.txt"
bash "$SCRIPT_DIR/signature-status.sh" "$PLATFORM" "$REBUILT" > "$TMP/signatures-rebuilt.txt"
diff -u "$TMP/signatures-new.txt" "$TMP/signatures-rebuilt.txt" >&2 ||
  die "$BASE_TAG: signature verification of the rebuilt build differs from the new build"
log "$BASE_TAG: patch set verified ($(wc -l < "$TMP/signatures-new.txt" | tr -d ' ') signed artifacts match)"
