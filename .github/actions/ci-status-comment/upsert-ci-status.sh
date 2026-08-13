#!/usr/bin/env bash
# Create or update the single unified CI status comment on a PR, replacing only
# one section (build | lint | tests | performance | automation). All CI comment
# workflows call this through the ci-status-comment composite action, so the
# separate bot comments collapse into one.
#
# The comment is keyed by the hidden <!-- ci-status --> marker and holds one
# fenced block per section:
#
#   <!-- ci-status -->
#   ### 🚦 CI Status
#   <!-- ci:build:start -->       …build…       <!-- ci:build:end -->
#   <!-- ci:lint:start -->        …lint…        <!-- ci:lint:end -->
#   <!-- ci:tests:start -->       …tests…       <!-- ci:tests:end -->
#   <!-- ci:performance:start --> …performance… <!-- ci:performance:end -->
#   <!-- ci:automation:start -->  …automation…  <!-- ci:automation:end -->
#
# Build and Unity Test run as independent workflows whose comment writers can
# fire at the same time, so a plain read-modify-write would drop a section or
# create a duplicate comment. Each attempt collapses any duplicates (keeping the
# oldest), rewrites only its own section on that comment, then re-reads to
# confirm the section landed and no duplicate slipped in — retrying otherwise.
set -euo pipefail

# Optional caller knobs (used by decentraland/performance-testing, which runs
# this script directly against unity-explorer's unified comment):
#   SECTION_BODY_FILE — read the body from a file instead of $SECTION_BODY,
#                       for bodies too large to pass comfortably via env.
#   NO_CREATE=1       — never create the unified comment; exit 3 when it does
#                       not exist so the caller can fall back to a standalone
#                       comment (a foreign-token creation would not be authored
#                       by github-actions[bot] and later writers would not
#                       find it, spawning duplicates).
if [ -n "${SECTION_BODY_FILE:-}" ]; then
  SECTION_BODY="$(cat "$SECTION_BODY_FILE")"
fi

MARKER="<!-- ci-status -->"
HEADER="### 🚦 CI Status"
BOT="github-actions[bot]"
START="<!-- ci:${SECTION}:start -->"
END="<!-- ci:${SECTION}:end -->"

# Neutral "waiting" placeholder for a section that has not reported yet. Used
# only when seeding a brand-new comment; a real run always overwrites its own.
section_default() {
  case "$1" in
    build) printf '![Build](https://img.shields.io/badge/Build-Waiting-lightgrey?logo=unity&logoColor=white&style=for-the-badge)\n\n_Waiting for the build to start…_' ;;
    lint)  printf '![Lint](https://img.shields.io/badge/Lint-Waiting-lightgrey?logo=jetbrains&logoColor=white&style=for-the-badge)\n\n_Waiting for lint to start…_' ;;
    tests) printf '![Tests](https://img.shields.io/badge/Tests-Waiting-lightgrey?logo=codecov&logoColor=white&style=for-the-badge)\n\n_Waiting for tests to start…_' ;;
    automation) printf '![Automation](https://img.shields.io/badge/Automation-On%%20demand-lightgrey?logo=github&logoColor=white&style=for-the-badge)\n\n_On demand — comment `/visual-tests` on this PR to run the visual regression suite against its build._' ;;
    performance) printf '![Performance](https://img.shields.io/badge/Performance-Waiting-lightgrey?logo=speedtest&logoColor=white&style=for-the-badge)\n\n_Bare-metal benchmarks run automatically after each successful build; results arrive as a separate comment. Add the `perf_test` label to run the in-repo Unity performance suite instead (skips normal CI and blocks merge while set)._' ;;
  esac
}

# One section, fenced by its start/end markers.
wrap_section() { printf '<!-- ci:%s:start -->\n%s\n<!-- ci:%s:end -->' "$1" "$2" "$1"; }

# A fresh comment with every section defaulted to "waiting".
skeleton() {
  printf '%s\n%s\n\n%s\n\n%s\n\n%s\n\n%s\n\n%s\n' \
    "$MARKER" "$HEADER" \
    "$(wrap_section build "$(section_default build)")" \
    "$(wrap_section lint  "$(section_default lint)")" \
    "$(wrap_section tests "$(section_default tests)")" \
    "$(wrap_section performance "$(section_default performance)")" \
    "$(wrap_section automation "$(section_default automation)")"
}

# Emit the section body for this run to a file so awk can splice it verbatim,
# free of shell quoting concerns. Parts of the body (lint findings, failed test
# names) originate in the untrusted pull_request job, so drop any line shaped
# like a section marker before writing it — a body line must never open or close
# a section fence, or it would scramble the comment structure / wedge the survive
# check below.
printf '%s\n' "$SECTION_BODY" \
  | grep -vE '^[[:space:]]*<!-- ci[-:][^>]*-->[[:space:]]*$' > section_body.md || true
WANT="$(cat section_body.md)"

