#!/usr/bin/env bash
# Build the DCL Roslyn analyzers and install the DLL Unity loads (../DCL.Analyzers.dll).
#
# Usage: build.sh [check]
#   (no args)  run the tests, build Release and copy the DLL next to this folder
#   check      rebuild and fail if the committed DLL differs from a build of this source
#
# The build is deterministic, so 'check' is only meaningful with the SDK pinned in global.json.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
installed="$here/../DCL.Analyzers.dll"
built="$here/DCL.Analyzers/bin/Release/netstandard2.0/DCL.Analyzers.dll"

dotnet build "$here/DCL.Analyzers/DCL.Analyzers.csproj" -c Release --nologo -v quiet

case "${1:-}" in
    check)
        if cmp -s "$built" "$installed"; then
            echo "DCL.Analyzers.dll matches its source"
        else
            echo "DCL.Analyzers.dll does not match a build of .source/ - run build.sh and commit the result" >&2
            exit 1
        fi
        ;;
    "")
        dotnet test "$here/DCL.Analyzers.Tests/DCL.Analyzers.Tests.csproj" --nologo -v quiet
        cp "$built" "$installed"
        echo "Installed $(shasum -a 256 "$installed" | cut -d' ' -f1)  DCL.Analyzers.dll"
        ;;
    *)
        echo "usage: build.sh [check]" >&2
        exit 2
        ;;
esac
