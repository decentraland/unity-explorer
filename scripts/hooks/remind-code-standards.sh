#!/usr/bin/env bash
# PreToolUse hook: when an Edit/Write/MultiEdit targets a .cs file, remind the model
# (once per session) to invoke the code-standards skill. A skill's full instructions are
# NOT in context until the Skill tool is invoked, so this closes the gap deterministically
# for C# edits. Emits `additionalContext`; it never blocks the tool.
#
# Input:  the PreToolUse hook JSON on stdin.
# Output: on the first .cs edit of a session, a JSON object with hookSpecificOutput
#         .additionalContext; otherwise nothing.
set -uo pipefail

# Never break a tool call: if jq is unavailable, stay silent and let the edit proceed.
command -v jq >/dev/null 2>&1 || exit 0

input="$(cat)"

file_path="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty' 2>/dev/null || true)"

# Only C# files; inert for everything else.
case "$file_path" in
    *.cs) ;;
    *) exit 0 ;;
esac

session_id="$(printf '%s' "$input" | jq -r '.session_id // "unknown"' 2>/dev/null || echo unknown)"
# Strip anything not path-safe: the sentinel name must never escape $TMPDIR.
session_id="${session_id//[^a-zA-Z0-9_-]/}"
[ -n "$session_id" ] || session_id="unknown"
sentinel="${TMPDIR:-/tmp}/claude-code-standards-reminder-${session_id}"

# Fire once per session to avoid per-edit noise.
if [ -e "$sentinel" ]; then
    exit 0
fi
: > "$sentinel" 2>/dev/null || true

reminder="About to edit a C# file. If you have not yet done so this session, invoke Skill(code-standards) BEFORE writing. Its rules (naming, member ordering, nullable, GC, and the anti-patterns distilled from PR review) are NOT in context until the skill is invoked — do not assume they are already loaded."

jq -cn --arg ctx "$reminder" \
    '{hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: $ctx}}'
