#!/usr/bin/env bash
# Enable the repo's tracked git hooks (.githooks/) for this clone.
#
# Git never runs hooks straight from a checkout: it only looks in .git/hooks (untracked) or in
# the directory named by `core.hooksPath`. This script points `core.hooksPath` at .githooks so
# the versioned hooks run, and marks them executable. Run it once per clone:
#
#     bash scripts/install-git-hooks.sh
#
# Undo with:  git config --unset core.hooksPath
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

git config core.hooksPath .githooks
chmod +x .githooks/* 2>/dev/null || true

echo "Git hooks enabled: core.hooksPath -> .githooks"
for hook in .githooks/*; do
    echo "  $(basename "$hook")"
done

if ! command -v "${CLAUDE_BIN:-claude}" >/dev/null 2>&1; then
    echo "Note: Claude Code CLI not found on PATH. The pre-commit comment clean-up will skip until it is installed." >&2
fi
