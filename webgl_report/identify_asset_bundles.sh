#!/bin/bash

# Identifies where AssetBundles come from by querying the Decentraland content server.
# For each asset bundle file, it:
# 1. Strips the _webgl suffix to get the hash
# 2. GETs active-entities for that hash
# 3. POSTs to get entity details (type + pointers)

ASSET_DIR="Explorer/Assets/StreamingAssets/AssetBundles"
BASE_URL="https://peer.decentraland.org/content"

if [ ! -d "$ASSET_DIR" ]; then
    echo "Error: $ASSET_DIR not found"
    exit 1
fi

rm -f /tmp/ab_report_raw.txt /tmp/ab_report_failed.txt
touch /tmp/ab_report_raw.txt /tmp/ab_report_failed.txt

# Gather all _webgl files from root and Wearables subdirectory
all_files=()
for file in "$ASSET_DIR"/*_webgl "$ASSET_DIR"/Wearables/*_webgl; do
    [ -f "$file" ] && all_files+=("$file")
done

total=${#all_files[@]}
count=0

for file in "${all_files[@]}"; do
    filename=$(basename "$file")
    hash="${filename%_webgl}"
    # Track which folder it came from
    parent=$(basename "$(dirname "$file")")

    count=$((count + 1))
    echo -ne "Processing $count/$total: $hash\r" >&2

    # Step 1: Get active entity IDs for this hash
    entity_ids=$(curl -s "${BASE_URL}/contents/${hash}/active-entities")

    if [ -z "$entity_ids" ] || [ "$entity_ids" = "[]" ]; then
        echo -e "FAILED\t$parent\t$hash\tNo active entities found" >> /tmp/ab_report_failed.txt
        continue
    fi

    # Parse the JSON array for the POST body
    ids_json=$(echo "$entity_ids" | jq -c '{ids: .}')

    if [ -z "$ids_json" ]; then
        echo -e "FAILED\t$parent\t$hash\tFailed to parse entity IDs" >> /tmp/ab_report_failed.txt
        continue
    fi

    # Step 2: POST to get entity details
    details=$(curl -s -X POST "${BASE_URL}/entities/active" \
        -H "Content-Type: application/json" \
        -d "$ids_json")

    # Step 3: Collect type and pointers (deduplicated at the end)
    parsed=$(echo "$details" | jq -r '.[] | "\(.type)\t\(.pointers | join(", "))"' 2>/dev/null)

    if [ $? -ne 0 ] || [ -z "$parsed" ]; then
        echo -e "FAILED\t$parent\t$hash\tFailed to parse response" >> /tmp/ab_report_failed.txt
    else
        echo "$parsed" >> /tmp/ab_report_raw.txt
    fi
done

echo "" >&2

# Deduplicate and print report
echo "=== Asset Bundle Report ==="
echo ""
sort -u /tmp/ab_report_raw.txt | while IFS=$'\t' read -r type pointers; do
    echo "Type: $type  Pointers: $pointers"
done

# Print failed entries
failed_count=$(wc -l < /tmp/ab_report_failed.txt | tr -d ' ')
if [ "$failed_count" -gt 0 ]; then
    echo ""
    echo "=== Failed ($failed_count) ==="
    echo ""
    while IFS=$'\t' read -r _ folder hash reason; do
        echo "[$folder] $hash - $reason"
    done < /tmp/ab_report_failed.txt
fi

rm -f /tmp/ab_report_raw.txt /tmp/ab_report_failed.txt
