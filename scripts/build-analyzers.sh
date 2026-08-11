#!/usr/bin/env bash
# Build the DCL.Analyzers Roslyn analyzer, run its tests, and sync the DLL into
# the Unity project (Explorer/Assets/DCL/DCL.Analyzers.dll - the placement makes
# Unity feed it to csc for every asmdef under Assets/DCL, and nothing vendored).
# Run after any change under Analyzers/. CI runs the tests on every such change;
# the DLL itself is synced manually via this script and committed (LFS).
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
    if command -v nix-shell >/dev/null 2>&1; then
        exec nix-shell -p dotnet-sdk_8 --run "bash $0 $*"
    fi
    echo "build-analyzers: dotnet SDK not found (and no nix-shell to provide one)" >&2
    exit 1
fi

dotnet test Analyzers/DCL.Analyzers.Tests -v q --nologo
dotnet build Analyzers/DCL.Analyzers -c Release -v q --nologo

src="Analyzers/DCL.Analyzers/bin/Release/netstandard2.0/DCL.Analyzers.dll"
dst="Explorer/Assets/DCL/DCL.Analyzers.dll"
cp "$src" "$dst"
echo "synced: $dst ($(sha256sum "$dst" | cut -c1-16)…, $(stat -c%s "$dst") bytes)"
