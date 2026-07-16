#!/usr/bin/env bash
# Download the JetBrains ReSharper command-line tools.
# Single source of truth for all linting in the project
#
# Usage: download-resharper.sh [target_dir]   (default target_dir: rsharp)
set -euo pipefail

# Pinned to the 2023.1 wave on purpose. The JetBrains.Unity extension is what makes the CLI
# Unity-aware (Assets/Packages are NOT namespace providers), matching Rider and killing the
# false CheckNamespace warnings. In every 2023.2+ build the Unity plugin schedules a background
# deferred-cache flush that crashes headless on Linux ("Serializing delegates is not supported",
# JetBrains RIDER-122490 / resharper-unity#2491) and truncates the scan. The 2023.1 plugin does
# NOT schedule that background process, so it runs to completion on the Linux CI container.
# Bump both together only once JetBrains ships a build with that bug fixed on Linux.
RESHARPER_URL="https://download.jetbrains.com/resharper/dotUltimate.2023.1.2/JetBrains.ReSharper.CommandLineTools.2023.1.2.zip"

# Unity Support extension, pinned to the 2023.1.0.150 build (matches the 2023.1 CLI above).
# Placed in a SEPARATE feed dir (not the CLI dir): run-inspectcode.sh points --source at it and
# names it via --eXtensions, so it loads offline with a single deployment. It must NOT live in
# the CLI dir, which InspectCode auto-scans — auto-scan + --eXtensions would deploy the same
# package twice and crash startup with "more than one package with the same ID JetBrains.Unity".
UNITY_EXT_URL="https://plugins.jetbrains.com/files/JetBrains.Unity/2023.1.0.150/jetbrains.unity.2023.1.0.150.nupkg"

target="${1:-rsharp}"
# Sibling of the CLI dir; run-inspectcode.sh derives the same path as "<cli_dir>-plugins".
ext_dir="${target%/}-plugins"

if [ -x "$target/inspectcode.sh" ]; then
    echo "ReSharper CLI already present at '$target'." >&2
    exit 0
fi

echo "Downloading ReSharper CLI to '$target'..." >&2
wget -q "$RESHARPER_URL" -O rsharp.zip
unzip -q rsharp.zip -d "$target"
chmod +x "$target"/*.sh

echo "Downloading Unity Support extension to '$ext_dir'..." >&2
mkdir -p "$ext_dir"
wget -q "$UNITY_EXT_URL" -O "$ext_dir/jetbrains.unity.2023.1.0.150.nupkg"

echo "ReSharper CLI installed at '$target'." >&2
