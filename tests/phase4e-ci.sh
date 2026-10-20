#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
script="$(cd "$(dirname "$0")/.." && pwd)/scripts/phase4c-nonprod-backup.sh"
bash -n "$script"
for tool in psql pg_dump pg_restore age age-keygen sha256sum; do command -v "$tool" >/dev/null; done
pg_dump --version; age --version
export CI=true GITHUB_ACTIONS=true BACKUP_ENVIRONMENT=ci
export BACKUP_DATABASE_URL='postgresql://ci_backup:ci_only_password@127.0.0.1:5432/phase4e_source?sslmode=disable'
export BACKUP_OUTPUT_DIR="$(mktemp -d)"
trap 'rm -rf "$BACKUP_OUTPUT_DIR"' EXIT
age-keygen -o "$BACKUP_OUTPUT_DIR/key.txt" 2>/dev/null
export BACKUP_ENCRYPTION_RECIPIENT="$(age-keygen -y "$BACKUP_OUTPUT_DIR/key.txt")"
export BACKUP_EXPECTED_SYSTEM_ID="$(psql "$BACKUP_DATABASE_URL" -AtX -c 'SELECT system_identifier FROM pg_control_system()')"
psql "$BACKUP_DATABASE_URL" -v ON_ERROR_STOP=1 -c "CREATE TABLE synthetic(id integer PRIMARY KEY, name text NOT NULL); INSERT INTO synthetic VALUES(1,'alpha'),(2,'beta')"
start="$(date +%s)"
archive="$(bash "$script")"
sha256sum --check "$archive.sha256"
age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output "$BACKUP_OUTPUT_DIR/decrypted.dump" "$archive"
export PGPASSWORD=ci_only_password
psql -h 127.0.0.1 -U ci_backup -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE phase4e_restore'
pg_restore -h 127.0.0.1 -U ci_backup -d phase4e_restore --no-owner --no-acl --exit-on-error "$BACKUP_OUTPUT_DIR/decrypted.dump"
src="$(psql "$BACKUP_DATABASE_URL" -AtX -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
dst="$(psql -h 127.0.0.1 -U ci_backup -d phase4e_restore -AtX -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
[[ "$src" == "$dst" && -n "$src" ]]
schema_src="$(pg_dump "$BACKUP_DATABASE_URL" --schema-only --no-owner --no-acl | grep -E '^CREATE TABLE|^    (id|name) ')"
schema_dst="$(pg_dump -h 127.0.0.1 -U ci_backup -d phase4e_restore --schema-only --no-owner --no-acl | grep -E '^CREATE TABLE|^    (id|name) ')"
[[ "$schema_src" == "$schema_dst" ]]
echo "PASS backup/encryption/checksum/restore/schema/records; seconds=$(( $(date +%s) - start ))"
reject(){ if "$@" >/dev/null 2>&1; then echo 'FAIL negative case' >&2; exit 1; fi; }
reject env BACKUP_ENVIRONMENT=production bash "$script"
reject env BACKUP_DATABASE_URL='postgresql://prod@production.invalid/prod' bash "$script"
reject env BACKUP_EXPECTED_SYSTEM_ID=0 bash "$script"
reject env BACKUP_DATABASE_URL='postgresql://ci_backup:bad@127.0.0.1:5432/phase4e_source?sslmode=disable' bash "$script"
reject env BACKUP_ENCRYPTION_RECIPIENT='bad-recipient' bash "$script"
reject env BACKUP_ENCRYPTION_RECIPIENT='' bash "$script"
reject env PATH=/nonexistent /bin/bash "$script"
cp "$archive" "$BACKUP_OUTPUT_DIR/corrupt.age"
printf X >> "$BACKUP_OUTPUT_DIR/corrupt.age"
reject age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output /dev/null "$BACKUP_OUTPUT_DIR/corrupt.age"
printf "%s  %s\\n" "$(sha256sum "$archive" | cut -d\x27 \x27 -f1)" "$BACKUP_OUTPUT_DIR/corrupt.age" > "$BACKUP_OUTPUT_DIR/corrupt.sha256"\nreject sha256sum --check "$BACKUP_OUTPUT_DIR/corrupt.sha256"
reject pg_restore -h 127.0.0.1 -U ci_backup -d phase4e_restore --exit-on-error "$BACKUP_OUTPUT_DIR/corrupt.age"
reject age --decrypt --identity /dev/null --output /dev/null "$archive"
echo 'PASS expected negative failures'
