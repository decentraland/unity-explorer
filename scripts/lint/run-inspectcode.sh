#!/usr/bin/env bash
# Run ReSharper InspectCode
# Single source of truth for the inspection invocation
#
# Usage: run-inspectcode.sh [solution] [output_report]
#   solution       default: Explorer/Explorer.sln
#   output_report  default: InspectCodeReport.json
#
# Exit codes: 0 = inspection ran; 3 = ReSharper CLI not found (caller decides what to do).
set -euo pipefail

solution="${1:-Explorer/Explorer.sln}"
output="${2:-InspectCodeReport.json}"

# Resolve the CLI: explicit RSHARP_HOME, the CI layout (./rsharp), then a common local install.
find_cli() {
    local d
    for d in "${RSHARP_HOME:-}" "rsharp" "$HOME/Downloads/rsharp"; do
        if [ -n "$d" ] && [ -x "$d/inspectcode.sh" ]; then
            echo "$d/inspectcode.sh"
            return 0
        fi
    done
    return 1
}

cli="$(find_cli)" || {
    echo "run-inspectcode: ReSharper CLI not found (looked in \$RSHARP_HOME, ./rsharp, ~/Downloads/rsharp)." >&2
    exit 3
}

# --no-build keeps execution time down; produces a SARIF/JSON report.
"$cli" "$solution" \
    --no-build \
    --verbosity=INFO \
    --properties:Configuration=Debug \
    --disable-settings-layers:SolutionPersonal \
    --output="$output"
