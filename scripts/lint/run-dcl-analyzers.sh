#!/usr/bin/env bash
# Run the in-repo DCL Roslyn analyzers (Explorer/Assets/DCL/Analyzers) from the shell, without the Editor.
#
# Recompiles each project assembly with Unity's bundled compiler and the exact response file
# Unity generated for it (Library/Bee/artifacts/<dag>/<Assembly>.rsp), so references, defines,
# rulesets, source generators and the analyzer DLL itself match an Editor compile. Output goes to
# a temp folder; Library/ is not modified.
#
# Requirements: Unity has compiled the project at least once (Library/Bee exists) with the Editor
# version from ProjectSettings/ProjectVersion.txt. The source list comes from that compile, so a
# .cs file added since then is not analyzed until Unity recompiles.
#
# Usage: run-dcl-analyzers.sh [-a] [assembly...]
#   assembly   only analyze these assemblies (exact names, e.g. DCL.Social DCL.Plugins)
#   -a         also print the diagnostics of every other analyzer, not just DCL*
#
# Environment: UNITY_EDITOR_CONTENTS overrides the Editor install
#   (default: /Applications/Unity/Hub/Editor/<version>/Unity.app/Contents).
#
# Exit codes: 0 no findings, 1 findings, 2 setup error (including the analyzers failing to load),
#             3 an assembly failed to compile for another reason.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
explorer="$repo/Explorer"

pattern='(warning|error) DCL[0-9]+'
if [ "${1:-}" = "-a" ]; then
    pattern='(warning|error) [A-Z]+[0-9]+'
    shift
fi

version="$(sed -n 's/^m_EditorVersion: //p' "$explorer/ProjectSettings/ProjectVersion.txt")"
contents="${UNITY_EDITOR_CONTENTS:-/Applications/Unity/Hub/Editor/$version/Unity.app/Contents}"
dotnet="$(find "$contents" -path '*DotNetSdk/dotnet' -type f 2>/dev/null | head -1 || true)"
csc="$(find "$contents" -path '*DotNetSdk/sdk/*/Roslyn/bincore/csc.dll' -type f 2>/dev/null | head -1 || true)"

if [ -z "$dotnet" ] || [ -z "$csc" ]; then
    echo "run-dcl-analyzers: Unity $version compiler not found under '$contents' (set UNITY_EDITOR_CONTENTS)" >&2
    exit 2
fi

# The Editor build graph (*EDbg.dag); fall back to any graph that has response files
dag="$(ls -d "$explorer"/Library/Bee/artifacts/*EDbg.dag 2>/dev/null | head -1 || true)"
[ -n "$dag" ] || dag="$(ls -d "$explorer"/Library/Bee/artifacts/*.dag 2>/dev/null | head -1 || true)"

if [ -z "$dag" ] || ! ls "$dag"/*.rsp >/dev/null 2>&1; then
    echo "run-dcl-analyzers: no Unity response files in Explorer/Library/Bee - open or batch-compile the project in Unity first" >&2
    exit 2
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Project assemblies only: package assemblies are compiled with '-warn:0' and the analyzer skips them anyway
: > "$work/rsps"
for rsp in "$dag"/*.rsp; do
    case "$rsp" in *.mvfrm.rsp) continue ;; esac
    grep -qx -- '-warn:0' "$rsp" && continue
    name="$(basename "$rsp" .rsp)"

    if [ $# -gt 0 ]; then
        wanted=false
        for a in "$@"; do [ "$a" = "$name" ] && wanted=true; done
        $wanted || continue
    fi

    echo "$rsp" >> "$work/rsps"
done

count="$(wc -l < "$work/rsps" | tr -d ' ')"
if [ "$count" -eq 0 ]; then
    echo "run-dcl-analyzers: no matching project assemblies in $dag" >&2
    exit 2
fi

echo "Analyzing $count assemblies with Unity $version compiler..." >&2

# Response-file paths are relative to Explorer/; the trailing -out/-refout override Unity's so Library/ is untouched
compile_one() {
    local rsp="$1" name
    name="$(basename "$rsp" .rsp)"
    if ! "$DOTNET" "$CSC" /noconfig "@$rsp" "-out:$WORK/$name.dll" "-refout:$WORK/$name.ref.dll" > "$WORK/$name.log" 2>&1; then
        echo "$name" >> "$WORK/failed"
    fi
}
export -f compile_one
export DOTNET="$dotnet" CSC="$csc" WORK="$work"

cd "$explorer"
jobs="$(sysctl -n hw.ncpu 2>/dev/null || nproc 2>/dev/null || echo 4)"
tr '\n' '\0' < "$work/rsps" | xargs -0 -n 1 -P "$jobs" bash -c 'compile_one "$0"'

# The compiler only warns when an analyzer cannot load (CS8034) or throws (AD0001), which would
# otherwise read as "0 findings"
if cat "$work"/*.log | grep -E '(CS8034|AD0001).*DCL\.Analyzers' | sort -u >&2; then
    echo "run-dcl-analyzers: the DCL analyzers did not run (see above; an LFS pointer instead of the DLL?)" >&2
    exit 2
fi

cat "$work"/*.log | grep -E "$pattern" | sort -u > "$work/findings" || true
cat "$work/findings"

findings="$(wc -l < "$work/findings" | tr -d ' ')"
echo >&2
echo "$findings finding(s) in $count assemblies" >&2
if [ "$findings" -gt 0 ]; then
    grep -oE "$pattern" "$work/findings" | awk '{print $2}' | sort | uniq -c | sort -rn >&2
fi

# DCL rules are errors, so a finding alone fails the compile: only C# errors count as a compile failure
if [ -f "$work/failed" ]; then
    : > "$work/broken"
    while read -r name; do
        grep -qE 'error CS[0-9]+' "$work/$name.log" && echo "$name" >> "$work/broken"
    done < "$work/failed"

    if [ -s "$work/broken" ]; then
        echo >&2
        echo "Failed to compile (stale Library/Bee? recompile in Unity):" >&2
        while read -r name; do
            echo "  $name" >&2
            grep -m 3 -E 'error CS[0-9]+' "$work/$name.log" | sed 's/^/    /' >&2 || true
        done < "$work/broken"
        exit 3
    fi
fi

[ "$findings" -eq 0 ] || exit 1
