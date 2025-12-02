#!/bin/bash

solution_root=$(pwd)
zip_file="${solution_root}/SettingsBackup.zip"
temp_dir=$(mktemp -d)

file_patterns=("appsettings.Development.json" "sharedsettings.Development.json" "*.ps1" "*.sh")

for pattern in "${file_patterns[@]}"
do
    find "$solution_root" -type f -name "$pattern" | while read -r file; do
        rel_path="${file#$solution_root/}"
        dest_dir="$temp_dir/$(dirname "$rel_path")"
        mkdir -p "$dest_dir"
        cp "$file" "$dest_dir"
    done
done

if [ -f "$zip_file" ]; then
    rm "$zip_file"
fi

(cd "$temp_dir" && zip -r "$zip_file" .)
rm -rf "$temp_dir"

echo "Completed: $zip_file"
