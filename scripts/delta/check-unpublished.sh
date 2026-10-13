#!/usr/bin/env bash
# Exits 0 only when <tag> has no published GitHub release in $GITHUB_REPOSITORY: delta
# patches and manifests are immutable once a release is published.
#
# Usage: check-unpublished.sh <tag>   (needs gh, authenticated through GH_TOKEN)
#
# GET /releases/tags/<tag> returns a published release, so "unpublished" is a 404 or a
# draft. Any other failure (rate limit, 5xx, network) is an error, never "unpublished".
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ $# -eq 1 ] || die "usage: check-unpublished.sh <tag>"
[ -n "${GITHUB_REPOSITORY:-}" ] || die "GITHUB_REPOSITORY is not set"
require_tool gh

if ! DRAFT="$(gh api "repos/$GITHUB_REPOSITORY/releases/tags/$1" --jq .draft 2>&1)"; then
  case "$DRAFT" in
    *"HTTP 404"*) log "$1 has no published release"; exit 0 ;;
  esac
  echo "::error::Could not check whether $1 is published: $DRAFT"
  exit 1
fi
if [ "$DRAFT" != true ]; then
  echo "::error::$1 is published; its delta patches are immutable and are not (re)generated or uploaded."
  exit 1
fi
log "$1 is a draft release"
