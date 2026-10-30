#!/usr/bin/env bash
# Functional tests for build-run-attempted.sh against a stubbed gh that serves
# canned /jobs and /runs responses from files — no network, no repo. Each
# scenario can hand out a different response per poll iteration (jobs.1.json,
# jobs.2.json, ...) to exercise the wait loop. Run from anywhere:
#   bash .github/actions/ci-status-comment/test-build-run-attempted.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROBE="$SCRIPT_DIR/build-run-attempted.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# --- gh stub ----------------------------------------------------------------
# Serves $SCENARIO/<kind>.<call-number>.json, falling back to the highest
# numbered file at or below the current call, so a scenario that wants one
# steady answer just writes <kind>.1.json.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
[ "$1" = api ] || { echo "gh stub: unsupported subcommand $1" >&2; exit 64; }
path="$2"
case "$path" in
  */jobs*) kind=jobs ;;
  *)       kind=run ;;
esac
counter="$SCENARIO/.calls.$kind"
n=1
[ -f "$counter" ] && n=$(( $(cat "$counter") + 1 ))
echo "$n" > "$counter"
file=""
for try in $(seq "$n" -1 1); do
  if [ -f "$SCENARIO/$kind.$try.json" ]; then file="$SCENARIO/$kind.$try.json"; break; fi
done
[ -n "$file" ] || { echo "gh stub: no canned $kind response for call $n" >&2; exit 65; }
if [ "${3:-}" = "--jq" ]; then jq -r "$4" < "$file"; else cat "$file"; fi
STUB
chmod +x "$WORK/bin/gh"
export PATH="$WORK/bin:$PATH"

# --- helpers ----------------------------------------------------------------
FAILURES=0
PASSES=0

jobs_json() { # jobs_json '<name>:<status>:<conclusion>' ...
  local out='{"jobs":[' first=1 spec name status conclusion
  for spec in "$@"; do
    IFS=: read -r name status conclusion <<< "$spec"
    [ $first -eq 1 ] || out+=','
    first=0
    if [ -z "$conclusion" ]; then
      out+="{\"name\":\"$name\",\"status\":\"$status\",\"conclusion\":null}"
    else
      out+="{\"name\":\"$name\",\"status\":\"$status\",\"conclusion\":\"$conclusion\"}"
    fi
  done
  echo "$out]}"
}

new_scenario() {
  SCENARIO="$WORK/$1"
  mkdir -p "$SCENARIO"
  export SCENARIO
}

# expect <name> <expected-stdout> <expected-exit>
expect() {
  local name="$1" want="$2" want_code="$3" got code=0
  got=$(RUN_ID=42 OWNER=o REPO=r WAIT_SECONDS="${WAIT:-0}" POLL_SECONDS=0 \
        bash "$PROBE" 2>"$SCENARIO/stderr") || code=$?
  if [ "$got" = "$want" ] && [ "$code" -eq "$want_code" ]; then
    echo "  ok   $name"
    PASSES=$((PASSES + 1))
  else
    echo "  FAIL $name: want stdout='$want' exit=$want_code, got stdout='$got' exit=$code"
    sed 's/^/         /' "$SCENARIO/stderr"
    FAILURES=$((FAILURES + 1))
  fi
}

echo "test-build-run-attempted.sh"

# 1. The reported bug: a label event filtered prebuild out. PRs 9999 / 9929.
new_scenario skipped-prebuild
jobs_json 'Prebuild:completed:skipped' 'Build:completed:skipped' \
  'Build Gate (Windows + macOS):completed:success' > "$SCENARIO/jobs.1.json"
expect "prebuild skipped => false" false 0

# 2. A real run.
new_scenario real-run
jobs_json 'Prebuild:completed:success' 'Build (macos):completed:success' \
  'Build (windows64):completed:success' > "$SCENARIO/jobs.1.json"
expect "prebuild succeeded => true" true 0

# 3. Prebuild itself failed — a real attempt, so the section must be updated.
new_scenario failed-prebuild
jobs_json 'Prebuild:completed:failure' > "$SCENARIO/jobs.1.json"
expect "prebuild failed => true" true 0

# 4. Cancelled by concurrency before any job started. PR 9955's label run.
new_scenario cancelled-while-queued
echo '{"jobs":[]}' > "$SCENARIO/jobs.1.json"
echo '{"status":"completed"}' > "$SCENARIO/run.1.json"
expect "no jobs + run completed => false" false 0

# 5. Asked before the run started: no jobs yet, then prebuild picks up.
new_scenario waits-for-start
echo '{"jobs":[]}' > "$SCENARIO/jobs.1.json"
echo '{"status":"queued"}' > "$SCENARIO/run.1.json"
jobs_json 'Prebuild:in_progress:' > "$SCENARIO/jobs.2.json"
WAIT=60 expect "waits through empty job list => true" true 0

# 6. Asked before the run started, and it turns out to be a label no-op.
new_scenario waits-then-skipped
jobs_json 'Prebuild:queued:' > "$SCENARIO/jobs.1.json"
jobs_json 'Prebuild:completed:skipped' > "$SCENARIO/jobs.2.json"
WAIT=60 expect "waits through queued prebuild => false" false 0

# 7. Drift guard: prebuild renamed in build-unitycloud.yml.
new_scenario renamed-prebuild
jobs_json 'Preflight:completed:success' > "$SCENARIO/jobs.1.json"
echo '{"status":"completed"}' > "$SCENARIO/run.1.json"
expect "jobs but no Prebuild => hard error" "" 2

# 8. Out of wait budget with prebuild still queued: keep the old behaviour.
new_scenario queued-past-deadline
jobs_json 'Prebuild:queued:' > "$SCENARIO/jobs.1.json"
expect "queued past deadline => true" true 0

echo
if [ "$FAILURES" -gt 0 ]; then
  echo "$FAILURES failed, $PASSES passed"
  exit 1
fi
echo "all $PASSES passed"
