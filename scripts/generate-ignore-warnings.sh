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

# Write suppressions to each .rsp file
for file_name in "${rsp_files[@]}"; do
    file_path="$assets_path/$file_name"

    {
        for warning in "${warnings_to_ignore[@]}"; do
            echo "-nowarn:$warning"
        done
    } > "$file_path"

    if [[ $? -eq 0 ]]; then
        echo "Successfully generated $file_name with ${#warnings_to_ignore[@]} warning suppressions."
    else
        echo "Failed to write to $file_name"
        exit 1
    fi
done
