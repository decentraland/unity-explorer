#!/usr/bin/env bash
# Filter false positives out of an InspectCode report and print the remaining count.
# Only warning-or-higher results (SARIF level 'warning'/'error') in code we own
# participate in the ratchet; the exclusions and their reasons are declared inline below.
#
# Usage: filter-warnings.sh <report.json> <filtered_output.json>
# Writes the filtered results array to <filtered_output.json>; prints the integer count to stdout.
set -euo pipefail

report="${1:?usage: filter-warnings.sh <report.json> <filtered_output.json>}"
out="${2:?usage: filter-warnings.sh <report.json> <filtered_output.json>}"

excluded_rules=(
    '.CSharpErrors'      # false positive from '--no-build' (unresolved refs)
    '.CppCompilerErrors' # false positive from '--no-build' (unresolved refs)
    'CheckNamespace'     # namespaces are domain names, not folder paths (docs/code-style-guidelines.md § Namespaces)
)

# vendored / third-party code we don't own
excluded_paths='^(Packages/|Assets/Plugins/(DOTween|SocketIO)/)'

if [ ! -f "$report" ]; then
    echo "filter-warnings: report not found at '$report'" >&2
    exit 1
fi

jq --argjson excludedRules "$(printf '%s\n' "${excluded_rules[@]}" | jq -R . | jq -s .)" \
   --arg excludedPaths "$excluded_paths" '
  .runs[0].results
  | map(select(
      ((.level // "warning") | IN("warning", "error"))
      and ((.ruleId // "") | IN($excludedRules[]) | not)
      and ((.locations[0].physicalLocation.artifactLocation.uri // "")
           | test($excludedPaths; "i") | not)
    ))
' "$report" > "$out"

jq length "$out"
