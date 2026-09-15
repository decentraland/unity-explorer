#!/bin/bash
#
# Canonical build: the shipped bytes must not depend on where the source was
# checked out, so HEAD is exported to one fixed directory (the canonical root)
# and built there. Two builds from two checkouts through this script are
# therefore comparable byte-for-byte, which is what repro-gate.sh relies on.

set -euo pipefail

case "$(uname -s)" in
Darwin) CANONICAL_ROOT="${RUST_SEGMENT_CANONICAL_ROOT:-/Users/Shared/build}" ;;
*) CANONICAL_ROOT="${RUST_SEGMENT_CANONICAL_ROOT:-/c/build}" ;;
esac

REPO="$(git rev-parse --show-toplevel)"
PLUGIN="Explorer/Assets/Plugins/RustSegment"
REV="$(git -C "$REPO" rev-parse HEAD)"

DEPLOY_GLOB="$PLUGIN/SegmentServerWrap/Libraries"
if ! git -C "$REPO" diff --quiet HEAD -- "$PLUGIN" scripts/rust-segment ":(exclude)$DEPLOY_GLOB"; then
    echo "error: $PLUGIN or scripts/rust-segment has uncommitted changes, and the build takes its source from HEAD - they would not be in it" >&2
    exit 1
fi

if [[ -L "$CANONICAL_ROOT" ]]; then
    echo "error: $CANONICAL_ROOT is a symlink; refusing to build shipped binaries through it" >&2
    exit 1
fi
if [[ -e "$CANONICAL_ROOT" && ! -O "$CANONICAL_ROOT" ]]; then
    echo "error: $CANONICAL_ROOT exists and is not owned by you; refusing to build shipped binaries in it" >&2
    exit 1
fi

rm -rf "$CANONICAL_ROOT"
mkdir -p "$CANONICAL_ROOT"

# The build path leaks into the bytes (panic locations, PDB records), so every build uses one fixed directory.
git -C "$REPO" -c core.autocrlf=false -c core.eol=lf archive "$REV" "$PLUGIN" scripts/rust-segment |
    tar -x -C "$CANONICAL_ROOT"

(cd "$CANONICAL_ROOT/$PLUGIN/.native" && bash build.sh)

case "$(uname -s)" in
Darwin) PLATFORM_DIR="Mac" ;;
*) PLATFORM_DIR="Windows" ;;
esac

DEPLOYED="$CANONICAL_ROOT/$DEPLOY_GLOB/$PLATFORM_DIR"
DEST="$REPO/$DEPLOY_GLOB/$PLATFORM_DIR"
mkdir -p "$DEST"
cp -R "$DEPLOYED/." "$DEST/"

echo "built at $CANONICAL_ROOT from $REV, deployed to $DEST"
