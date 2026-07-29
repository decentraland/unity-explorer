#!/bin/bash
# Prints the lipo architecture status of every dylib next to this script.
# All UUAV macOS binaries must be universal (arm64 + x86_64).
# Exit code: 0 if all universal, 1 otherwise.
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
fail=0
for lib in "$DIR"/*.dylib; do
    archs="$(lipo -archs "$lib")"
    if [[ "$archs" == *arm64* && "$archs" == *x86_64* ]]; then
        status="OK  "
    else
        status="FAIL"; fail=1
    fi
    printf '%s %-24s %s\n' "$status" "$(basename "$lib")" "$archs"
done
exit "$fail"
