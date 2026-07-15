#!/usr/bin/env bash
# Download the JetBrains ReSharper command-line tools.
# Single source of truth for all linting in the project
#
# Usage: download-resharper.sh [target_dir]   (default target_dir: rsharp)
set -euo pipefail

# Held at the 2025.1 wave (not the newer 2025.3) because the JetBrains.Unity extension
# below only has a build for wave 251 (2025.1) — no 2025.2/2025.3 build is published, and
# an extension is rejected by a mismatched engine wave. The extension is what makes the CLI
# Unity-aware (Assets/Packages are not namespace providers), matching Rider and killing the
# false CheckNamespace warnings. Bump both together once a newer Unity build ships.
RESHARPER_URL="https://download.jetbrains.com/resharper/dotUltimate.2025.1.4/JetBrains.ReSharper.CommandLineTools.2025.1.4.zip"

# Unity Support extension, pinned to the 2025.1.4.67 build (wave 251, matches the CLI above).
# Dropped into the CLI directory, which InspectCode auto-scans for extension nupkgs, so it
# loads offline — no plugin-gallery access needed at inspection time. run-inspectcode.sh only
# names it via --eXtensions; it must NOT also pass --source for this dir (double-registration).
UNITY_EXT_URL="https://plugins.jetbrains.com/files/JetBrains.Unity/2025.1.4.67/jetbrains.unity.2025.1.4.67.nupkg"

target="${1:-rsharp}"

if [ -x "$target/inspectcode.sh" ]; then
    echo "ReSharper CLI already present at '$target'." >&2
    exit 0
fi

echo "Downloading ReSharper CLI to '$target'..." >&2
wget -q "$RESHARPER_URL" -O rsharp.zip
unzip -q rsharp.zip -d "$target"
chmod +x "$target"/*.sh

echo "Downloading Unity Support extension to '$target'..." >&2
wget -q "$UNITY_EXT_URL" -O "$target/jetbrains.unity.2025.1.4.67.nupkg"

echo "ReSharper CLI installed at '$target'." >&2
