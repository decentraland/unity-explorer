#!/bin/bash

set -euo pipefail

TARGET="${1:-}"
case "$TARGET" in
macos-universal)
    PLATFORM_DIR="macOS"
    CARGO_ARTIFACTS=(libuuav.dylib libuuav_core.dylib uuav-helper)
    ;;
windows-x86_64)
    PLATFORM_DIR="x86_64"
    CARGO_ARTIFACTS=(uuav.dll uuav_core.dll uuav-helper.exe)
    ;;
*)
    echo "usage: repro-gate.sh <macos-universal|windows-x86_64>" >&2
    exit 2
    ;;
esac

PLUGIN="Explorer/Assets/Plugins/UUAV"
RUNTIME_REL="$PLUGIN/Packages/UUAV/Runtime/Plugins/$PLATFORM_DIR"
FFMPEG_REL="$PLUGIN/native/.third_party/ffmpeg"

FIRST_TREE="$(git rev-parse --show-toplevel)"
REV="$(git -C "$FIRST_TREE" rev-parse HEAD)"

WORK="${UUAV_REPRO_WORK:-$(dirname "$FIRST_TREE")}"
SECOND_TREE="$WORK/uuav-repro-second-tree"
KEEP="$WORK/uuav-repro-first-build"

in_actions() { [[ "${GITHUB_ACTIONS:-}" == "true" ]]; }

group() { in_actions && echo "::group::$1" || echo "--- $1"; }
endgroup() { in_actions && echo "::endgroup::" || true; }

sha256() {
    if command -v sha256sum > /dev/null 2>&1; then
        sha256sum "$1" | cut -d' ' -f1
    else
        shasum -a 256 "$1" | cut -d' ' -f1
    fi
}

if [[ ! -d "$FIRST_TREE/$FFMPEG_REL" ]]; then
    echo "error: $FFMPEG_REL is missing; provide FFmpeg before the gate - it is fetched or built once and both cargo builds reuse it" >&2
    exit 1
fi

export GIT_LFS_SKIP_SMUDGE=1

rm -rf "$SECOND_TREE" "$KEEP"

group "second source tree at $SECOND_TREE"
git clone --quiet --shared --no-checkout "$FIRST_TREE" "$SECOND_TREE"
git -C "$SECOND_TREE" sparse-checkout set "$PLUGIN" scripts/uuav
git -C "$SECOND_TREE" checkout --quiet "$REV"
mkdir -p "$SECOND_TREE/$PLUGIN/native/.third_party"
cp -R "$FIRST_TREE/$FFMPEG_REL" "$SECOND_TREE/$PLUGIN/native/.third_party/ffmpeg"
echo "HEAD $(git -C "$SECOND_TREE" rev-parse HEAD)"
endgroup

build_in() {
    local tree="$1" label="$2"
    group "$label build - $tree"
    (cd "$tree" && bash scripts/uuav/build-canonical.sh)
    endgroup
}

build_in "$FIRST_TREE" "first"
mkdir -p "$KEEP"
for name in "${CARGO_ARTIFACTS[@]}"; do
    if [[ ! -f "$FIRST_TREE/$RUNTIME_REL/$name" ]]; then
        echo "error: the first build produced no $RUNTIME_REL/$name" >&2
        exit 1
    fi
    cp "$FIRST_TREE/$RUNTIME_REL/$name" "$KEEP/$name"
done

build_in "$SECOND_TREE" "second"

echo
echo "Gate A - two builds, two source trees, one canonical path"
status=0
summary=""
for name in "${CARGO_ARTIFACTS[@]}"; do
    first="$KEEP/$name"
    second="$SECOND_TREE/$RUNTIME_REL/$name"

    if [[ ! -f "$second" ]]; then
        echo "::error::[$TARGET] the second build produced no $name"
        summary="$summary| \`$name\` | missing from the second build |"$'\n'
        status=1
        continue
    fi

    if cmp -s "$first" "$second"; then
        printf '  identical  %-18s %s\n' "$name" "$(sha256 "$first")"
        summary="$summary| \`$name\` | identical (\`$(sha256 "$first")\`) |"$'\n'
        continue
    fi

    first_bytes=$(wc -c < "$first" | tr -d ' ')
    second_bytes=$(wc -c < "$second" | tr -d ' ')
    differing=$( (cmp -l "$first" "$second" 2> /dev/null || true) | wc -l | tr -d ' ')
    where=$( (cmp "$first" "$second" 2>&1 || true) | head -1)

    echo "::error::[$TARGET] $name is not reproducible on this runner. Two builds of $REV, same machine, same canonical path, source trees named '$(basename "$FIRST_TREE")' and '$(basename "$SECOND_TREE")', produced different bytes: $differing byte(s) differ ($first_bytes vs $second_bytes bytes; $where). Something in the build is reading the clock, the source path or an unordered container - see scripts/uuav/build-canonical.sh for how the canonical path removes the source-path input."
    printf '  DIFFERS    %-18s %s bytes differ\n' "$name" "$differing"
    summary="$summary| \`$name\` | **$differing byte(s) differ** ($first_bytes vs $second_bytes) |"$'\n'
    status=1
done

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    {
        echo "### Gate A - same-runner determinism ($TARGET)"
        echo
        echo "Two \`build-canonical.sh\` runs of \`$REV\` on this runner, from"
        echo "\`$(basename "$FIRST_TREE")/\` and \`$(basename "$SECOND_TREE")/\`."
        echo
        echo "| artifact | result |"
        echo "|---|---|"
        printf '%s' "$summary"
        echo
    } >> "$GITHUB_STEP_SUMMARY"
fi

if [[ "$status" -eq 0 ]]; then
    rm -rf "$SECOND_TREE"
    echo
    echo "Gate A PASS - ${#CARGO_ARTIFACTS[@]} cargo-produced artifact(s) byte-identical across two source trees."
fi

exit "$status"
