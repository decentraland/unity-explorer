#!/usr/bin/env bash
# clean-up-excessive-ai-comments.sh
#
# Uses Claude Code to trim excessive comments introduced by the current PR and to enforce the
# repo rule that a comment must only describe the code it annotates, never the behaviour of
# callers or other scopes it does not own (CLAUDE.md § Anti-Patterns, "Comments that narrate
# caller/external behavior"). Only comments added or changed on this branch are in scope;
# Claude is told not to touch code, formatting, or pre-existing comments.
#
# Usage:
#   bash scripts/lint/clean-up-excessive-ai-comments.sh [MODE] [OPTIONS]
#
# Modes:
#   interactive   Run Claude, show its proposed edits as a diff, then ask what to do:
#                 keep them, discard them, or abort. Default when stdin is a terminal.
#                 This is the mode the pre-commit hook uses (.githooks/pre-commit).
#   go            Launch-and-go: run Claude and keep its edits without asking. Default when
#                 stdin is not a terminal. For scripted use (agents, CI, batch clean-ups).
#
# Options:
#   --base <ref>        Ref the PR is compared against. Default: origin/dev, falling back to dev.
#   --staged            Restrict the scope to files staged for the current commit and skip
#                       entirely when the staged diff adds no comment lines. Set by the hook.
#                       Without it, every C# file changed on the branch (committed, staged or
#                       unstaged) is in scope.
#   --model <model>     Model passed to Claude Code (default: Claude Code's own default).
#   --prompt-file <f>   Replace the built-in prompt with the contents of <f>.
#   -h, --help          Print this header and exit.
#
# Environment:
#   SKIP_AI_COMMENT_CLEANUP=1   Do nothing and exit 0 (the hook also honours `git commit --no-verify`).
#   CLAUDE_BIN                  Path to the claude executable (default: `claude` on PATH).
#
# Exit codes:
#   0  finished (edits kept, edits discarded, or nothing to do); the hook lets the commit proceed
#   1  usage or environment error (not inside a git repo, bad arguments)
#   2  interactive mode: the user chose to abort; the hook stops the commit
#
# Claude Code is optional: when it is not installed the script prints an install hint and exits 0
# so a missing tool never blocks a commit.
set -uo pipefail

usage() { sed -n '2,/^set -uo pipefail/{ /^set -uo pipefail/d; s/^# \{0,1\}//; p; }' "$0"; }

[ "${SKIP_AI_COMMENT_CLEANUP:-0}" = "1" ] && exit 0

mode=""
base=""
staged=0
model=""
prompt_file=""

while [ $# -gt 0 ]; do
    case "$1" in
        interactive|go) mode="$1" ;;
        --base) base="${2:?--base needs a ref}"; shift ;;
        --staged) staged=1 ;;
        --model) model="${2:?--model needs a value}"; shift ;;
        --prompt-file) prompt_file="${2:?--prompt-file needs a path}"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "clean-up-excessive-ai-comments: unknown argument '$1'" >&2; usage >&2; exit 1 ;;
    esac
    shift
done

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "clean-up-excessive-ai-comments: not inside a git repository" >&2; exit 1; }
cd "$ROOT"

if [ -z "$mode" ]; then
    if [ -t 0 ]; then mode="interactive"; else mode="go"; fi
fi

CLAUDE_BIN="${CLAUDE_BIN:-claude}"
if ! command -v "$CLAUDE_BIN" >/dev/null 2>&1; then
    {
        echo "clean-up-excessive-ai-comments: Claude Code CLI not found - skipping the comment clean-up."
        echo "Install it from https://docs.claude.com/en/docs/claude-code or set CLAUDE_BIN."
    } >&2
    exit 0
fi

# Resolve the base ref the PR is compared against.
if [ -z "$base" ]; then
    for candidate in origin/dev dev; do
        if git rev-parse --verify --quiet "$candidate^{commit}" >/dev/null; then base="$candidate"; break; fi
    done
fi
[ -z "$base" ] && { echo "clean-up-excessive-ai-comments: cannot resolve a base ref, pass --base <ref>" >&2; exit 1; }
merge_base="$(git merge-base "$base" HEAD 2>/dev/null)" || { echo "clean-up-excessive-ai-comments: no merge base with '$base'" >&2; exit 1; }

# Scope: C# files introduced or changed by the PR.
if [ "$staged" -eq 1 ]; then
    # Only worth running when the commit itself adds comment lines.
    if ! git diff --cached -U0 --diff-filter=ACM -- '*.cs' | grep -Eq '^\+\s*(//|/\*|\*)'; then
        exit 0
    fi
    files="$(git diff --cached --name-only --diff-filter=ACM -- '*.cs')"
else
    files="$(
        {
            git diff --name-only --diff-filter=ACM "$merge_base" -- '*.cs'
            git diff --name-only --diff-filter=ACM --cached -- '*.cs'
        } | sort -u
    )"
fi
files="$(printf '%s\n' "$files" | sed '/^$/d' | while IFS= read -r f; do [ -f "$f" ] && printf '%s\n' "$f"; done)"
[ -z "$files" ] && exit 0

# Build the prompt.
if [ -n "$prompt_file" ]; then
    [ -f "$prompt_file" ] || { echo "clean-up-excessive-ai-comments: prompt file not found: $prompt_file" >&2; exit 1; }
    prompt="$(cat "$prompt_file")"
