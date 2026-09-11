#!/bin/bash

# List of warning codes to ignore
warnings_to_ignore=(
    8618 8625 8602 8604 8619 8620 8603 8600 8601
    0649 0414 0168 0219 8632
)

# Determine path to the Unity Assets directory
assets_path="./Explorer/Assets"
rsp_files=("csc.rsp" "mcs.rsp" "gmcs.rsp" "smcs.rsp" "us.rsp")

# Make sure the Assets directory exists
if [[ ! -d "$assets_path" ]]; then
    echo "Assets directory not found at $assets_path"
    exit 1
fi

# Write suppressions to each .rsp file, but only on content drift: the rsp files are
# timestamp inputs to Bee's build graph, and an unconditional rewrite forces a DAG
# rebuild (IL2CPP + Usym rerun, ~267s) on every otherwise-no-change build.
# The temp file lives outside Assets/ so an interrupted run never leaves a stray
# file for Unity to import.
tmp=$(mktemp) || { echo "Failed to create temp file"; exit 1; }
trap 'rm -f "$tmp"' EXIT

if ! {
    for warning in "${warnings_to_ignore[@]}"; do
        echo "-nowarn:$warning"
    done
} > "$tmp"; then
    echo "Failed to write warning suppressions"
    exit 1
fi

for file_name in "${rsp_files[@]}"; do
    file_path="$assets_path/$file_name"

    if cmp -s "$tmp" "$file_path" 2>/dev/null; then
        echo "$file_name unchanged (${#warnings_to_ignore[@]} warning suppressions)."
    else
        # cp keeps the destination inode and permissions; only content (and mtime) change.
        if ! cp "$tmp" "$file_path"; then
            echo "Failed to write to $file_name"
            exit 1
        fi
        echo "Updated $file_name with ${#warnings_to_ignore[@]} warning suppressions."
    fi
done
