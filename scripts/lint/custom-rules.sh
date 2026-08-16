#!/usr/bin/env bash
# Deterministic project-rule linter: regex rules from CLAUDE.md / .claude/skills,
# enforced ONLY on lines added in the diff under inspection — pre-existing
# violations never block (same ratchet philosophy as the ReSharper warning count).
#
# Usage:
#   custom-rules.sh --working-tree            # added lines vs HEAD + untracked files (Stop hook)
#   custom-rules.sh --diff <base> <head>      # added lines in a commit range (CI)
#
# Output: one finding per line  ->  <path>:<line>  <severity>  <rule-id>  <message>
# Exit codes: 0 = clean (or only WARN findings); 2 = BLOCK findings present;
#             3 = a rule pattern is broken (never silently passes).
#
# Escape hatch: a finding is suppressed when its line carries a trailing
#   // lint-ignore: <rule-id>[, <rule-id>...]
# comment naming that rule. Use it for the rare sanctioned exception (e.g. an
# #if UNITY_EDITOR-guarded Debug.Log); the suppression stays visible in the
# diff for reviewers to challenge.
#
# Patterns are POSIX EREs evaluated by awk as dynamic regexes: no \b/\y word
# boundaries (write (^|[^[:alnum:]_.]) guards instead) so they behave the same
# under gawk, mawk, and BSD awk.
set -uo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

mode="${1:?usage: custom-rules.sh --working-tree | --diff <base> <head>}"

# ---------------------------------------------------------------------------
# Rules. rule <severity> <id> <include-path-ERE> <exclude-path-ERE> <line-ERE> <message> [anti-ERE]
#   severity  BLOCK (exit 2) or WARN (printed, never blocks)
#   include   rule applies only to paths matching (empty = every .cs file)
#   exclude   rule never applies to paths matching (empty = no exclusions)
#   anti      optional: a line matching this is NOT a finding even when the
#             pattern matches (carves a sanctioned idiom out of a broad pattern)
# Keep each message pointing at the rule's source doc so findings explain themselves.
# ---------------------------------------------------------------------------
declare -a R_SEV=() R_ID=() R_INC=() R_EXC=() R_PAT=() R_MSG=() R_ANT=()
rule() { R_SEV+=("$1"); R_ID+=("$2"); R_INC+=("$3"); R_EXC+=("$4"); R_PAT+=("$5"); R_MSG+=("$6"); R_ANT+=("${7:-}"); }

EXCLUDE_NON_PROD='(^|/)([A-Za-z]*Tests?|Editor|Plugins|Demo)/|Editor\.cs$|Should\.cs$|Tests?\.cs$'

# ReportsHandling is the ReportHub implementation itself - its own error/ANR-dump
# paths cannot log through it.
rule BLOCK debug-log '' "$EXCLUDE_NON_PROD|(^|/)ReportsHandling/" \
    '(^|[^[:alnum:]_])(UnityEngine\.)?Debug\.(Log|LogError|LogWarning|LogException|LogFormat|LogErrorFormat|LogWarningFormat|LogAssertion)\(' \
    'Use ReportHub instead of Debug.Log (CLAUDE.md; diagnostics-and-logging skill)'

# instance. Tests constructing the two sanctioned proxies are exempt.
rule BLOCK object-proxy '' "$EXCLUDE_NON_PROD" \
    '(^|[^[:alnum:]_])new +ObjectProxy<' \
    'ObjectProxy is an anti-pattern - pick a recipe from docs/architecture-overview.md § Deferred dependencies (CLAUDE.md)'

rule BLOCK checknamespace-suppression '' '' \
    'ReSharper +disable( +once)? +CheckNamespace' \
    'Never suppress CheckNamespace - fix the namespace instead (docs/code-style-guidelines.md § Namespaces)'

rule BLOCK linq-in-system 'System\.cs$' "$EXCLUDE_NON_PROD" \
    'using +System\.Linq' \
    'No LINQ in ECS systems - Update() must be allocation-free (CLAUDE.md § Performance Constraints)'

rule BLOCK camera-main '' "$EXCLUDE_NON_PROD" \
    '(^|[^[:alnum:]_])(UnityEngine\.)?Camera\.main($|[^[:alnum:]_])' \
    'Use the ECS camera singleton (TryGet in a system), not Camera.main (CLAUDE.md § Anti-Patterns)'

# NOTE: no null!/default! rule on purpose - `[field: SerializeField] ... = null!`
# is the sanctioned inspector-assigned idiom and the attribute sits on the previous
# line, invisible to a per-line check (310 hits over 200 commits, all legitimate).

