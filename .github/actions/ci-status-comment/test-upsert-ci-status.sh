#!/usr/bin/env bash
# Functional tests for upsert-ci-status.sh against a stubbed gh whose comment
# store is a JSON file — no network, no repo. Run from anywhere:
#   bash .github/actions/ci-status-comment/test-upsert-ci-status.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UPSERT="$SCRIPT_DIR/upsert-ci-status.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# --- gh stub ----------------------------------------------------------------
# Supports exactly the calls the script makes; comments live in $STORE as a
# JSON array of {id, user:{login}, body}.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
exec python3 "$GH_STUB_PY" "$@"
STUB
chmod +x "$WORK/bin/gh"
cat > "$WORK/gh-stub.py" <<'PY'
import json, os, sys

store = os.environ['STORE']

def load():
    with open(store) as f:
        return json.load(f)

def save(comments):
    with open(store, 'w') as f:
        json.dump(comments, f)

args = sys.argv[1:]
if args[0] != 'api':
    sys.exit(f'gh stub: unsupported subcommand {args[0]}')
args = args[1:]

method = 'GET'
if '-X' in args:
    i = args.index('-X')
    method = args[i + 1]
    del args[i:i + 2]
read_stdin = '--input' in args
path = next(a for a in args if a.startswith('/'))

comments = load()
if method == 'GET':
    # --paginate --slurp shape: array of pages.
    print(json.dumps([comments]))
elif method == 'POST':
    body = json.load(sys.stdin)['body']
    new_id = max([c['id'] for c in comments], default=0) + 1
    comments.append({'id': new_id, 'user': {'login': 'github-actions[bot]'}, 'body': body})
    save(comments)
    print(json.dumps({'id': new_id}))
elif method == 'PATCH':
    cid = int(path.rsplit('/', 1)[1])
    body = json.load(sys.stdin)['body']
    for c in comments:
        if c['id'] == cid:
            c['body'] = body
    save(comments)
    print(json.dumps({'id': cid}))
elif method == 'DELETE':
    cid = int(path.rsplit('/', 1)[1])
    save([c for c in comments if c['id'] != cid])
PY

export PATH="$WORK/bin:$PATH"
export GH_STUB_PY="$WORK/gh-stub.py"
export STORE="$WORK/comments.json"
export REPO="example/repo" PR_NUMBER="1" GITHUB_TOKEN="stub"

FAILED=0
fail() { echo "FAIL: $1"; FAILED=1; }
pass() { echo "ok: $1"; }

reset_store() { echo "${1:-[]}" > "$STORE"; }

body_of() { python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))[int(sys.argv[2])]["body"])' "$STORE" "${1:-0}"; }
count() { python3 -c 'import json,sys; print(len(json.load(open(sys.argv[1]))))' "$STORE"; }

run_upsert() { (cd "$WORK" && SECTION="$1" SECTION_BODY="$2" bash "$UPSERT"); }

# --- 1. create from skeleton -------------------------------------------------
reset_store
run_upsert build "BUILD-CONTENT" >/dev/null
[ "$(count)" = 1 ] || fail "create: expected 1 comment"
BODY="$(body_of)"
grep -q 'BUILD-CONTENT' <<< "$BODY" || fail "create: build content missing"
grep -q '<!-- ci:performance:start -->' <<< "$BODY" || fail "create: performance fence missing"
grep -q '🚦 CI Status' <<< "$BODY" || fail "create: emoji header missing"
grep -q 'decentraland_256x256' <<< "$BODY" && fail "create: retired logo header present"
pass "create seeds skeleton with all sections"

# --- 2. section update preserves the others ---------------------------------
run_upsert tests "TESTS-CONTENT" >/dev/null
BODY="$(body_of)"
grep -q 'BUILD-CONTENT' <<< "$BODY" || fail "update: build content lost"
grep -q 'TESTS-CONTENT' <<< "$BODY" || fail "update: tests content missing"
pass "section update preserves other sections"

