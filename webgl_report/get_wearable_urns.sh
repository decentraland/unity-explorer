#!/bin/bash

# Fetches the last 100 deployed profiles from Decentraland and extracts
# a deduplicated list of all wearable URNs they are wearing.

BASE_URL="https://peer.decentraland.org/content"
PROFILE_COUNT=${1:-100}
LIMIT=200
DELAY=2

echo "Fetching latest $PROFILE_COUNT deployed profiles..." >&2

urns_file=$(mktemp)
collected=0
next_url="${BASE_URL}/deployments?entityType=profile&limit=${LIMIT}"

while [ $collected -lt $PROFILE_COUNT ] && [ -n "$next_url" ]; do
    echo "Fetching from: $next_url" >&2
    response=$(curl -s "$next_url")

    # Extract wearable URNs directly from deployment metadata
    echo "$response" | python3 -c "
import sys, json
data = json.loads(sys.stdin.read())
for d in data.get('deployments', []):
    if d.get('entityType') == 'profile':
        for avatar in d.get('metadata', {}).get('avatars', []):
            for w in avatar.get('avatar', {}).get('wearables', []):
                print(w)
" >> "$urns_file"

    # Count profiles in this batch
    batch_count=$(echo "$response" | python3 -c "
import sys, json
data = json.loads(sys.stdin.read())
print(sum(1 for d in data.get('deployments', []) if d.get('entityType') == 'profile'))
")
    collected=$((collected + batch_count))

    # Get next pagination URL
    next_params=$(echo "$response" | python3 -c "
import sys, json
data = json.loads(sys.stdin.read())
n = data.get('pagination', {}).get('next', '')
print(n)
")
    if [ -n "$next_params" ]; then
        next_url="${BASE_URL}/deployments?${next_params}&entityType=profile&limit=${LIMIT}"
    else
        next_url=""
    fi

    echo "Collected $collected profiles so far" >&2
    sleep $DELAY
done

# Deduplicate and output
echo "=== Wearable URNs from last $PROFILE_COUNT profiles ==="
echo ""
sort -u "$urns_file" | while IFS= read -r urn; do
    [ -n "$urn" ] && echo "$urn"
done

total_unique=$(sort -u "$urns_file" | grep -c .)
echo "" >&2
echo "Total unique wearable URNs: $total_unique" >&2

rm -f "$urns_file"
