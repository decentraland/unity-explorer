#!/usr/bin/env bash
# Lists the code-signature verification result of every signed artifact in a build, one
# `<path> ok|invalid` line each, sorted by path. Two builds with identical bytes, modes and
# symlinks give identical output; comparing a rebuilt tree with the original proves the
# signatures survive patching.
#
# Usage: signature-status.sh <platform> <build root>
#   windows64: every *.exe, checked with osslsigncode (Authenticode)
#   macos:     every top-level *.app, checked with codesign --verify --deep --strict
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ $# -eq 2 ] || die "usage: signature-status.sh <platform> <build root>"
[ -d "$2" ] || die "not a directory: $2"
cd "$2"

case "$1" in
  windows64)
    require_tool osslsigncode
    find . -type f -name '*.exe' | sed 's|^\./||' | LC_ALL=C sort |
      while IFS= read -r exe; do
        if osslsigncode verify -in "$exe" < /dev/null > /dev/null 2>&1; then echo "$exe ok"; else echo "$exe invalid"; fi
      done
    ;;
  macos)
    require_tool codesign
    for app in *.app; do
      [ -d "$app" ] || continue
      if codesign --verify --deep --strict "$app" > /dev/null 2>&1; then echo "$app ok"; else echo "$app invalid"; fi
    done
    ;;
  *)
    die "unknown platform: $1"
    ;;
esac
