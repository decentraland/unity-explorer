#!/usr/bin/env bash
# Stale-log watchdog for game-ci Unity test containers.
#
# game-ci/unity-test-runner starts the Unity editor with
# `-logFile <workspace>/artifacts/<mode>.log`, so while the step runs the
# editor log grows on the host but the step's own console stays silent. When
# the editor deadlocks mid-run the job burns the whole `timeout-minutes`
# budget with no output (run 29613116640 attempt 1: console silent from
# 21:04:21 right after the "Testing in editmode" banner until manually
# cancelled at 22:14:52 — 70 minutes).
#
# This script polls the editor log's byte size from the host and kills the
# Unity container when the log stops growing, failing the test step within
# ~STALL_THRESHOLD seconds instead. Arming rules mirror the build-phase
# watchdog in scripts/cloudbuild/build.py (LOG_STALL_THRESHOLD):
#   - the stall clock arms only after one observed size *increase* — a log
#     that never grows reads as "watchdog inactive", never as "stalled";
#   - a size *decrease* (log replaced or truncated) resets the clock.
# If the log stalls but no container matches the image, the step is already
# tearing down — the watchdog exits without reporting a stall.
#
# Usage: test-stale-log-watchdog.sh <editor-log> <container-image> <status-file>
#   <editor-log>      host path of the Unity editor log to watch
#   <container-image> image the Unity container runs (docker ancestor filter)
#   <status-file>     written (JSON) only when a stall kill actually happened
# Env overrides:
#   STALL_THRESHOLD   seconds without log growth before the kill (default 900)
#   POLL_INTERVAL     seconds between size probes (default 30)
#   MAX_LIFETIME      hard cap on the watchdog's own runtime (default 7200)
#
# Deliberately no `set -e`: the poll loop must survive transient stat/docker
# failures; every external call is individually guarded instead.
set -u

LOG_FILE=$1
IMAGE=$2
STATUS_FILE=$3
STALL_THRESHOLD=${STALL_THRESHOLD:-900}
POLL_INTERVAL=${POLL_INTERVAL:-30}
MAX_LIFETIME=${MAX_LIFETIME:-7200}

start=$(date +%s)
last_size=-1
last_growth=$start
growth_observed=false

echo "Watching $LOG_FILE for container image $IMAGE (stall threshold ${STALL_THRESHOLD}s, poll ${POLL_INTERVAL}s)"

while :; do
    sleep "$POLL_INTERVAL"
    now=$(date +%s)

    if (( now - start > MAX_LIFETIME )); then
        echo "Max lifetime (${MAX_LIFETIME}s) reached - exiting without a verdict."
        exit 0
    fi

    size=$(stat -c%s "$LOG_FILE" 2>/dev/null) || continue

    if (( last_size < 0 || size < last_size )); then
        # First observation, or the log was replaced/truncated.
        last_size=$size
        last_growth=$now
        continue
    fi

    if (( size > last_size )); then
        if [ "$growth_observed" = false ]; then
            echo "Log is growing ($size bytes) - stall detection armed."
        fi
        last_size=$size
        last_growth=$now
        growth_observed=true
        continue
    fi

    if [ "$growth_observed" = true ] && (( now - last_growth > STALL_THRESHOLD )); then
        stalled_for=$(( now - last_growth ))
        containers=$(docker ps -q --filter "ancestor=$IMAGE" 2>/dev/null || true)
        if [ -z "$containers" ]; then
            echo "Log stalled for ${stalled_for}s but no container is running $IMAGE - step is tearing down, exiting."
            exit 0
        fi
        echo "Log has not grown for ${stalled_for}s (size $size bytes). Killing Unity container(s): $containers"
        printf '{"stalledSeconds": %d, "logSizeBytes": %d, "log": "%s"}\n' \
            "$stalled_for" "$size" "$LOG_FILE" > "$STATUS_FILE"
        # container ids are newline/space separated words
        # shellcheck disable=SC2086
        docker kill $containers || true
        exit 0
    fi
done
