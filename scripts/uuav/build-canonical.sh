#!/bin/bash

set -euo pipefail

case "$(uname -s)" in
Darwin) CANONICAL_ROOT="${UUAV_CANONICAL_ROOT:-/Users/Shared/build}" ;;
*) CANONICAL_ROOT="${UUAV_CANONICAL_ROOT:-/c/build}" ;;
esac

REPO="$(git rev-parse --show-toplevel)"
PLUGIN="Explorer/Assets/Plugins/UUAV"
REV="$(git -C "$REPO" rev-parse HEAD)"

DEPLOY_GLOB="$PLUGIN/Packages/UUAV/Runtime/Plugins"
if ! git -C "$REPO" diff --quiet HEAD -- "$PLUGIN" scripts/uuav ":(exclude)$DEPLOY_GLOB"; then
    echo "error: $PLUGIN or scripts/uuav has uncommitted changes, and the build takes its source from HEAD - they would not be in it" >&2
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

FFMPEG_SRC="$REPO/$PLUGIN/native/.third_party/ffmpeg"
if [[ ! -d "$FFMPEG_SRC" ]]; then
    echo "error: $FFMPEG_SRC is missing; build it with native/scripts/build-ffmpeg-macos.sh (macOS) or fetch it with scripts/uuav/fetch-ffmpeg-windows.sh (Windows) first" >&2
    exit 1
fi

rm -rf "$CANONICAL_ROOT"
mkdir -p "$CANONICAL_ROOT"

git -C "$REPO" -c core.autocrlf=false -c core.eol=lf archive "$REV" "$PLUGIN" scripts/uuav |
    tar -x -C "$CANONICAL_ROOT"

CANONICAL_NATIVE="$CANONICAL_ROOT/$PLUGIN/native"
mkdir -p "$CANONICAL_NATIVE/.third_party"
cp -R "$FFMPEG_SRC" "$CANONICAL_NATIVE/.third_party/ffmpeg"

(cd "$CANONICAL_NATIVE" && bash build.sh)

case "$(uname -s)" in
Darwin) PLATFORM_DIR="macOS" ;;
*) PLATFORM_DIR="x86_64" ;;
esac

DEPLOYED="$CANONICAL_ROOT/$PLUGIN/Packages/UUAV/Runtime/Plugins/$PLATFORM_DIR"
DEST="$REPO/$PLUGIN/Packages/UUAV/Runtime/Plugins/$PLATFORM_DIR"
mkdir -p "$DEST"
cp -R "$DEPLOYED/." "$DEST/"

echo "built at $CANONICAL_ROOT from $REV, deployed to $DEST"
