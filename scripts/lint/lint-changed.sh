#!/usr/bin/env bash
# inspect the C# files changed this session with the SAME ReSharper rules CI uses,
# and report issues in files Claude touched.
#
# Behaviour:
#   - no changed .cs            -> exit 0 (skip the multi-minute solution load)
#   - ReSharper CLI not present -> print install hint, exit 0 (advisory, never blocks)
#   - tooling failure           -> warn, exit 0 (never trap the flow on environment issues)
#   - findings in changed files -> print them, exit 2 (blocks so they get fixed)
set -uo pipefail

ROOT="$(git rev-parse --show-toplevel)"
LINT_DIR="$ROOT/scripts/lint"
cd "$ROOT"

# Loop guard: if this Stop is already a hook-continuation, don't block again.
input="$(cat 2>/dev/null || true)"
if [ -n "$input" ] && command -v jq >/dev/null 2>&1; then
    [ "$(printf '%s' "$input" | jq -r '.stop_hook_active // false' 2>/dev/null)" = "true" ] && exit 0
fi

# Changed C# under Explorer/ (working tree + staged + new untracked).
changed="$(
    {
        git diff --name-only --diff-filter=ACM HEAD -- Explorer
        git diff --name-only --diff-filter=ACM --cached -- Explorer
        git ls-files --others --exclude-standard -- Explorer
    } 2>/dev/null | grep -E '\.cs$' | sort -u
)"
[ -z "$changed" ] && exit 0

report="$(mktemp)"; filtered="$(mktemp)"
trap 'rm -f "$report" "$filtered"' EXIT

bash "$LINT_DIR/run-inspectcode.sh" "Explorer/Explorer.sln" "$report"
rc=$?

if [ "$rc" -eq 3 ]; then
    {
        echo "ReSharper CLI not found — skipping the AI-flow lint."
        echo "Install it (same version CI uses) with:"
        echo "    bash scripts/lint/download-resharper.sh"
    } >&2
    exit 0
elif [ "$rc" -ne 0 ]; then
    echo "Lint: inspectcode failed (rc=$rc) — not blocking." >&2
    exit 0
fi

bash "$LINT_DIR/filter-warnings.sh" "$report" "$filtered" >/dev/null || {
    echo "Lint: filter step failed - not blocking." >&2
    exit 0
}

# Report uris are relative to the solution dir (Explorer/); strip that prefix from changed paths.
rels="$(printf '%s\n' "$changed" | sed 's#^Explorer/##' | jq -R . | jq -s .)"
findings="$(jq --argjson changed "$rels" '
    map(select(((.locations[0].physicalLocation.artifactLocation.uri // "")) as $u | ($changed | index($u)) != null))
' "$filtered")"

n="$(printf '%s' "$findings" | jq 'length')"
if [ "$n" -gt 0 ]; then
    {
        echo "ReSharper found $n issue(s) in files changed this session (same rules as CI). Resolve them before finishing:"
        printf '%s' "$findings" | jq -r '.[] | "  \(.locations[0].physicalLocation.artifactLocation.uri):\(.locations[0].physicalLocation.region.startLine)  \(.ruleId)  \(.message.text)"'
    } >&2
    exit 2
fi
exit 0
