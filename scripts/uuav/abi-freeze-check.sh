#!/usr/bin/env bash

set -u

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
native="$repo_root/Explorer/Assets/Plugins/UUAV/native"
frame_info="$native/src/frame_info.rs"
backup="$(mktemp -t uuav-frame-info.XXXXXX)"
log="$(mktemp -t uuav-freeze-build.XXXXXX)"
swapped=""

cleanup() {
    if [ -f "$backup" ]; then
        cp "$backup" "$frame_info"
        rm -f "$backup"
    fi
    [ -n "$swapped" ] && rm -f "$swapped"
    rm -f "$log"
    return 0
}
trap cleanup EXIT INT TERM

fail() {
    echo "abi-freeze-check: FAIL: $*" >&2
    exit 1
}

[ -f "$frame_info" ] || fail "$frame_info does not exist"

if ! git -C "$repo_root" diff --quiet -- "$frame_info"; then
    fail "$frame_info already has uncommitted changes; refusing to edit it"
fi

cp "$frame_info" "$backup"

echo "abi-freeze-check: baseline build of uuav-adapter, unmodified tree"
( cd "$native" && cargo build -p uuav-adapter ) >"$log" 2>&1
status=$?

if [ "$status" -ne 0 ]; then
    echo "--- build output ---" >&2
    cat "$log" >&2
    fail "cargo build -p uuav-adapter FAILED on the unmodified tree (exit" \
        "$status); the adapter must build before a freeze check means anything"
fi

swapped="$(mktemp -t uuav-frame-info-swapped.XXXXXX)"
awk '
    BEGIN { pending = 0; done = 0 }
    pending == 1 {
        pending = 0
        if ($0 == second) { print; print first; done = 1; next }
        print first
    }
    $0 == first && done == 0 { pending = 1; next }
    { print }
    END {
        if (pending == 1) print first
        if (done == 0) exit 1
    }
' first="    pub colorspace: i32," second="    pub color_range: i32," \
    "$frame_info" >"$swapped" \
    || fail "the adjacent colorspace/color_range declarations were not found verbatim"

cat "$swapped" >"$frame_info" || fail "could not write the swapped $frame_info"
rm -f "$swapped"

if git -C "$repo_root" diff --quiet -- "$frame_info"; then
    fail "the swap produced no diff, so nothing was actually tested"
fi

echo "abi-freeze-check: swapped colorspace/color_range; building uuav-adapter"
( cd "$native" && cargo build -p uuav-adapter ) >"$log" 2>&1
status=$?

if [ "$status" -eq 0 ]; then
    fail "cargo build -p uuav-adapter SUCCEEDED on a reordered FrameInfo"
fi

if ! grep -q 'FRAME_INFO_DECL_SHA256' "$log"; then
    echo "--- build output ---" >&2
    cat "$log" >&2
    fail "the build failed, but not with the FrameInfo declaration digest guard"
fi

cleanup
trap - EXIT INT TERM

if ! git -C "$repo_root" diff --quiet -- "$frame_info"; then
    fail "$frame_info was not restored"
fi

echo "abi-freeze-check: PASS (adapter build failed on FRAME_INFO_DECL_SHA256, source restored)"
