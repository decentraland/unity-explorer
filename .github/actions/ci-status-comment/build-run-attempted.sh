#!/usr/bin/env bash
# Answers one question for pr-comment-artifact-url.yml: did this "Unity Cloud
# Build" run actually attempt a build, or was it a deliberate no-op?
#
# build-unitycloud.yml subscribes to pull_request `labeled`/`unlabeled` because
# four labels (force-build, clean-build, windows-only, macos-only) re-trigger a
# build. GitHub cannot filter label events by name in `on:`, so *every* label
# add/remove starts a run; the prebuild job's `if` then filters it out and the
# run finishes `success` with nothing built. The same shape happens for a draft
# without force-build and for the perf_test label.
#
# Such a run says nothing about the build, so the comment's build section must
# be left exactly as the last real run left it. Without this probe the no-op run
# reads as "a build ran and produced no artifacts" and overwrites a green build
# (download links included) with "Build skipped - no changes detected".
#
# The signal is the Prebuild job: `skipped` means the run was filtered out.
# Absent with no jobs at all means the run was cancelled by concurrency before
# any job materialised - also a no-op.
#
# Prints `true` (a build was attempted; the section may be written) or `false`
# (no-op; leave the section alone) on stdout.
#
# Endpoints omit the leading slash so Git Bash (MSYS) does not rewrite them into
# Windows filesystem paths when the script is run locally.
#
# Usage: RUN_ID=<id> OWNER=<owner> REPO=<repo> [WAIT_SECONDS=300] [POLL_SECONDS=5] \
#          bash build-run-attempted.sh
#
# WAIT_SECONDS is for the `requested` caller, which asks before the run has
# started: 0 (the default) answers from the run's current state, which is all a
# `completed` caller needs.
set -euo pipefail

: "${RUN_ID:?RUN_ID is required}"
: "${OWNER:?OWNER is required}"
: "${REPO:?REPO is required}"
WAIT_SECONDS="${WAIT_SECONDS:-0}"
POLL_SECONDS="${POLL_SECONDS:-5}"

# The job that decides whether anything gets built. Renaming it in
# build-unitycloud.yml without updating this breaks the probe, so a completed
# run that has jobs but not this one is a hard error rather than a silent
# "no-op" that would freeze the build section forever.
PREBUILD_JOB_NAME='Prebuild'

log() { echo "[build-run-attempted] $*" >&2; }

deadline=$(( $(date +%s) + WAIT_SECONDS ))

while :; do
  jobs_json=$(gh api "repos/$OWNER/$REPO/actions/runs/$RUN_ID/jobs?per_page=100")

  prebuild=$(jq -c --arg name "$PREBUILD_JOB_NAME" \
    'first(.jobs[] | select(.name == $name)) // empty' <<< "$jobs_json")

  if [ -n "$prebuild" ]; then
    status=$(jq -r '.status' <<< "$prebuild")
    conclusion=$(jq -r '.conclusion // ""' <<< "$prebuild")
    log "$PREBUILD_JOB_NAME: status=$status conclusion=${conclusion:-null}"

    if [ "$conclusion" = "skipped" ]; then
      log "prebuild was filtered out (label/draft/perf_test event) - no build attempted"
      echo false
      exit 0
    fi

    # Anything past `queued` means the build decision is being made for real.
    if [ "$status" != "queued" ]; then
      echo true
      exit 0
    fi
  else
    job_count=$(jq '.jobs | length' <<< "$jobs_json")
    run_status=$(gh api "repos/$OWNER/$REPO/actions/runs/$RUN_ID" --jq '.status')
    log "no $PREBUILD_JOB_NAME job yet (jobs=$job_count, run status=$run_status)"

    if [ "$job_count" -gt 0 ]; then
      names=$(jq -r '[.jobs[].name] | join(", ")' <<< "$jobs_json")
      echo "::error::Run $RUN_ID has jobs but none named '$PREBUILD_JOB_NAME' (found: $names). Update PREBUILD_JOB_NAME in $(basename "${BASH_SOURCE[0]}") to match build-unitycloud.yml." >&2
      exit 2
    fi

    if [ "$run_status" = "completed" ]; then
      log "run finished without starting a job (cancelled while queued) - no build attempted"
      echo false
      exit 0
    fi
  fi

  now=$(date +%s)
  if [ "$now" -ge "$deadline" ]; then
    # Still queued past the wait budget. Assume a build is coming, which keeps
    # the pre-probe behaviour (reset the section to Pending) for this case.
    log "still queued after ${WAIT_SECONDS}s - assuming a build is coming"
    echo true
    exit 0
  fi

  sleep "$POLL_SECONDS"
done
