#!/usr/bin/env bash
# Describes a build folder as JSON, sorted by path:
#   [{ "path", "size", "sha256", "mode" } | { "path", "symlink" }]
# Paths are relative to <dir> with forward slashes. Symlinks are listed with their
# target and not followed. Directories are not listed.
#
# Usage: hash-tree.sh <dir>
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ $# -eq 1 ] || die "usage: hash-tree.sh <dir>"
[ -d "$1" ] || die "not a directory: $1"
require_tool jq

cd "$1"
find . -mindepth 1 \( -type f -o -type l \) -print0 |
  while IFS= read -r -d '' p; do
    rel="${p#./}"
    case "$rel" in
      *$'\t'*|*$'\n'*|*$'\r'*|*"$SEP"*) die "unsupported character in path: $rel" ;;
    esac
    if [ -L "$p" ]; then
      printf 'L\t%s\t%s\n' "$rel" "$(readlink "$p")"
    else
      printf 'F\t%s\t%s\t%s\t%s\n' "$rel" "$(size_of "$p")" "$(sha256_of "$p")" "$(mode_of "$p")"
    fi
  done |
  jq -Rn '[inputs | split("\t")
          | if .[0] == "L" then {path: .[1], symlink: .[2]}
            else {path: .[1], size: (.[2] | tonumber), sha256: .[3], mode: .[4]} end]
          | sort_by(.path)'
