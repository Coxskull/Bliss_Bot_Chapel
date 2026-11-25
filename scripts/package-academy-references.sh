#!/bin/sh
# Package only approved internal reference assets outside the static frontend.
set -eu
source_root="$1"
target_root="$2"
manifest="$source_root/MANIFEST.tsv"
test -s "$manifest"
mkdir -p "$target_root/inbox"
cp "$manifest" "$target_root/MANIFEST.tsv"
files="$(mktemp)"
trap 'rm -f "$files"' EXIT HUP INT TERM
awk -F '\t' 'NR>1 && $6=="ACTIVE" && length($8)>0 && length($9)>0 {print $5}' "$manifest" | sort -u > "$files"
test -s "$files"
count=0
while IFS= read -r file; do
    case "$file" in
        ""|.*|*/*) echo "Invalid approved reference filename" >&2; exit 1 ;;
    esac
    test -f "$source_root/inbox/$file"
    test ! -L "$source_root/inbox/$file"
    test -s "$source_root/inbox/$file"
    cp "$source_root/inbox/$file" "$target_root/inbox/$file"
    cmp "$source_root/inbox/$file" "$target_root/inbox/$file"
    count=$((count+1))
done < "$files"
test "$count" -gt 0
printf 'Packaged %s approved internal creative reference assets\n' "$count"
