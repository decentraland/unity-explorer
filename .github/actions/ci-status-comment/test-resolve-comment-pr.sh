#!/usr/bin/env bash
# Functional tests for resolve-comment-pr.sh against a stubbed gh that serves a
# canned `gh pr list` response from a file — no network, no repo. Run from
# anywhere:
#   bash .github/actions/ci-status-comment/test-resolve-comment-pr.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESOLVER="$SCRIPT_DIR/resolve-comment-pr.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# --- gh stub ----------------------------------------------------------------
# `gh pr list` serves $SCENARIO/prs.json, fails when $SCENARIO/fail exists, and
# records that it was called so a scenario can assert it was not.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
[ "$1" = pr ] && [ "$2" = list ] || { echo "gh stub: unsupported command $*" >&2; exit 64; }
touch "$SCENARIO/.called"
[ -f "$SCENARIO/fail" ] && { echo "gh stub: HTTP 502" >&2; exit 1; }
cat "$SCENARIO/prs.json"
STUB
chmod +x "$WORK/bin/gh"
export PATH="$WORK/bin:$PATH"

# --- helpers ----------------------------------------------------------------
FAILURES=0
PASSES=0
SHA=1111111111111111111111111111111111111111
OTHER=2222222222222222222222222222222222222222

pr_run() { # pr_run <number> <head-ref> <base-ref> [head-repo-id]
  jq -nc --argjson n "$1" --arg h "$2" --arg b "$3" --argjson repo "${4:-7}" --arg sha "$SHA" \
    '{event:"pull_request", head_branch:$h, head_sha:$sha,
      pull_requests:[{number:$n, head:{ref:$h, sha:$sha, repo:{id:$repo}}, base:{ref:$b, repo:{id:7}}}]}'
}
push_run() { # push_run <branch> <sha>
  jq -nc --arg b "$1" --arg sha "$2" '{event:"push", head_branch:$b, head_sha:$sha, pull_requests:[]}'
}
pr_list() { # pr_list '<number>:<headRefName>:<headRefOid>:<isCrossRepository>' ...
  local out='[' first=1 spec n ref oid cross
  for spec in "$@"; do
    IFS=: read -r n ref oid cross <<< "$spec"
    [ $first -eq 1 ] || out+=','
    first=0
    out+="{\"number\":$n,\"headRefName\":\"$ref\",\"headRefOid\":\"$oid\",\"isCrossRepository\":$cross}"
  done
  echo "$out]"
}

new_scenario() {
  SCENARIO="$WORK/$1"
  mkdir -p "$SCENARIO"
  export SCENARIO
}

# expect <name> <event-json> <expected-stdout>
expect() {
  local name="$1" event="$2" want="$3" got code=0
  got=$(WORKFLOW_RUN_EVENT_OBJ="$event" REPO=o/r bash "$RESOLVER" 2>"$SCENARIO/stderr") || code=$?
  if [ "$got" = "$want" ] && [ "$code" -eq 0 ]; then
    echo "  ok   $name"
    PASSES=$((PASSES + 1))
  else
    echo "  FAIL $name: exit=$code"
    echo "       want:"; sed 's/^/         /' <<< "$want"
    echo "       got:";  sed 's/^/         /' <<< "$got"
    sed 's/^/         /' "$SCENARIO/stderr"
    FAILURES=$((FAILURES + 1))
  fi
}
expect_not_called() {
  if [ -f "$SCENARIO/.called" ]; then
    echo "  FAIL $1: gh pr list was called"
    FAILURES=$((FAILURES + 1))
  else
    echo "  ok   $1 (gh not called)"
    PASSES=$((PASSES + 1))
  fi
}
out() { printf 'pr-number=%s\nhead-ref=%s\nhead-sha=%s\nrelease-pr=%s\nbackfill=%s' "$@"; }

# --- scenarios --------------------------------------------------------------
echo "resolve-comment-pr.sh"

new_scenario feature-pr
expect "PR run on a feature branch resolves from the payload" \
  "$(pr_run 10 feat/thing dev)" "$(out 10 feat/thing "$SHA" false false)"
expect_not_called "PR run needs no lookup"

new_scenario release-pr
expect "PR run on a release branch into main is a release PR" \
  "$(pr_run 11 release/2026-09-17 main)" "$(out 11 release/2026-09-17 "$SHA" true false)"

new_scenario hotfix-pr
expect "PR run on a hotfix branch into main is a release PR" \
  "$(pr_run 12 hotfix/crash main)" "$(out 12 hotfix/crash "$SHA" true false)"

new_scenario fork-release-pr
expect "a fork's release/* branch into main is not a release PR" \
  "$(pr_run 14 release/2026-09-17 main 99)" "$(out 14 release/2026-09-17 "$SHA" false false)"

new_scenario release-into-dev
expect "release/* into dev is not a release PR" \
  "$(pr_run 13 release/2026-09-17 dev)" "$(out 13 release/2026-09-17 "$SHA" false false)"

new_scenario dev-push-backfill
pr_list "20:feat/other:$OTHER:false" "21:release/2026-09-17:$SHA:false" > "$SCENARIO/prs.json"
expect "dev push whose SHA heads an open release PR back-fills it" \
  "$(push_run dev "$SHA")" "$(out 21 release/2026-09-17 "$SHA" true true)"

new_scenario dev-push-hotfix
pr_list "22:hotfix/crash:$SHA:false" > "$SCENARIO/prs.json"
expect "dev push whose SHA heads an open hotfix PR back-fills it" \
  "$(push_run dev "$SHA")" "$(out 22 hotfix/crash "$SHA" true true)"

new_scenario dev-push-no-match
pr_list "23:release/2026-09-10:$OTHER:false" > "$SCENARIO/prs.json"
expect "dev push with no release PR at its SHA reports nowhere" \
  "$(push_run dev "$SHA")" "$(out "" "" "" false false)"

new_scenario dev-push-feature-head
pr_list "24:feat/from-dev-tip:$SHA:false" > "$SCENARIO/prs.json"
expect "a feature branch sharing dev's SHA is not back-filled" \
  "$(push_run dev "$SHA")" "$(out "" "" "" false false)"

new_scenario dev-push-fork
pr_list "25:release/2026-09-17:$SHA:true" > "$SCENARIO/prs.json"
expect "a fork's release/* branch is never back-filled" \
  "$(push_run dev "$SHA")" "$(out "" "" "" false false)"

new_scenario other-branch-push
pr_list "26:release/2026-09-17:$SHA:false" > "$SCENARIO/prs.json"
expect "a push run on a branch other than dev reports nowhere" \
  "$(push_run main "$SHA")" "$(out "" "" "" false false)"
expect_not_called "non-dev push needs no lookup"

new_scenario dev-push-lookup-fails
touch "$SCENARIO/fail"
expect "a failed PR listing reports nowhere without failing" \
  "$(push_run dev "$SHA")" "$(out "" "" "" false false)"
grep -q '::warning::' "$SCENARIO/stderr" && echo "  ok   lookup failure is warned about" && PASSES=$((PASSES + 1)) \
  || { echo "  FAIL lookup failure should warn"; FAILURES=$((FAILURES + 1)); }

echo
echo "$PASSES passed, $FAILURES failed"
[ "$FAILURES" -eq 0 ]
