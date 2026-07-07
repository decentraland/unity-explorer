#!/usr/bin/env bash
# Re-runs the most recent failed pull_request run of the "Enforce QA and DEV
# Approvals" workflow for HEAD_SHA, so an approval check that went red before
# new approvals/labels arrived turns green without waiting for a push.
# Skipped when more than one run for the SHA is already in progress (the
# caller may itself be one of them).
#
# Env: GH_TOKEN, REPO (owner/name), HEAD_SHA
set -euo pipefail

WORKFLOW_NAME="Enforce QA and DEV Approvals"

echo "Searching '$WORKFLOW_NAME' runs for SHA $HEAD_SHA..."
RUNS_JSON=$(gh run list \
  --workflow="$WORKFLOW_NAME" \
  --limit 1000 \
  --repo "$REPO" \
  --json databaseId,event,headSha,conclusion,status)

IN_PROGRESS_COUNT=$(jq -r --arg sha "$HEAD_SHA" '
  map(select(.headSha == $sha and .status == "in_progress")) | length
' <<< "$RUNS_JSON")

echo "In-progress runs for SHA $HEAD_SHA: $IN_PROGRESS_COUNT"
if [ "$IN_PROGRESS_COUNT" -gt 1 ]; then
  echo "More than 1 in-progress run detected for this SHA, skipping rerun."
  exit 0
fi

WORKFLOW_RUN_ID=$(jq -r --arg sha "$HEAD_SHA" '
  [ .[] |
    select(.event == "pull_request") |
    select(.headSha == $sha) |
    select(.conclusion == "failure") ] |
  .[0].databaseId // empty
' <<< "$RUNS_JSON")

if [ -z "$WORKFLOW_RUN_ID" ]; then
  echo "No failed run found for this commit ($HEAD_SHA)."
  exit 0
fi

echo "Re-running failed run: $WORKFLOW_RUN_ID"
gh run rerun "$WORKFLOW_RUN_ID" --repo "$REPO"
echo "Re-run triggered successfully."
