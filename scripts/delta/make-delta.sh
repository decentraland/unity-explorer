#!/usr/bin/env bash
# Produces the patches that turn one base build into the new build.
#
# Usage: make-delta.sh <base dir> <new dir> <base tag> <out dir>
#
# Writes:
#   <out dir>/from/<base tag>/<path>.hdiff  one HDiffPatch patch per changed or new file
#   <out dir>/base-<base tag>.json          { base, deleted[], patches[], whole[] }
# and a size summary on stderr.
#
# - patches[]: regular files that changed, patched from `from_path` in the base. A bundle
#   renamed by its content hash (<name>_<32 hex>.bundle) is paired with the base bundle
#   of the same <name>, so `from_path` differs from `path`.
# - whole[]: files with no base counterpart, and files whose patch is not smaller than
#   the file encoded on its own. They are HDiffPatch patches against an empty file, so
#   applying them needs only hpatchz.
# - deleted[]: base paths (files or symlinks) absent from the new build.
# Every patch is applied back with hpatchz and must reproduce the new file's sha256.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/lib.sh"

[ $# -eq 4 ] || die "usage: make-delta.sh <base dir> <new dir> <base tag> <out dir>"
BASE_TAG="$3"
[ -d "$1" ] || die "base dir not found: $1"
[ -d "$2" ] || die "new dir not found: $2"
case "$BASE_TAG" in ''|*/*|.*) die "invalid base tag: '$BASE_TAG'" ;; esac
require_tool jq "$HDIFFZ" "$HPATCHZ"

BASE_DIR="$(abs_dir "$1")"
NEW_DIR="$(abs_dir "$2")"
mkdir -p "$4"
OUT_DIR="$(abs_dir "$4")"
PATCH_REL="from/$BASE_TAG"
rm -rf "${OUT_DIR:?}/$PATCH_REL"
mkdir -p "$OUT_DIR/$PATCH_REL"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
: > "$WORK/empty"

bash "$SCRIPT_DIR/hash-tree.sh" "$BASE_DIR" > "$WORK/base.json"
bash "$SCRIPT_DIR/hash-tree.sh" "$NEW_DIR" > "$WORK/new.json"

# One record per regular file of the new build that needs data: kind, from_path, path.
jq -r --slurpfile base "$WORK/base.json" '
  def bundle_key: sub("_[0-9a-f]{32}\\.bundle$"; "");
  def is_bundle: test("_[0-9a-f]{32}\\.bundle$");
  ($base[0] | map(select(.sha256))) as $b
  | ($b | INDEX(.path)) as $bm
  | (map(select(.sha256)) | INDEX(.path)) as $nm
  | [$b[] | select($nm[.path] == null)] as $gone
  | .[] | select(.sha256) as $n
  | if $bm[$n.path] then
      if $bm[$n.path].sha256 == $n.sha256 then empty else ["patch", $n.path, $n.path] end
    elif ($n.path | is_bundle) then
      [$gone[] | select((.path | is_bundle) and (.path | bundle_key) == ($n.path | bundle_key))] as $c
      | if ($c | length) == 1 then ["patch", $c[0].path, $n.path] else ["whole", "", $n.path] end
    else ["whole", "", $n.path] end
  | join("\u001f")' "$WORK/new.json" > "$WORK/work.txt"

# hdiff_checked <old> <new> <patch>: create the patch and prove it reproduces <new>.
hdiff_checked() {
  # shellcheck disable=SC2086 # HDIFFZ_ARGS is a list of flags
  "$HDIFFZ" -f $HDIFFZ_ARGS "$1" "$2" "$3" < /dev/null > "$WORK/hdiffz.log" 2>&1 ||
    { cat "$WORK/hdiffz.log" >&2; die "hdiffz failed for $2"; }
  "$HPATCHZ" -f "$1" "$3" "$WORK/roundtrip" < /dev/null > "$WORK/hpatchz.log" 2>&1 ||
    { cat "$WORK/hpatchz.log" >&2; die "hpatchz failed for $3"; }
  [ "$(sha256_of "$WORK/roundtrip")" = "$(sha256_of "$2")" ] || die "patch round-trip mismatch for $2"
  rm -f "$WORK/roundtrip"
}

: > "$WORK/entries.tsv"
while IFS="$SEP" read -r kind from_path path; do
  new_file="$NEW_DIR/$path"
  patch_rel="$PATCH_REL/$path.hdiff"
  patch_file="$OUT_DIR/$patch_rel"
  mkdir -p "$(dirname "$patch_file")"
  new_size="$(size_of "$new_file")"

  if [ "$kind" = patch ]; then
    hdiff_checked "$BASE_DIR/$from_path" "$new_file" "$patch_file"
    # A patch that is at least half the file's size may lose to encoding the file alone.
    if [ $(( $(size_of "$patch_file") * 2 )) -ge "$new_size" ]; then
      hdiff_checked "$WORK/empty" "$new_file" "$WORK/whole.hdiff"
      if [ "$(size_of "$WORK/whole.hdiff")" -lt "$(size_of "$patch_file")" ]; then
        mv -f "$WORK/whole.hdiff" "$patch_file"
        kind=whole
      fi
    fi
  else
    hdiff_checked "$WORK/empty" "$new_file" "$patch_file"
  fi

  from_sha=""
  [ "$kind" = patch ] && from_sha="$(sha256_of "$BASE_DIR/$from_path")"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$kind" "$path" "$from_path" "$from_sha" \
    "$patch_rel" "$(size_of "$patch_file")" "$(sha256_of "$patch_file")" "$new_size" >> "$WORK/entries.tsv"
done < "$WORK/work.txt"

jq -Rn --arg base "$BASE_TAG" --slurpfile b "$WORK/base.json" --slurpfile n "$WORK/new.json" '
  [inputs | split("\t")] as $rows
  | ($n[0] | map(.path) | INDEX(.)) as $new_paths
  | {
      base: $base,
      deleted: [$b[0][] | select($new_paths[.path] == null) | .path],
      patches: [$rows[] | select(.[0] == "patch")
                | {path: .[1], from_path: .[2], from_sha256: .[3],
                   patch: .[4], patch_size: (.[5] | tonumber), patch_sha256: .[6]}],
      whole: [$rows[] | select(.[0] == "whole")
              | {path: .[1], patch: .[4], patch_size: (.[5] | tonumber), patch_sha256: .[6]}]
    }' < "$WORK/entries.tsv" > "$OUT_DIR/base-$BASE_TAG.json"

jq -r --slurpfile n "$WORK/new.json" '
  "[delta] \(.base): \(.patches | length) patched, \(.whole | length) whole, \(.deleted | length) deleted; "
  + "download \([.patches[], .whole[] | .patch_size] | add // 0) bytes, "
  + "new build \([$n[0][] | .size // 0] | add // 0) bytes"' "$OUT_DIR/base-$BASE_TAG.json" >&2
