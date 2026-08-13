#!/usr/bin/env bash
# Selftest for custom-rules.sh: builds a throwaway git repo from fixtures/ and
# asserts both modes against golden findings. Catches dead rules (pattern edits
# that stop matching), engine regressions (parse_diff, pattern transport), and
# whole-script breakage (awk fatal, bash error) - all of which otherwise exit 0.
#
# Goldens pin `path:line  SEVERITY  rule-id` (not the message text, so message
# rewording doesn't fail the build). Regenerate after an intentional rule
# change with: selftest.sh --regen
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LINTER="$HERE/../custom-rules.sh"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

fail() { echo "selftest FAIL: $1" >&2; exit 1; }
# tr strips CR so goldens compare equal when git checked them out with CRLF
# (Windows working copies); the linter itself always emits LF.
norm() { tr -d '\r' | awk -F'  ' '{ print $1 "  " $2 "  " $3 }'; }
golden() { tr -d '\r' < "$1"; }
commit() { git -C "$work" -c user.email=selftest@local -c user.name=selftest -c commit.gpgsign=false commit -q "$@"; }
run_linter() { (cd "$work" && bash "$LINTER" "$@"); }

cp -R "$HERE/fixtures/." "$work/"
git -C "$work" init -q -b selftest
commit --allow-empty -m base

out="$(run_linter --working-tree)"; rc=$?
if [ "${1:-}" = "--regen" ]; then
    printf '%s\n' "$out" | norm > "$HERE/expected-working-tree.txt"
else
    [ "$rc" -eq 2 ] || fail "working-tree rc=$rc, want 2 (BLOCK findings present)"
    diff <(printf '%s\n' "$out" | norm) <(golden "$HERE/expected-working-tree.txt") >&2 \
        || fail "working-tree findings diverge from golden (intentional rule change? rerun with --regen)"
fi

git -C "$work" add -A
commit -m fixtures
out="$(run_linter --working-tree)"; rc=$?
[ "$rc" -eq 0 ] && [ -z "$out" ] || fail "clean tree rc=$rc out='$out', want silent 0"

printf 'using UnityEngine;\nclass Late { void F() { Debug.Log("l"); } }\n' \
    > "$work/Explorer/Assets/DCL/Feature/Late.cs"                              # new file (A)
printf '        Debug.LogError("appended");\n' \
    >> "$work/Explorer/Assets/DCL/Feature/FooSystem.cs"                        # modified file (M)
git -C "$work" add -A
commit -m late
out="$(run_linter --diff HEAD~1 HEAD)"; rc=$?
if [ "${1:-}" = "--regen" ]; then
    printf '%s\n' "$out" | norm > "$HERE/expected-diff.txt"
    echo "selftest: goldens regenerated - review the diff before committing"
    exit 0
fi
[ "$rc" -eq 2 ] || fail "diff-mode rc=$rc, want 2"
diff <(printf '%s\n' "$out" | norm) <(golden "$HERE/expected-diff.txt") >&2 \
    || fail "diff-mode findings diverge from golden (line numbers test the hunk parser)"

echo "selftest OK"
