#!/usr/bin/env bash
# Applies a delta onto a copy of the base and proves the result is byte-identical to the
# new build described by the manifest.
#
# Usage: verify-delta.sh <base dir> <delta dir> <manifest.json> <base tag> <rebuild dir>
#
# Copies <base dir> into <rebuild dir> (which must not exist or be empty), removes
# `deleted` paths and the directories they leave empty, checks each patch's sha256,
# applies patches and whole files, applies `symlink` and `mode` entries, then compares
# the rebuilt tree with `files[]`. Deleting first keeps a case-only rename or a directory
# replaced by a symlink from losing new files.
# Prints the absolute rebuild dir on stdout and exits 0 only on an exact match;
# mismatches are listed on stderr. The caller owns and removes <rebuild dir>.
# On Windows (Git Bash) permission bits are not meaningful, so `mode` is not compared.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/lib.sh"

[ $# -eq 5 ] || die "usage: verify-delta.sh <base dir> <delta dir> <manifest.json> <base tag> <rebuild dir>"
BASE_TAG="$4"
[ -d "$1" ] || die "base dir not found: $1"
[ -d "$2" ] || die "delta dir not found: $2"
[ -f "$3" ] || die "manifest not found: $3"
require_tool jq "$HPATCHZ"

BASE_DIR="$(abs_dir "$1")"
DELTA_DIR="$(abs_dir "$2")"
MANIFEST="$3"
jq -e --arg t "$BASE_TAG" '.bases[$t]' "$MANIFEST" > /dev/null || die "manifest has no base '$BASE_TAG'"

mkdir -p "$5"
[ -z "$(ls -A "$5")" ] || die "rebuild dir is not empty: $5"
REBUILD="$(abs_dir "$5")"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
: > "$WORK/empty"

cp -a "$BASE_DIR/." "$REBUILD/"

# apply <old file> <patch rel> <patch sha256> <path>
apply() {
  local patch="$DELTA_DIR/$2" target="$REBUILD/$4"
  [ -f "$patch" ] || die "missing patch: $2"
  [ "$(sha256_of "$patch")" = "$3" ] || die "patch sha256 mismatch: $2"
  "$HPATCHZ" -f "$1" "$patch" "$WORK/out" < /dev/null > "$WORK/hpatchz.log" 2>&1 ||
    { cat "$WORK/hpatchz.log" >&2; die "hpatchz failed for $2"; }
  mkdir -p "$(dirname "$target")"
  rm -f "$target"
  mv -f "$WORK/out" "$target"
}

jq -r --arg t "$BASE_TAG" '.bases[$t].deleted[]' "$MANIFEST" |
  while IFS= read -r path; do
    rm -f "$REBUILD/$path"
    dir="$(dirname "$path")"
    while [ "$dir" != . ] && rmdir "$REBUILD/$dir" 2> /dev/null; do
      dir="$(dirname "$dir")"
    done
  done
# Patches read from the pristine base, so renamed sources and `deleted` never interfere.
jq -r --arg t "$BASE_TAG" '.bases[$t].patches[] | [.from_path, .patch, .patch_sha256, .path] | join("\u001f")' "$MANIFEST" |
  while IFS="$SEP" read -r from_path patch sha path; do
    apply "$BASE_DIR/$from_path" "$patch" "$sha" "$path"
  done
jq -r --arg t "$BASE_TAG" '.bases[$t].whole[] | [.patch, .patch_sha256, .path] | join("\u001f")' "$MANIFEST" |
  while IFS="$SEP" read -r patch sha path; do
    apply "$WORK/empty" "$patch" "$sha" "$path"
  done
jq -r '.files[] | [.path, (.symlink // ""), (.mode // "")] | join("\u001f")' "$MANIFEST" |
  while IFS="$SEP" read -r path link mode; do
    if [ -n "$link" ]; then
      mkdir -p "$(dirname "$REBUILD/$path")"
      rm -rf "${REBUILD:?}/$path"
      ln -s "$link" "$REBUILD/$path"
    elif ! is_windows; then
      chmod "$mode" "$REBUILD/$path"
    fi
  done

bash "$SCRIPT_DIR/hash-tree.sh" "$REBUILD" > "$WORK/rebuilt.json"
STRIP='.'
if is_windows; then
  STRIP='map(del(.mode))'
fi
jq "$STRIP" "$WORK/rebuilt.json" > "$WORK/got.json"
jq ".files | $STRIP" "$MANIFEST" > "$WORK/want.json"

if ! jq -e -n --slurpfile got "$WORK/got.json" --slurpfile want "$WORK/want.json" '$got[0] == $want[0]' > /dev/null; then
  jq -r -n --slurpfile got "$WORK/got.json" --slurpfile want "$WORK/want.json" '
    ($got[0] | INDEX(.path)) as $g | ($want[0] | INDEX(.path)) as $w
    | ($w | keys[] | select($g[.] == null) | "missing:    \(.)"),
      ($g | keys[] | select($w[.] == null) | "unexpected: \(.)"),
      ($w | keys[] | select($g[.] != null and $g[.] != $w[.]) | "different:  \(.)")' >&2
  die "rebuilt tree does not match the manifest for base '$BASE_TAG'"
fi
log "$BASE_TAG: rebuilt tree matches the manifest"
echo "$REBUILD"
