#!/usr/bin/env bash
# Picks Explorer release tags from a GitHub releases list (the JSON array returned by
# `gh api repos/<repo>/releases`) on stdin. Only published `v*` releases count (no drafts,
# no pre-releases, which /releases/latest never serves); Avatar Preview Renderer tags
# are ignored.
#
# Usage:
#   release-tags.sh bases <count> <current tag>   the <count> most recently published
#                                                 releases other than <current tag>: the
#                                                 delta bases
#   release-tags.sh prune <keep> <window>         releases <keep> .. <keep>+<window>-1 by
#                                                 descending version: the ones whose delta/
#                                                 folder is removed
# Prints one tag per line.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

require_tool jq
is_count() { case "$1" in ''|*[!0-9]*) return 1 ;; *) return 0 ;; esac; }

CANDIDATES='[.[] | select(.draft == false and (.prerelease | not) and (.tag_name | startswith("v")))]'
PUBLISHED="$CANDIDATES"' | sort_by(.published_at) | reverse | map(.tag_name)'
# Prune follows version order, so a late publish of an older tag cannot push a newer
# release (the rollback target) out of the kept series. A malformed v* tag fails here, before any deletion.
BY_VERSION="$CANDIDATES"' | sort_by(.tag_name | ltrimstr("v") | split("-")[0] | split(".") | map(tonumber)) | reverse | map(.tag_name)'

case "${1:-}" in
  bases)
    if [ $# -ne 3 ] || ! is_count "$2"; then die "usage: release-tags.sh bases <count> <current tag>"; fi
    jq -r --argjson n "$2" --arg current "$3" "$PUBLISHED"' | map(select(. != $current)) | .[:$n][]'
    ;;
  prune)
    if [ $# -ne 3 ] || ! is_count "$2" || ! is_count "$3"; then die "usage: release-tags.sh prune <keep> <window>"; fi
    jq -r --argjson keep "$2" --argjson window "$3" "$BY_VERSION"' | .[$keep:$keep + $window][]'
    ;;
  *)
    die "usage: release-tags.sh bases <count> <current tag> | prune <keep> <window>"
    ;;
esac