else
    prompt="$(cat <<EOF
Clean up excessive comments that were introduced by the current PR.

Scope: only comment lines that this PR added or changed, in the files listed below. The PR diff is
\`git diff $merge_base -- <file>\` (the merge base with $base). Everything that pre-dates the PR is out of scope.

Enforce the repo rule from CLAUDE.md § Anti-Patterns: a comment must state only what the annotated
code itself does or guarantees. Remove or rewrite any comment that explains the behaviour of a scope
it does not own - callers, upper layers, other systems, or what "will" happen elsewhere.

Also apply:
- Delete comments that merely restate the code next to them.
- Collapse multi-line narration into one concise line; keep only the non-obvious fact (an invariant,
  a workaround, an issue reference like #1234).
- Keep XML doc comments on public members, but shorten verbose ones to one summary line.
- In tests keep bare Arrange / Act / Assert markers if the file uses them, without explanations.

Constraints:
- Edit only comment text. Do not change code, identifiers, blank lines outside comments, or formatting.
- Do not touch comments that pre-date the PR, and do not touch files that are not listed.
- Do not stage, commit, or run any git command that modifies state.
- Edit the files in place, then answer with one line per edited file describing what was trimmed,
  or the single word "nothing" if no comment needed a change.

Files:
$(printf '%s\n' "$files" | sed 's/^/- /')
EOF
)"
fi

# Snapshot the in-scope files so Claude's edits can be shown and reverted precisely.
backup="$(mktemp -d)"
trap 'rm -rf "$backup"' EXIT
while IFS= read -r f; do
    mkdir -p "$backup/$(dirname "$f")"
    cp "$f" "$backup/$f"
done <<<"$files"

status_before="$(git status --porcelain)"

echo "clean-up-excessive-ai-comments: asking Claude to review comments in $(printf '%s\n' "$files" | wc -l | tr -d ' ') file(s) against $base..." >&2

claude_args=(-p "$prompt" --permission-mode acceptEdits --output-format text
    --allowedTools "Read" "Edit" "MultiEdit" "Grep" "Glob" "Bash(git diff:*)" "Bash(git merge-base:*)" "Bash(git log:*)")
[ -n "$model" ] && claude_args+=(--model "$model")

summary="$("$CLAUDE_BIN" "${claude_args[@]}" 2>&1)"
rc=$?
if [ $rc -ne 0 ]; then
    echo "clean-up-excessive-ai-comments: claude exited with $rc - leaving files untouched." >&2
    printf '%s\n' "$summary" >&2
    while IFS= read -r f; do cp "$backup/$f" "$f"; done <<<"$files"
    exit 0
fi

changed=()
while IFS= read -r f; do
    cmp -s "$backup/$f" "$f" || changed+=("$f")
done <<<"$files"

# Anything Claude touched outside the scope is reported, never silently kept or reverted.
status_after="$(git status --porcelain)"
if [ "$status_before" != "$status_after" ]; then
    unexpected="$(diff <(printf '%s\n' "$status_before") <(printf '%s\n' "$status_after") | grep '^>' | sed 's/^> ...//' | grep -vxF -f <(printf '%s\n' "$files") || true)"
    [ -n "$unexpected" ] && { echo "clean-up-excessive-ai-comments: WARNING - files changed outside the scope, review them:" >&2; printf '%s\n' "$unexpected" | sed 's/^/  /' >&2; }
fi

if [ ${#changed[@]} -eq 0 ]; then
    echo "clean-up-excessive-ai-comments: no comment changes proposed." >&2
    exit 0
fi

show_diff() {
    for f in "${changed[@]}"; do
        diff -u --label "a/$f" --label "b/$f" "$backup/$f" "$f"
    done
}

restore() { for f in "${changed[@]}"; do cp "$backup/$f" "$f"; done; }

# Re-stage files whose staged content Claude changed so the commit picks the trimmed version up.
restage() {
    [ "$staged" -eq 1 ] || return 0
    for f in "${changed[@]}"; do git add -- "$f"; done
}

if [ "$mode" = "go" ]; then
    echo "clean-up-excessive-ai-comments: Claude trimmed comments in ${#changed[@]} file(s):" >&2
    printf '%s\n' "$summary" >&2
    show_diff >&2
    restage
    exit 0
fi

# Interactive: git hooks run without a terminal on stdin, so talk to the user via /dev/tty.
if [ ! -t 0 ] && [ -e /dev/tty ]; then exec </dev/tty; fi

{
    echo
    echo "clean-up-excessive-ai-comments: Claude proposes these comment changes (${#changed[@]} file(s)):"
    echo
    show_diff
    echo
    printf '%s\n' "$summary"
    echo
} >&2

while :; do
    printf '[k]eep edits and continue, [d]iscard edits and continue, [a]bort commit? [k/d/a] ' >&2
    if ! IFS= read -r answer; then answer="d"; fi
    case "$answer" in
        k|K|"") restage; echo "clean-up-excessive-ai-comments: edits kept." >&2; exit 0 ;;
        d|D) restore; echo "clean-up-excessive-ai-comments: edits discarded." >&2; exit 0 ;;
        a|A) restore; echo "clean-up-excessive-ai-comments: aborted." >&2; exit 2 ;;
        *) ;;
    esac
done
