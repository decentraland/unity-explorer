#!/usr/bin/env bash
# Filter false positives out of an InspectCode report and print the remaining count.
# Excludes:
#   - '.CSharpErrors' / '.CppCompilerErrors': false positives from '--no-build' (unresolved refs).
#   - vendored / third-party code we don't own: everything under 'Packages/' and the
#     'DOTween' / 'SocketIO' plugins.
#   - everything below warning severity (SARIF level 'note' = ReSharper suggestions/hints):
#     only warning-or-higher results participate in the ratchet.
#   - ruleIds listed in EXCLUDE_RULES (optional, comma-separated). Empty/unset excludes
#     nothing extra — the local AI-flow hook relies on that default.
#
# Usage: [EXCLUDE_RULES=RuleA,RuleB] filter-warnings.sh <report.json> <filtered_output.json>
# Writes the filtered results array to <filtered_output.json>; prints the integer count to stdout.
set -euo pipefail

report="${1:?usage: filter-warnings.sh <report.json> <filtered_output.json>}"
out="${2:?usage: filter-warnings.sh <report.json> <filtered_output.json>}"

if [ ! -f "$report" ]; then
    echo "filter-warnings: report not found at '$report'" >&2
    exit 1
fi

jq --arg excluded "${EXCLUDE_RULES:-}" '
  ($excluded | split(",") | map(select(length > 0))) as $skip
  | .runs[0].results
  | map(select(
      ((.level // "warning") | IN("warning", "error"))
      and (.ruleId != ".CSharpErrors" and .ruleId != ".CppCompilerErrors")
      and ((.ruleId // "") | IN($skip[]) | not)
      and ((.locations[0].physicalLocation.artifactLocation.uri // "")
           | test("^(Packages/|Assets/Plugins/(DOTween|SocketIO)/)"; "i") | not)
    ))
' "$report" > "$out"

jq length "$out"
