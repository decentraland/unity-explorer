#!/usr/bin/env bash
# Answers one question for the pr-comment-* writers: which PR does this
# "Unity Cloud Build" / "Unity Test" workflow_run report into?
#
# For a PR-triggered run the payload's pull_requests[0] is the answer. A push
# run on dev carries no PR, but its head SHA can be the head of an open release
# or hotfix PR: create-release-branch.yml cuts release/* from dev's tip and opens
# the PR with GITHUB_TOKEN, whose events start no runs — so when the cut lands
# while dev is still building or testing that commit, the dev run is the only
# run the release PR will ever get for it. Such a run resolves to that PR, and
# its result back-fills the seeded "on dev" placeholder in the PR's CI status
# comment. Matched on the exact SHA, restricted to same-repo release/hotfix
# heads into main: a fork may name a branch release/* too, and a feature branch
# freshly created from dev shares its SHA without being a release.
#
# Prints GITHUB_OUTPUT lines on stdout:
#   pr-number   the PR to write to, or empty when the run belongs to none
#   head-ref    the PR's head branch
#   head-sha    the commit the run built or tested
#   release-pr  true when the PR is a release/* or hotfix/* branch into main
#   backfill    true when the PR was found through the dev-push lookup
#
# A failed `gh pr list` resolves to no PR with a warning rather than failing the
# caller: the pr-comment-* writers must never red a run over a lookup blip.
#
# Usage: WORKFLOW_RUN_EVENT_OBJ='<json>' REPO=owner/name \
#          bash resolve-comment-pr.sh >> "$GITHUB_OUTPUT"
set -euo pipefail

: "${WORKFLOW_RUN_EVENT_OBJ:?WORKFLOW_RUN_EVENT_OBJ is required}"
: "${REPO:?REPO is required}"

field() { jq -r "$1 // \"\"" <<< "$WORKFLOW_RUN_EVENT_OBJ"; }

number=$(field '.pull_requests[0].number')
head_ref=""
head_sha=""
base_ref=""
backfill=false

if [ -n "$number" ]; then
  head_ref=$(field '.pull_requests[0].head.ref')
  head_sha=$(field '.pull_requests[0].head.sha // .head_sha')
  base_ref=$(field '.pull_requests[0].base.ref')
  # A fork's release/* branch is not a ref of this repo and cannot be
  # dispatched against; a head repo the payload does not name is ours.
  if [ "$(field '.pull_requests[0].head.repo.id')" != "$(field '.pull_requests[0].base.repo.id')" ]; then
    base_ref=""
  fi
else
  event=$(field '.event')
  branch=$(field '.head_branch')
  sha=$(field '.head_sha')
  if [ "$event" = "push" ] && [ "$branch" = "dev" ] && [ -n "$sha" ]; then
    if prs=$(gh pr list --repo "$REPO" --base main --state open --limit 50 \
        --json number,headRefName,headRefOid,isCrossRepository); then
      match=$(jq -c --arg sha "$sha" '
        first(.[] | select(
          .headRefOid == $sha and .isCrossRepository == false and
          ((.headRefName | startswith("release/")) or (.headRefName | startswith("hotfix/")))
        )) // empty' <<< "$prs")
      if [ -n "$match" ]; then
        number=$(jq -r '.number' <<< "$match")
        head_ref=$(jq -r '.headRefName' <<< "$match")
        head_sha="$sha"
        base_ref=main
        backfill=true
        echo "::notice::dev run of ${sha:0:7} is the head of release PR #${number} (${head_ref}); its result reports there." >&2
      fi
    else
      echo "::warning::Could not list open PRs into main to match dev run of ${sha:0:7}; reporting nowhere." >&2
    fi
  fi
fi

release_pr=false
if [ "$base_ref" = "main" ]; then
  case "$head_ref" in
    release/*|hotfix/*) release_pr=true ;;
  esac
fi

echo "pr-number=${number}"
echo "head-ref=${head_ref}"
echo "head-sha=${head_sha}"
echo "release-pr=${release_pr}"
echo "backfill=${backfill}"