# Replace the content between START and END in $1 with section_body.md.
replace_section() {
  awk -v s="$START" -v e="$END" -v f="section_body.md" '
    $0==s { print; while ((getline line < f) > 0) print line; close(f); skip=1; next }
    $0==e { print; skip=0; next }
    skip  { next }
          { print }
  ' <<< "$1"
}

# Trimmed content currently between START and END in $1 (for the survive check).
extract_section() {
  awk -v s="$START" -v e="$END" '
    $0==s { grab=1; next }
    $0==e { grab=0; next }
    grab  { print }
  ' <<< "$1"
}

# Normalise the comment list to a flat array, whether `--paginate --slurp` hands
# back a flat array of comments or an array of per-page arrays.
flatten_pages() { jq -c '[.[] | if type=="array" then .[] else . end]' <<< "$1"; }

# IDs of every marker-bearing bot comment on the PR, oldest first. Flattened
# first so sort_by orders globally rather than only within a page.
marker_ids() {
  jq -r --arg m "$MARKER" --arg bot "$BOT" \
    '[.[] | select(.user.login==$bot and (.body|contains($m)))] | sort_by(.id) | .[].id' <<< "$(flatten_pages "$1")"
}

for attempt in 1 2 3 4 5; do
  COMMENTS=$(gh api "/repos/$REPO/issues/$PR_NUMBER/comments" --paginate --slurp)
  IDS=()
  while IFS= read -r line; do [ -n "$line" ] && IDS+=("$line"); done <<< "$(marker_ids "$COMMENTS")"
  COMMENT_ID="${IDS[0]:-}"

  if [ -z "$COMMENT_ID" ] && [ -n "${NO_CREATE:-}" ]; then
    echo "No unified CI status comment exists and NO_CREATE is set; leaving creation to the repo's own workflows."
    exit 3
  fi

  # Collapse accidental duplicates from a create race: keep the oldest, drop the rest.
  if [ "${#IDS[@]}" -gt 1 ]; then
    for extra in "${IDS[@]:1}"; do
      echo "Deleting duplicate CI status comment $extra."
      gh api -X DELETE "/repos/$REPO/issues/comments/$extra" >/dev/null || true
    done
  fi

  if [ -n "$COMMENT_ID" ]; then
    CURRENT_BODY=$(jq -r --arg id "$COMMENT_ID" '.[] | select(.id==($id|tonumber)) | .body' <<< "$(flatten_pages "$COMMENTS")")
  else
    CURRENT_BODY=""
  fi

  # No unified comment yet: start from the full skeleton. A comment that exists
  # but lacks our markers predates this section (e.g. it was written before the
  # automation section existed) — append an empty fence for just our section
  # instead of resetting the whole comment and wiping the other sections' state.
  if [ -z "$CURRENT_BODY" ]; then
    CURRENT_BODY="$(skeleton)"
  elif ! grep -qF "$START" <<< "$CURRENT_BODY"; then
    CURRENT_BODY="$CURRENT_BODY"$'\n\n'"$(wrap_section "$SECTION" "$(section_default "$SECTION")")"
  fi

  NEW_BODY="$(replace_section "$CURRENT_BODY")"

  if [ -z "$COMMENT_ID" ]; then
    RESULT=$(jq -n --arg b "$NEW_BODY" '{body:$b}' \
      | gh api -X POST "/repos/$REPO/issues/$PR_NUMBER/comments" --input -)
    COMMENT_ID=$(jq -r '.id' <<< "$RESULT")
  else
    jq -n --arg b "$NEW_BODY" '{body:$b}' \
      | gh api -X PATCH "/repos/$REPO/issues/comments/$COMMENT_ID" --input - >/dev/null
  fi

  # Re-read and confirm our section landed on the surviving comment, and that no
  # concurrent writer left a duplicate behind.
  sleep 1
  RECHECK=$(gh api "/repos/$REPO/issues/$PR_NUMBER/comments" --paginate --slurp)
  RIDS=()
  while IFS= read -r line; do [ -n "$line" ] && RIDS+=("$line"); done <<< "$(marker_ids "$RECHECK")"
  LIVE_BODY=$(jq -r --arg id "$COMMENT_ID" '.[] | select(.id==($id|tonumber)) | .body' <<< "$(flatten_pages "$RECHECK")")

  # Success means our section landed on the comment we wrote — nothing more.
  # Duplicate collapsing is best-effort cleanup (the DELETE above may lack
  # permission); a duplicate we could not remove must not block reporting that
  # already succeeded, or every run would burn all 5 attempts and warn forever.
  if [ "$(extract_section "$LIVE_BODY")" = "$WANT" ]; then
    echo "CI status '$SECTION' section updated (attempt $attempt)."
    if [ "${#RIDS[@]}" -gt 1 ]; then
      echo "A duplicate CI status comment remains (could not be deleted); it will be retried next run."
    fi
    exit 0
  fi

  echo "Section '$SECTION' not settled (attempt $attempt); retrying."
  sleep $((attempt * 2))
done

echo "::warning::Could not confirm the '$SECTION' CI status section after 5 attempts."
exit 0
