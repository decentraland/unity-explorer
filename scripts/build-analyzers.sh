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
        exec nix-shell -p dotnet-sdk_10 --run "bash $0 $*"
    fi
    echo "build-analyzers: dotnet SDK not found (and no nix-shell to provide one)" >&2
    exit 1
fi

# Run from Analyzers/ so Analyzers/global.json pins the SDK: the CI drift check
# byte-compares a rebuild against the committed DLL, and byte-identical output
# requires the same Roslyn compiler on every machine.
cd Analyzers
# Always build clean: stale obj/ state can leak into the output and desync it
# from the clean rebuild the CI drift check performs (observed: incremental
# rebuild after an edit produced different bytes than the clean CI build).
rm -rf DCL.Analyzers/bin DCL.Analyzers/obj
dotnet test DCL.Analyzers.Tests -v q --nologo
# ContinuousIntegrationBuild normalizes embedded paths and DebugType=none drops
# the debug directory (whose source hashes differ between CRLF and LF checkouts),
# so this local build is byte-identical to the CI drift check's rebuild
# (workflow: "Fail on DLL drift").
dotnet build DCL.Analyzers -c Release -v q --nologo \
    -p:ContinuousIntegrationBuild=true -p:DebugType=none
cd "$ROOT"

src="Analyzers/DCL.Analyzers/bin/Release/netstandard2.0/DCL.Analyzers.dll"
dst="Explorer/Assets/DCL/DCL.Analyzers.dll"
cp "$src" "$dst"
echo "synced: $dst ($(sha256sum "$dst" | cut -c1-16)…, $(stat -c%s "$dst") bytes)"
