#!/usr/bin/env bash
# Functional tests for release-tags.sh against a canned GitHub releases list, and
# check-unpublished.sh against a stubbed gh. No network. Run from anywhere:
#   bash scripts/delta/test-release-tags.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TAGS="$SCRIPT_DIR/release-tags.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# API order (created_at) deliberately differs from published_at order.
cat > "$WORK/releases.json" <<'JSON'
[
  {"tag_name": "v0.188.0-alpha", "draft": true,  "published_at": null},
  {"tag_name": "v0.186.0-alpha", "draft": false, "published_at": "2026-10-08T14:33:06Z"},
  {"tag_name": "avatar-preview-renderer/v3.4.0", "draft": false, "published_at": "2026-10-09T10:00:00Z"},
  {"tag_name": "v0.188.0-rc1", "draft": false, "prerelease": true, "published_at": "2026-10-13T12:00:00Z"},
  {"tag_name": "v0.187.0-alpha", "draft": false, "prerelease": false, "published_at": "2026-10-13T09:00:00Z"},
  {"tag_name": "v0.185.0-alpha", "draft": false, "published_at": "2026-10-06T14:22:59Z"},
  {"tag_name": "v0.184.0-alpha", "draft": false, "published_at": "2026-10-01T19:18:45Z"},
  {"tag_name": "v0.183.0-alpha", "draft": false, "published_at": "2026-09-29T15:45:19Z"}
]
JSON

FAILURES=0
PASSES=0
# expect <name> <expected stdout> <args...>
expect() {
  local name="$1" want="$2" got code=0
  shift 2
  got=$(bash "$TAGS" "$@" < "${RELEASES:-$WORK/releases.json}" 2> "$WORK/stderr") || code=$?
  if [ "$got" = "$want" ] && [ "$code" -eq 0 ]; then
    echo "  ok   $name"
    PASSES=$((PASSES + 1))
  else
    echo "  FAIL $name: exit=$code"
    echo "       want: $(echo "$want" | tr '\n' ' ')"
    echo "       got:  $(echo "$got" | tr '\n' ' ')"
    sed 's/^/       /' "$WORK/stderr"
    FAILURES=$((FAILURES + 1))
  fi
}
expect_usage_error() {
  local name="$1" code=0
  shift
  bash "$TAGS" "$@" < "${RELEASES:-$WORK/releases.json}" > /dev/null 2>&1 || code=$?
  if [ "$code" -ne 0 ]; then echo "  ok   $name"; PASSES=$((PASSES + 1)); else echo "  FAIL $name: exit 0"; FAILURES=$((FAILURES + 1)); fi
}

echo "release-tags.sh bases"
expect "newest published first, drafts, pre-releases and renderer tags skipped" \
  "$(printf 'v0.187.0-alpha\nv0.186.0-alpha\nv0.185.0-alpha')" bases 3 v0.188.0-alpha
expect "excludes the current tag" \
  "$(printf 'v0.186.0-alpha\nv0.185.0-alpha')" bases 2 v0.187.0-alpha
expect "fewer releases than requested" \
  "$(printf 'v0.187.0-alpha\nv0.186.0-alpha\nv0.185.0-alpha\nv0.184.0-alpha\nv0.183.0-alpha')" bases 10 v0.188.0-alpha
expect "zero bases" "" bases 0 v0.188.0-alpha

echo "release-tags.sh prune"
expect "keeps the two newest, prunes the next window" \
  "$(printf 'v0.185.0-alpha\nv0.184.0-alpha')" prune 2 2
expect "window past the end" "v0.183.0-alpha" prune 4 5
expect "nothing to prune" "" prune 10 3

# v0.187 was published after v0.188: prune must still keep v0.189 and v0.188.
cat > "$WORK/out-of-order.json" <<'JSON'
[
  {"tag_name": "v0.189.0-alpha", "draft": false, "published_at": "2026-10-20T10:00:00Z"},
  {"tag_name": "v0.187.0-alpha", "draft": false, "published_at": "2026-10-16T10:00:00Z"},
  {"tag_name": "v0.188.0-alpha", "draft": false, "published_at": "2026-10-15T10:00:00Z"},
  {"tag_name": "v0.186.10-alpha", "draft": false, "published_at": "2026-10-08T10:00:00Z"},
  {"tag_name": "v0.186.9-alpha", "draft": false, "published_at": "2026-10-09T10:00:00Z"}
]
JSON
RELEASES="$WORK/out-of-order.json" expect "ranks by version, not publish time"   "$(printf 'v0.187.0-alpha
v0.186.10-alpha
v0.186.9-alpha')" prune 2 5
RELEASES="$WORK/out-of-order.json" expect "bases still follow publish time"   "$(printf 'v0.187.0-alpha
v0.188.0-alpha')" bases 2 v0.189.0-alpha
echo '[{"tag_name": "vNext", "draft": false, "published_at": "2026-10-20T10:00:00Z"}]' > "$WORK/malformed.json"
RELEASES="$WORK/malformed.json" expect_usage_error "fails on a malformed v* tag instead of pruning" prune 2 5

echo "usage"
expect_usage_error "unknown mode" list
expect_usage_error "non-numeric count" bases two v1
expect_usage_error "missing window" prune 2

echo "check-unpublished.sh"
mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'STUB'
#!/usr/bin/env bash
case "$GH_STUB" in
  missing)   echo "gh: Not Found (HTTP 404)" >&2; exit 1 ;;
  draft)     echo true ;;
  published) echo false ;;
  outage)    echo "gh: Bad Gateway (HTTP 502)" >&2; exit 1 ;;
esac
STUB
chmod +x "$WORK/bin/gh"
# unpublished <name> <gh stub mode> <expected exit: 0 or 1> [tag]
unpublished() {
  local code=0
  PATH="$WORK/bin:$PATH" GH_STUB="$2" GITHUB_REPOSITORY=owner/repo     bash "$SCRIPT_DIR/check-unpublished.sh" "${4-v1.0.0-alpha}" > /dev/null 2>&1 || code=$?
  if [ "$code" -eq "$3" ]; then echo "  ok   $1"; PASSES=$((PASSES + 1)); else echo "  FAIL $1: exit=$code"; FAILURES=$((FAILURES + 1)); fi
}
unpublished "no release (404) is unpublished" missing 0
unpublished "a draft is unpublished" draft 0
unpublished "a published release is refused" published 1
unpublished "an API failure is refused, not treated as unpublished" outage 1
unpublished "an empty tag is refused" missing 1 ""
unpublished "a non-release tag is refused" missing 1 "avatar-preview-renderer/v3.4.0"
unpublished "a tag with .. is refused" missing 1 "v1.0.0-../x"
unpublished "a tag with trailing characters is refused" missing 1 "v9.9.9?x"
unpublished "a tag with a suffix is accepted" missing 0 "v0.187.0-alpha"

echo
echo "$PASSES passed, $FAILURES failed"
[ "$FAILURES" -eq 0 ]
