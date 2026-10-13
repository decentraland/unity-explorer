#!/usr/bin/env bash
# Describes a build folder as JSON, sorted by path:
#   [{ "path", "size", "sha256", "mode" } | { "path", "symlink" }]
# Paths are relative to <dir> with forward slashes. Symlinks are listed with their
# target and not followed. Directories are not listed. Fails on any path or symlink
# target that could reach outside <dir> (absolute, `..` climbing above the root).
#
# Usage: hash-tree.sh <dir>
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ $# -eq 1 ] || die "usage: hash-tree.sh <dir>"
[ -d "$1" ] || die "not a directory: $1"
require_tool jq

# escapes_root <path> <symlink target>: true when the target, resolved lexically from the
# link's folder, climbs above the build root.
escapes_root() {
  local depth seg IFS=/
  depth=$(printf '%s' "$1" | tr -cd / | wc -c)
  set -f
  for seg in $2; do
    case "$seg" in
      ''|.) ;;
      ..) depth=$((depth - 1)); [ "$depth" -ge 0 ] || { set +f; return 0; } ;;
      *) depth=$((depth + 1)) ;;
    esac
  done
  set +f
  return 1
}

cd "$1"
ROOT_P="$(pwd -P)"
find . -mindepth 1 \( -type f -o -type l \) -print0 |
  while IFS= read -r -d '' p; do
    rel="${p#./}"
    case "$rel" in
      *$'\t'*|*$'\n'*|*$'\r'*|*"$SEP"*) die "unsupported character in path: $rel" ;;
    esac
    case "/$rel/" in */../*|*/./*|//*) die "unsafe path: $rel" ;; esac
    if [ -L "$p" ]; then
      target="$(readlink "$p")"
      case "$target" in /*|'') die "absolute or empty symlink target: $rel -> $target" ;; esac
      case "$target" in
        *$'\t'*|*$'\n'*|*$'\r'*|*"$SEP"*) die "unsupported character in symlink target: $rel" ;;
      esac
      if escapes_root "$rel" "$target"; then die "symlink leaves the build: $rel -> $target"; fi
      # The lexical check misses a climb through another symlink (d/x -> up/../.. with
      # d/up -> ..), so a resolvable target is also checked physically.
      if [ -d "$p" ]; then t_dir="$target"; else t_dir="$(dirname "$target")"; fi
      if dest="$(cd "$(dirname "$p")" && cd -P "$t_dir" 2> /dev/null && pwd -P)"; then
        case "$dest/" in "$ROOT_P"/*) ;; *) die "symlink leaves the build: $rel -> $target" ;; esac
      fi
      printf 'L\t%s\t%s\n' "$rel" "$target"
    else
      printf 'F\t%s\t%s\t%s\t%s\n' "$rel" "$(size_of "$p")" "$(sha256_of "$p")" "$(mode_of "$p")"
    fi
  done |
  jq -Rn '[inputs | split("\t")
          | if .[0] == "L" then {path: .[1], symlink: .[2]}
            else {path: .[1], size: (.[2] | tonumber), sha256: .[3], mode: .[4]} end]
          | sort_by(.path)'
