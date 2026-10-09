#!/usr/bin/env bash

# Sourced by CI callers. Keep retries quiet until the last API error.
gh_api_retry() {
  local out attempt
  for attempt in 1 2 3; do
    if [ "$attempt" -lt 3 ]; then
      if out=$(gh api "$@" 2>/dev/null); then printf '%s' "$out"; return 0; fi
      sleep 5
    else
      if out=$(gh api "$@"); then printf '%s' "$out"; return 0; fi
    fi
  done
  return 1
}
