#!/bin/bash

# List of warning codes to ignore
warnings_to_ignore=(
    8618 8625 8602 8604 8619 8620 8603 8600 8601
    0649 0414 0168 0219 8632
)

# Determine path to the Unity Assets directory
# Assumes this script is run from the project root
assets_path="./Explorer/Assets"
file_path="$assets_path/csc.rsp"

# Make sure the Assets directory exists
if [[ ! -d "$assets_path" ]]; then
    echo "Assets directory not found at $assets_path"
    exit 1
fi

# Write the suppressions to csc.rsp
{
    for warning in "${warnings_to_ignore[@]}"; do
        echo "-nowarn:$warning"
    done
} > "$file_path"

# Feedback
if [[ $? -eq 0 ]]; then
    echo "Successfully generated csc.rsp file at: $file_path"
    echo "Added ${#warnings_to_ignore[@]} warning suppressions to the file."
else
    echo "Failed to generate csc.rsp file"
    exit 1
fi
