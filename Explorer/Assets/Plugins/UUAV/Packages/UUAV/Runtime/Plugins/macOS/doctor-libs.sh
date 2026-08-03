#!/bin/bash
# Prints the lipo architecture status of every dylib next to this script
# and of the uuav-helper executable.
# All UUAV macOS binaries must be universal (arm64 + x86_64).
# Exit code: 0 if all universal, 1 otherwise.
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
fail=0
for lib in "$DIR"/*.dylib "$DIR"/uuav-helper; do
    if ! archs="$(lipo -archs "$lib" 2>/dev/null)"; then
        # unreadable as mach-o: missing, or an unfetched git-lfs pointer
        printf 'FAIL %-24s %s\n' "$(basename "$lib")" "not a mach-o (git-lfs pointer?)"
        fail=1
        continue
    fi
    if [[ "$archs" == *arm64* && "$archs" == *x86_64* ]]; then
        status="OK  "
    else
        status="FAIL"; fail=1
    fi
    printf '%s %-24s %s\n' "$status" "$(basename "$lib")" "$archs"
done
exit "$fail"
