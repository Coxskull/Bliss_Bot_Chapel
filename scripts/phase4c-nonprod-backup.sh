#!/usr/bin/env bash
# Phase 4C non-production logical backup prototype.
set -Eeuo pipefail
umask 077
: "${BACKUP_ENVIRONMENT:?Set BACKUP_ENVIRONMENT=development or test}"
case "$BACKUP_ENVIRONMENT" in development|test|ci) ;; *) echo "Refusing non-test environment" >&2; exit 64;; esac
: "${BACKUP_DATABASE_URL:?Set a non-production PostgreSQL URL}"
: "${BACKUP_OUTPUT_DIR:?Set an output directory}"
: "${BACKUP_ENCRYPTION_RECIPIENT:?Set age public recipient}"
command -v pg_dump >/dev/null
command -v age >/dev/null
command -v sha256sum >/dev/null
mkdir -p -- "$BACKUP_OUTPUT_DIR"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
file="$BACKUP_OUTPUT_DIR/nonprod-$stamp.dump.age"
tmp="$(mktemp "$BACKUP_OUTPUT_DIR/.dump.XXXXXXXX")"
trap 'rm -f -- "$tmp"' EXIT
pg_dump --dbname="$BACKUP_DATABASE_URL" --format=custom --no-owner --no-acl --file="$tmp"
test -s "$tmp"
age --encrypt --recipient "$BACKUP_ENCRYPTION_RECIPIENT" --output "$file" "$tmp"
sha256sum "$file" > "$file.sha256"
sha256sum --check "$file.sha256"
echo "Encrypted non-production backup prepared: $file"
echo "No production operation, upload, retention deletion, or restore was performed."