rule BLOCK nullable-disable '' '' \
    '^[ \t]*#nullable +disable' \
    'Do not add #nullable disable - annotate properly instead (code-standards skill)'

rule BLOCK interface-prefix '' '' \
    '(public|internal) +(partial +)?interface +([^I[:space:]]|I[a-z_])' \
    'Interface names must start with I (docs/code-style-guidelines.md § Naming Conventions)'

rule BLOCK foreign-test-framework '' '' \
    'using +(Moq|Xunit|FluentAssertions|FakeItEasy)( *;|\.)' \
    'Tests use NUnit + NSubstitute only (docs/standards.md § Tests)'

rule BLOCK async-void-in-tests '(^|/)Tests?/|Should\.cs$|Tests\.cs$' '' \
    '(^|[^[:alnum:]_])async +void +' \
    'Tests must use async Task, not async void (testing-infrastructure skill)'

rule BLOCK explorer-flag-prefix '' '' \
    'IsEnabled *\( *"explorer-' \
    'Flag names drop the explorer- prefix in code (feature-flags-and-configuration skill)'

rule BLOCK tryaddwidget-unguarded '' '' \
    'TryAddWidget *\([^)]*\) *\.' \
    'TryAddWidget returns null when debug is disabled - chain with ?. (debug-widget skill)'

rule BLOCK thread-affinity-scene-runtime '(^|/)(SceneRunner|SceneRuntime|CrdtEcsBridge)/' "$EXCLUDE_NON_PROD" \
    '\[ThreadStatic\]|ThreadLocal<|Thread\.CurrentThread' \
    'No thread affinity in scene-runtime code - it hops threads at every await (scene-runtime-and-crdt skill)'

# WARN: some overloads legitimately take the message first.
rule WARN reporthub-string-category '' '' \
    'ReportHub\.(Log|LogWarning|LogError|LogException) *\( *"' \
    'Pass a ReportCategory constant to ReportHub, not a string literal (diagnostics-and-logging skill)'

rule WARN world-query '' "$EXCLUDE_NON_PROD" \
    '(^|[^[:alnum:]_])[Ww]orld\.Query *\(' \
    'World.Query is a last resort - prefer source-generated [Query] (CLAUDE.md § Querying)'

rule WARN raw-http '' "(^|/)WebRequests/|$EXCLUDE_NON_PROD" \
    'new +(HttpClient|WebClient) *\(|UnityWebRequest\.(Get|Post|Put|Delete|Head) *\(|new +UnityWebRequest *\(' \
    'Route HTTP through IWebRequestController (web-requests skill)'

rule WARN nameof-argument-exception '' '' \
    'new +(ArgumentNullException|ArgumentOutOfRangeException) *\( *"' \
    'Use nameof(...) for the parameter name (docs/code-style-guidelines.md)'

rule BLOCK nullable-local '' "$EXCLUDE_NON_PROD" \
    '^[ \t]*[A-Za-z_][A-Za-z0-9_.]*(<[^;={}]*>)?\? +[a-z_][A-Za-z0-9_]* *(=[^;]*)?;' \
    'Local variables must not be nullable - use pattern matching (is not { } x) to bind a non-nullable local (CLAUDE.md § Anti-Patterns)'

# The null-forgiving operator lies to the compiler about nullability. The anti
# carves out (a) the sanctioned "= null!"/"= default!" initializer idiom
# (wire-format DTOs, [SerializeField]-assigned members, generic defaults) and
# (b) the three split-phase-initialization idioms the frameworks force -
# viewInstance! (MVC view created after the controller), Instance! (late-init
# singletons), World! (Arch ECS field assigned during system wiring) - where
# no NRT-clean rewrite exists.
rule BLOCK null-forgiving-suppression '' "$EXCLUDE_NON_PROD" \
    '! *[.;,)]' \
    'Never use the null-forgiving ! to silence nullability - restructure so the value is provably non-null (CLAUDE.md § Anti-Patterns)' \
    '(null|default) *! *[.;,)]|(viewInstance|(^|[^[:alnum:]_])(Instance|World))! *[.;,)]'

rule BLOCK world-get-copy '' "$EXCLUDE_NON_PROD" \
    '(^|[ \t(])var +[a-z_][A-Za-z0-9_]* *= *([A-Za-z_][A-Za-z0-9_.]*)?[Ww]orld\.Get<' \
    'Use ref var x = ref World.Get<T>() - a plain var copies the component and mutations are silently lost (CLAUDE.md § Safe Component Mutation)' \
    'ref +(readonly +)?var'

rule BLOCK stringbuilder-interpolation '' "$EXCLUDE_NON_PROD" \
    '\.Append(Line)? *\( *\$"' \
    'Do not interpolate into StringBuilder - use the typed Append overloads (review rule, PR #9339; docs/standards.md § Memory)'

