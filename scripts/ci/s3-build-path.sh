#!/usr/bin/env bash
# Single source of the S3 destination prefix for non-release CI build artifacts:
#
#   @dcl/<repo>/branch/<branch>/<event-prefix>-<run_number>-<short_sha>
#
# The uploader (build-unitycloud.yml) and every consumer that reconstructs the
# URL (pr-comment-artifact-url.yml, create-release-branch.yml,
# visual-regression.yml) must agree on this format - any drift silently breaks
# download links, so they all call this script instead of re-deriving it.
#
# Usage: s3-build-path.sh <event_name> <repo_name> <branch> <run_number> <sha>
set -euo pipefail

if [ "$#" -ne 5 ]; then
  echo "usage: $0 <event_name> <repo_name> <branch> <run_number> <sha>" >&2
  exit 1
fi

EVENT="$1"
REPO="$2"
BRANCH="$3"
RUN_NUMBER="$4"
SHA="$5"

case "$EVENT" in
  pull_request)      PREFIX="pr" ;;
  push)              PREFIX="pu" ;;
  merge_group)       PREFIX="mg" ;;
  workflow_dispatch) PREFIX="wd" ;;
  workflow_call)     PREFIX="wc" ;;
  schedule)          PREFIX="sc" ;;
  *)                 PREFIX="gn" ;;
esac

echo "@dcl/${REPO}/branch/${BRANCH}/${PREFIX}-${RUN_NUMBER}-${SHA:0:7}"
