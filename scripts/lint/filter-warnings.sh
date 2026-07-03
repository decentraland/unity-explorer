#!/usr/bin/env bash
# Filter false positives out of an InspectCode report and print the remaining count.
# Excludes:
#   - '.CSharpErrors' / '.CppCompilerErrors': false positives from '--no-build' (unresolved refs).
#   - vendored / third-party code we don't own: everything under 'Packages/' and the
#     'DOTween' / 'SocketIO' plugins.
#
# Usage: filter-warnings.sh <report.json> <filtered_output.json>
# Writes the filtered results array to <filtered_output.json>; prints the integer count to stdout.
set -euo pipefail

report="${1:?usage: filter-warnings.sh <report.json> <filtered_output.json>}"
out="${2:?usage: filter-warnings.sh <report.json> <filtered_output.json>}"

if [ ! -f "$report" ]; then
    echo "filter-warnings: report not found at '$report'" >&2
    exit 1
fi

jq '
  .runs[0].results
  | map(select(
      (.ruleId != ".CSharpErrors" and .ruleId != ".CppCompilerErrors")
      and ((.locations[0].physicalLocation.artifactLocation.uri // "")
           | test("^(Packages/|Assets/Plugins/(DOTween|SocketIO)/)"; "i") | not)
    ))
' "$report" > "$out"

jq length "$out"