rule WARN caller-narrating-comment '' "$EXCLUDE_NON_PROD" \
    '// .*so (that )?(the )?(caller|consumer|upper layer|client)s?[^[:alnum:]]' \
    'Comments must not narrate caller/external behavior - state only what this code does (CLAUDE.md § Anti-Patterns)'

rule WARN contextmenu '' "$EXCLUDE_NON_PROD" \
    '\[ContextMenu' \
    'Prefer [Button] from EasyButtons over [ContextMenu] (docs/code-style-guidelines.md § Attribute Usages)'

# Rules below are mined from the 6-month human review record (2026-02..2026-08).
# Each message cites the PR whose review stated the rule.

# Raw ConcurrentDictionary silently fails the WebGL build; DCLConcurrentDictionary
# (Utility.Multithreading) is the platform-safe stand-in. Multithreading/ dirs are
# the threading-primitive layer itself and legitimately touch the raw type.
rule BLOCK concurrent-dictionary '' "$EXCLUDE_NON_PROD|(^|/)Multithreading/" \
    '(^|[^[:alnum:]_])(System\.Collections\.Concurrent\.)?ConcurrentDictionary<' \
    'ConcurrentDictionary is prohibited (WebGL) - use DCLConcurrentDictionary (review rule, PR #7595)'

rule WARN hardcoded-dcl-url '\.cs$' "$EXCLUDE_NON_PROD|DecentralandUrl|(^|/)Playground|StressTest" \
    '"https?://[a-z0-9.-]+\.decentraland\.(org|zone|today)' \
    'Resolve decentraland URLs through DecentralandUrlsSource, never hardcode (review rule, PR #8393)'

rule WARN tolower-compare '' "$EXCLUDE_NON_PROD" \
    '\.To(Lower|Upper)(Invariant)? *\( *\) *[=!]=|[=!]= *[A-Za-z_][A-Za-z0-9_.]*\.To(Lower|Upper)(Invariant)? *\( *\)' \
    'Compare with string.Equals(..., StringComparison.OrdinalIgnoreCase), not ToLower/ToUpper round-trips (review idiom bar)'

rule WARN path-concat-unity '' "$EXCLUDE_NON_PROD" \
    '(persistentDataPath|dataPath|streamingAssetsPath|temporaryCachePath) *\+ *"' \
    'Use Path.Combine on Unity path properties, not string concatenation (review idiom bar)'

# Single-line form only; multi-line empty catches are invisible to a per-line
# check and stay with reviewers. Any line naming OperationCanceledException is
# carved out - swallowing OCE is the sanctioned cancellation pattern.
rule WARN empty-catch '' "$EXCLUDE_NON_PROD" \
    'catch[ \t]*(\([^)]*\))?[ \t]*[{][ \t]*[}]' \
    'Empty catch silently swallows context - catch the specific exception and act, or let it propagate (review rule, PR #7347)' \
    'OperationCanceledException'

rule WARN ai-comment-opener '' "$EXCLUDE_NON_PROD" \
    '// *(Note that|This ensures|This allows|We use this to|Importantly,|As mentioned)' \
    'Reads as AI narration - state the invariant or delete the comment (review rule, PRs #9043 #9747; CLAUDE.md anti-patterns)'

rule BLOCK manifest-branch-ref 'Explorer/Packages/manifest\.json$' '' \
    'github\.com/[^"]*#(fix|feat|chore|refactor|wip)/' \
    'Feature-branch ref left in manifest.json - repin to the mainline ref before review (review rule: PR scope discipline)'

# Emit added lines as: <path>\t<line-number>\t<line-text>
parse_diff() {
    awk '
        /^\+\+\+ b\// { path = substr($0, 7); sub(/\t$/, "", path); next }
        /^@@ /        { split($0, a, "+"); split(a[2], b, /[ ,]/); line = b[1]; next }
        /^\+/         { if (path != "") printf "%s\t%d\t%s\n", path, line, substr($0, 2); line++ }
    '
}

# The -c/-- flags pin the exact diff format parse_diff expects, immune to user
# gitconfig (diff.noprefix, diff.mnemonicPrefix, diff.external, core.quotePath).
GIT_DIFF=(git -c core.quotePath=off -c diff.noprefix=false diff
    --no-ext-diff --src-prefix=a/ --dst-prefix=b/ -U0 --no-color --diff-filter=ACMR)