# --- 3. missing fence appended, others intact --------------------------------
reset_store "$(python3 - <<'PY'
import json
body = ('<!-- ci-status -->\n### <picture><img src="https://ui.decentraland.org/decentraland_256x256.png"'
        ' width="30" alt="Decentraland"></picture> CI Status\n'
        "<!-- ci:build:start -->\nOLD-BUILD\n<!-- ci:build:end -->")
print(json.dumps([{"id": 5, "user": {"login": "github-actions[bot]"}, "body": body}]))
PY
)"
run_upsert automation "AUTO-CONTENT" >/dev/null
BODY="$(body_of)"
grep -q 'OLD-BUILD' <<< "$BODY" || fail "append: existing section wiped"
grep -q 'AUTO-CONTENT' <<< "$BODY" || fail "append: new section missing"
grep -q '🚦 CI Status' <<< "$BODY" || fail "append: logo header not migrated"
grep -q 'decentraland_256x256' <<< "$BODY" && fail "append: retired logo header still present"
pass "missing fence appended + header migrated"

# --- 4. marker-shaped body lines are stripped --------------------------------
reset_store
run_upsert build "$(printf 'SAFE\n<!-- ci:lint:start -->\nALSO-SAFE')" >/dev/null
BODY="$(body_of)"
[ "$(grep -c '<!-- ci:lint:start -->' <<< "$BODY")" = 1 ] || fail "strip: injected fence survived"
grep -q 'ALSO-SAFE' <<< "$BODY" || fail "strip: legitimate line lost"
pass "marker-shaped body lines stripped"

# --- 5. duplicate GC keeps the oldest ----------------------------------------
reset_store "$(python3 - <<'PY'
import json
mk = lambda i: {"id": i, "user": {"login": "github-actions[bot]"},
                "body": "<!-- ci-status -->\nhdr\n<!-- ci:build:start -->\nB%d\n<!-- ci:build:end -->" % i}
print(json.dumps([mk(3), mk(9)]))
PY
)"
run_upsert build "DEDUPED" >/dev/null
[ "$(count)" = 1 ] || fail "gc: duplicate not deleted"
grep -q 'DEDUPED' <<< "$(body_of)" || fail "gc: content missing on survivor"
pass "duplicate collapse keeps one comment with the write"

# --- 6. NO_CREATE exits 3 without creating -----------------------------------
reset_store
set +e
(cd "$WORK" && SECTION=performance SECTION_BODY=X NO_CREATE=1 bash "$UPSERT") >/dev/null 2>&1
RC=$?
set -e
[ "$RC" = 3 ] || fail "no-create: expected exit 3, got $RC"
[ "$(count)" = 0 ] || fail "no-create: comment was created"
pass "NO_CREATE exits 3, creates nothing"

# --- 7. unknown section exits 2 ----------------------------------------------
set +e
(cd "$WORK" && SECTION=bogus SECTION_BODY=X bash "$UPSERT") >/dev/null 2>&1
RC=$?
set -e
[ "$RC" = 2 ] || fail "allowlist: expected exit 2, got $RC"
pass "unknown section exits 2"

# --- 8. oversized body truncates and re-closes constructs --------------------
reset_store
BIG="$WORK/big-body.md"
{
  echo '<details><summary>big</summary>'
  echo '```'
  for i in $(seq 1 3000); do echo "line $i of filler to overflow the cap"; done
} > "$BIG"
(cd "$WORK" && SECTION=tests SECTION_BODY= SECTION_BODY_FILE="$BIG" bash "$UPSERT") >/dev/null
BODY="$(body_of)"
grep -q 'truncated' <<< "$BODY" || fail "truncate: no truncation note"
[ "$(( $(grep -c '^```' <<< "$BODY") % 2 ))" = 0 ] || fail "truncate: unbalanced code fence"
pass "oversized body truncated with constructs closed"

[ "$FAILED" = 0 ] && echo "ALL PASS" || { echo "FAILURES PRESENT"; exit 1; }
