#!/usr/bin/env bash
# Shared helpers for the delta scripts. Sourced, never executed.
# Portable across Linux, macOS (stock bash 3.2, BSD tools) and Git Bash on Windows.

log() { echo "[delta] $*" >&2; }
die() { echo "[delta] error: $*" >&2; exit 1; }

require_tool() { # require_tool <command>...
  local t
  for t in "$@"; do
    # type -P looks only at executables, not at the jq() wrapper below.
    type -P "$t" >/dev/null 2>&1 || die "'$t' not found on PATH"
  done
}

is_windows() {
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) return 0 ;;
    *) return 1 ;;
  esac
}

# A native jq.exe under Git Bash writes CRLF line endings; -b keeps them LF.
if is_windows; then
  jq() { command jq -b "$@"; }
fi

sha256_of() { # sha256_of <file> -> lowercase hex digest
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -c1-64
  else
    shasum -a 256 "$1" | cut -c1-64
  fi
}

size_of() { # size_of <file> -> bytes
  wc -c < "$1" | tr -d ' '
}

mode_of() { # mode_of <file> -> permission bits in octal, e.g. 755
  if stat -c %a "$1" >/dev/null 2>&1; then
    stat -c %a "$1"
  else
    stat -f %Lp "$1"
  fi
}

abs_dir() { # abs_dir <existing dir> -> absolute path
  (cd "$1" && pwd)
}

# Field separator for records read with `IFS=$SEP read`. Unlike a tab it is not IFS
# whitespace, so empty fields are kept instead of being merged. jq writes it with
# join("\u001f").
# shellcheck disable=SC2034 # used by the scripts that source this file
SEP=$'\x1f'

# HDiffPatch tools, overridable for tests or non-PATH installs.
HDIFFZ="${HDIFFZ:-hdiffz}"
HPATCHZ="${HPATCHZ:-hpatchz}"
# shellcheck disable=SC2034 # used by the scripts that source this file
HDIFFZ_ARGS="-m-6 -c-lzma2-9-16m"