# The selftest fixture corpus is violations on purpose - never lint it.
# Assets/Plugins and Packages are vendored/plugin-layer code these project
# rules don't govern (mirrors filter-warnings.sh's ownership boundary).
# Analyzers/ is Roslyn-host code with its own idioms (nullable locals are
# idiomatic there) - governed by its own test suite, not these Unity rules.
# manifest.json rides along for the manifest-branch-ref rule; the Packages
# exclude is one level down so the top-level manifest stays linted while
# vendored package contents stay out.
PATHSPEC=('*.cs'
    'Explorer/Packages/manifest.json'
    ':(exclude)scripts/lint/tests/**'
    ':(exclude)Analyzers/**'
    ':(exclude)Explorer/Assets/Plugins/**'
    ':(exclude)Explorer/Packages/*/**')

added_lines() {
    case "$mode" in
        --working-tree)
            "${GIT_DIFF[@]}" HEAD -- "${PATHSPEC[@]}" 2>/dev/null | parse_diff
            # untracked .cs files: every line counts as added
            git -c core.quotePath=off ls-files --others --exclude-standard -- "${PATHSPEC[@]}" 2>/dev/null |
            while IFS= read -r f; do
                [ -f "$f" ] && F="$f" awk '{ printf "%s\t%d\t%s\n", ENVIRON["F"], NR, $0 }' < "$f"
            done
            ;;
        --diff)
            local base="${2:?--diff needs <base> <head>}" head="${3:?--diff needs <base> <head>}"
            "${GIT_DIFF[@]}" "$base" "$head" -- "${PATHSPEC[@]}" 2>/dev/null | parse_diff
            ;;
        *)
            echo "custom-rules: unknown mode '$mode'" >&2
            exit 1
            ;;
    esac
}

# Apply every rule to every added line. A cheap grep -E pass narrows each rule
# to candidate lines (the C regex engine is ~200x faster than awk dynamic
# regexes on big diffs); awk then re-verifies the exact pattern against the
# text column alone and applies the path include/exclude. Patterns reach awk
# through the environment (never -v, which escape-processes backslashes).
# A rule whose pattern fails to compile exits 3 - never a silent pass.
main() {
    local tmp found i cpat
    tmp="$(mktemp)"; found="$(mktemp)"
    trap 'rm -f "$tmp" "$found"' EXIT

    added_lines "$@" > "$tmp"
    [ -s "$tmp" ] || exit 0

    for i in "${!R_ID[@]}"; do
        # Candidate pre-filter over the whole TSV line. A leading ^ can never
        # match after the path\tline\t prefix, so strip it for the candidate
        # pass - the exact pattern is still enforced by awk below. (^|...)
        # groups degrade gracefully: the other branch matches the tab.
        cpat="${R_PAT[$i]#^}"
        printf 'probe\n' | grep -E -- "$cpat" >/dev/null 2>&1
        if [ $? -gt 1 ]; then
            echo "custom-rules: rule '${R_ID[$i]}' has an invalid pattern - refusing to pass" >&2
            exit 3
        fi
        { grep -E -- "$cpat" "$tmp" || true; } |
        INC="${R_INC[$i]}" EXC="${R_EXC[$i]}" PAT="${R_PAT[$i]}" ANT="${R_ANT[$i]}" \
        SEV="${R_SEV[$i]}" ID="${R_ID[$i]}" MSG="${R_MSG[$i]}" \
        awk -F'\t' '
            BEGIN {
                inc = ENVIRON["INC"]; exc = ENVIRON["EXC"]; pat = ENVIRON["PAT"]; ant = ENVIRON["ANT"]
            }
            {
                path = $1; ln = $2
                text = $0; sub(/^[^\t]*\t[^\t]*\t/, "", text)
                if (inc != "" && path !~ inc) next
                if (exc != "" && path ~ exc) next
                if (text !~ pat) next
                if (ant != "" && text ~ ant) next
                # same-line escape hatch: // lint-ignore: rule-a, rule-b
                if (text ~ ("lint-ignore:[ a-z0-9,-]*" ENVIRON["ID"] "([^a-z0-9-]|$)")) next
                printf "%s:%s  %s  %s  %s\n", path, ln, ENVIRON["SEV"], ENVIRON["ID"], ENVIRON["MSG"]
            }
        ' >> "$found" || {
            echo "custom-rules: rule '${R_ID[$i]}' failed to evaluate - refusing to pass" >&2
            exit 3
        }
    done

    [ -s "$found" ] || exit 0
    sort -t: -k1,1 -k2,2n "$found"
    # grep -q on a file, not a pipe: a SIGPIPE'd writer under pipefail once
    # turned BLOCK findings into exit 0 here.
    grep -q '  BLOCK  ' "$found" && exit 2
    exit 0
}

main "$@"
