#!/usr/bin/env bash
# Functional tests for hash-tree.sh, make-delta.sh, assemble-manifest.sh and
# verify-delta.sh on small synthetic builds. Needs hdiffz/hpatchz on PATH
# (bash scripts/delta/fetch-tools.sh <dir>). Run from anywhere:
#   bash scripts/delta/test-delta.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/lib.sh"
require_tool jq "$HDIFFZ" "$HPATCHZ"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

FAILURES=0
PASSES=0
ok()   { echo "  ok   $1"; PASSES=$((PASSES + 1)); }
fail() { echo "  FAIL $1"; FAILURES=$((FAILURES + 1)); }
check() { # check <name> <command...>
  local name="$1"; shift
  if "$@" > "$WORK/check.log" 2>&1; then ok "$name"; else fail "$name"; sed 's/^/       /' "$WORK/check.log"; fi
}
check_fails() { # check_fails <name> <command...>
  local name="$1"; shift
  if "$@" > "$WORK/check.log" 2>&1; then fail "$name (expected failure)"; else ok "$name"; fi
}
jq_true() { jq -e "$1" "$2" > /dev/null; } # jq_true <filter> <file>

random_file() { head -c "$2" /dev/urandom > "$1"; } # random_file <path> <bytes>
poke() { printf '%s' "$3" | dd of="$1" bs=1 seek="$2" conv=notrunc 2>/dev/null; } # poke <file> <offset> <bytes>

OLD_BUNDLE=ui_assets_all_0123456789abcdef0123456789abcdef.bundle
NEW_BUNDLE=ui_assets_all_fedcba9876543210fedcba9876543210.bundle

# --- fixtures ---------------------------------------------------------------
BASE="$WORK/base"
NEW="$WORK/new"
mkdir -p "$BASE/Data/aa" "$BASE/Data/space dir"
echo "unchanged" > "$BASE/same.txt"
random_file "$BASE/Game.dll" 262144
random_file "$BASE/Data/aa/$OLD_BUNDLE" 131072
echo "removed in new" > "$BASE/gone.txt"
echo "a=1" > "$BASE/Data/boot.config"
random_file "$BASE/Data/catalog.bin" 4096
random_file "$BASE/Data/space dir/asset file.bin" 65536
printf '#!/bin/sh\necho hi\n' > "$BASE/run.sh"
echo "old casing" > "$BASE/Plugin.dll"

cp -a "$BASE" "$NEW"
# Case-only rename: on NTFS/APFS deleting the old name after writing the new one loses the file.
rm -f "$NEW/Plugin.dll"
echo "new casing" > "$NEW/plugin.dll"
rm -f "$NEW/gone.txt"
poke "$NEW/Game.dll" 1000 "PATCHED-CODE"
poke "$NEW/Game.dll" 200000 "MORE"
mv "$NEW/Data/aa/$OLD_BUNDLE" "$NEW/Data/aa/$NEW_BUNDLE"
poke "$NEW/Data/aa/$NEW_BUNDLE" 5000 "BUNDLE-CHANGE"
echo "a=2" > "$NEW/Data/boot.config"
random_file "$NEW/Data/catalog.bin" 4096
poke "$NEW/Data/space dir/asset file.bin" 10 "X"
echo "brand new" > "$NEW/added.txt"
SYMLINKS=0
if ! is_windows; then
  chmod 644 "$BASE/run.sh"
  chmod 755 "$NEW/run.sh"
  ln -s same.txt "$NEW/link.txt"
  # A real directory that becomes a symlink (.framework Versions/Current) and the reverse.
  mkdir -p "$BASE/Fw/Versions/A" "$BASE/Fw/Versions/Current" "$NEW/Fw/Versions/A" "$NEW/Lib"
  echo "lib a" > "$BASE/Fw/Versions/A/lib"
  echo "lib a" > "$NEW/Fw/Versions/A/lib"
  echo "lib current" > "$BASE/Fw/Versions/Current/lib"
  ln -s A "$NEW/Fw/Versions/Current"
  ln -s Fw/Versions/A "$BASE/Lib"
  echo "lib dir" > "$NEW/Lib/lib"
  SYMLINKS=1
