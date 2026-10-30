#!/usr/bin/env bash
# Download the JetBrains ReSharper command-line tools.
# Single source of truth for all linting in the project
#
# Usage: download-resharper.sh [target_dir]   (default target_dir: rsharp)
set -euo pipefail

RESHARPER_URL="https://download.jetbrains.com/resharper/dotUltimate.2025.3.0.1/JetBrains.ReSharper.CommandLineTools.2025.3.0.1.zip"

target="${1:-rsharp}"

if [ -x "$target/inspectcode.sh" ]; then
    echo "ReSharper CLI already present at '$target'." >&2
    exit 0
fi

echo "Downloading ReSharper CLI to '$target'..." >&2
wget -q "$RESHARPER_URL" -O rsharp.zip
unzip -q rsharp.zip -d "$target"
chmod +x "$target"/*.sh
echo "ReSharper CLI installed at '$target'." >&2