fi
random_file "$WORK/Decentraland_test.zip" 1024

# --- hash-tree.sh -----------------------------------------------------------
echo "hash-tree.sh"
bash "$SCRIPT_DIR/hash-tree.sh" "$NEW" > "$WORK/tree.json"
check "lists every file sorted by path" jq_true '(map(.path) == (map(.path) | sort)) and length >= 7' "$WORK/tree.json"
check "paths are relative with forward slashes" \
  jq_true 'any(.[]; .path == "Data/space dir/asset file.bin") and all(.[]; (.path | startswith("/") | not))' "$WORK/tree.json"
check "records size, sha256 and mode" \
  jq_true '.[] | select(.path == "same.txt") | .size == 10 and (.sha256 | length) == 64 and (.mode | test("^[0-7]{3,4}$"))' "$WORK/tree.json"
if [ $SYMLINKS -eq 1 ]; then
  check "records symlinks with their target" jq_true 'any(.[]; .path == "link.txt" and .symlink == "same.txt" and (has("sha256") | not))' "$WORK/tree.json"
  check "records the executable bit" jq_true '.[] | select(.path == "run.sh") | .mode == "755"' "$WORK/tree.json"
fi
bash "$SCRIPT_DIR/hash-tree.sh" "$NEW" > "$WORK/tree2.json"
check "is deterministic" cmp -s "$WORK/tree.json" "$WORK/tree2.json"

# --- make-delta.sh ----------------------------------------------------------
echo "make-delta.sh"
OUT="$WORK/out"
check "succeeds" bash "$SCRIPT_DIR/make-delta.sh" "$BASE" "$NEW" v1.0.0-alpha "$OUT"
B="$OUT/base-v1.0.0-alpha.json"
check "writes base json with the tag" jq_true '.base == "v1.0.0-alpha"' "$B"
check "patches a changed file" jq_true 'any(.patches[]; .path == "Game.dll" and .from_path == "Game.dll")' "$B"
check "pairs the renamed bundle with its old name" \
  jq_true "any(.patches[]; .path == \"Data/aa/$NEW_BUNDLE\" and .from_path == \"Data/aa/$OLD_BUNDLE\")" "$B"
check "handles paths with spaces" jq_true 'any(.patches[], .whole[]; .path == "Data/space dir/asset file.bin")' "$B"
check "ships a new file whole" jq_true 'any(.whole[]; .path == "added.txt")' "$B"
check "ships a fully rewritten file whole" jq_true 'any(.whole[]; .path == "Data/catalog.bin")' "$B"
check "keeps a small edit as a patch when whole is not smaller" jq_true 'any(.patches[]; .path == "Data/boot.config")' "$B"
check "lists deleted and renamed-away paths" \
  jq_true "[\"Data/aa/$OLD_BUNDLE\", \"gone.txt\", \"Plugin.dll\"] - .deleted == []" "$B"
check "ships a case-only rename whole" jq_true 'any(.whole[]; .path == "plugin.dll")' "$B"
check "skips unchanged files" jq_true 'all(.patches[], .whole[]; .path != "same.txt")' "$B"
check "records from_sha256 of the base file" \
  jq_true ".patches[] | select(.path == \"Game.dll\") | .from_sha256 == \"$(sha256_of "$BASE/Game.dll")\"" "$B"
check "writes patches under from/<tag>/<path>.hdiff" test -f "$OUT/from/v1.0.0-alpha/Game.dll.hdiff"
check "records patch size and sha256" jq_true \
  ".patches[] | select(.path == \"Game.dll\") | .patch_size == $(size_of "$OUT/from/v1.0.0-alpha/Game.dll.hdiff") and .patch_sha256 == \"$(sha256_of "$OUT/from/v1.0.0-alpha/Game.dll.hdiff")\"" "$B"
check "patch for a small change is much smaller than the file" \
  jq_true '.patches[] | select(.path == "Game.dll") | .patch_size < 26214' "$B"
check_fails "rejects a tag containing a slash" bash "$SCRIPT_DIR/make-delta.sh" "$BASE" "$NEW" "a/b" "$WORK/bad"

# --- assemble-manifest.sh ---------------------------------------------------
echo "assemble-manifest.sh"
M="$OUT/manifest.json"
check "succeeds" bash -c "bash '$SCRIPT_DIR/assemble-manifest.sh' v1.1.0-alpha windows64 '$NEW' '$WORK/Decentraland_test.zip' '$B' > '$M'"
check "has version, platform and zip" \
  jq_true ".version == \"v1.1.0-alpha\" and .platform == \"windows64\" and .zip.path == \"Decentraland_test.zip\" and .zip.size == 1024 and .zip.sha256 == \"$(sha256_of "$WORK/Decentraland_test.zip")\"" "$M"
check "files equal hash-tree of the new build" bash -c "jq -e --slurpfile t '$WORK/tree.json' '.files == \$t[0]' '$M' > /dev/null"
check "keys bases by tag without the base field" jq_true '.bases | keys == ["v1.0.0-alpha"] and (.["v1.0.0-alpha"] | has("base") | not)' "$M"
bash "$SCRIPT_DIR/assemble-manifest.sh" v1.1.0-alpha windows64 "$NEW" "$WORK/Decentraland_test.zip" < /dev/null > "$WORK/nobases.json"
check "with no bases yields an empty bases object" jq_true '.bases == {}' "$WORK/nobases.json"

# --- verify-delta.sh --------------------------------------------------------
echo "verify-delta.sh"
check "rebuilds a tree identical to the new build" \
  bash "$SCRIPT_DIR/verify-delta.sh" "$BASE" "$OUT" "$M" v1.0.0-alpha "$WORK/rebuild"
check "prints the rebuild dir" bash -c "[ -f '$WORK/rebuild/added.txt' ] && [ ! -e '$WORK/rebuild/gone.txt' ] && [ ! -e '$WORK/rebuild/Data/aa/$OLD_BUNDLE' ]"
if [ $SYMLINKS -eq 1 ]; then
  check "restores symlinks and modes" bash -c "[ -L '$WORK/rebuild/link.txt' ] && [ -x '$WORK/rebuild/run.sh' ]"
  check "replaces a directory with a symlink without touching its target" \
    bash -c "[ -L '$WORK/rebuild/Fw/Versions/Current' ] && [ \"\$(cat '$WORK/rebuild/Fw/Versions/A/lib')\" = 'lib a' ]"
  check "replaces a symlink with a directory without writing through it" \
    bash -c "[ ! -L '$WORK/rebuild/Lib' ] && [ -f '$WORK/rebuild/Lib/lib' ] && [ \"\$(cat '$WORK/rebuild/Fw/Versions/A/lib')\" = 'lib a' ]"
fi
check_fails "refuses a non-empty rebuild dir" \
  bash "$SCRIPT_DIR/verify-delta.sh" "$BASE" "$OUT" "$M" v1.0.0-alpha "$WORK/rebuild"
check_fails "fails for an unknown base tag" \
  bash "$SCRIPT_DIR/verify-delta.sh" "$BASE" "$OUT" "$M" v0.9.0-alpha "$WORK/rebuild-unknown"

cp -a "$OUT" "$WORK/out-tampered"
poke "$WORK/out-tampered/from/v1.0.0-alpha/Game.dll.hdiff" 40 "ZZZZ"
check_fails "fails when a patch was tampered with" \
  bash "$SCRIPT_DIR/verify-delta.sh" "$BASE" "$WORK/out-tampered" "$M" v1.0.0-alpha "$WORK/rebuild-tampered"

cp -a "$BASE" "$WORK/base-modified"
poke "$WORK/base-modified/Game.dll" 5 "LOCAL-EDIT"
check_fails "fails when the base differs from the one the patches were made from" \
  bash "$SCRIPT_DIR/verify-delta.sh" "$WORK/base-modified" "$OUT" "$M" v1.0.0-alpha "$WORK/rebuild-modified"

echo
echo "$PASSES passed, $FAILURES failed"
[ "$FAILURES" -eq 0 ]
