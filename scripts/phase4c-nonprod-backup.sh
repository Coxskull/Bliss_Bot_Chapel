#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
fail(){ echo "BACKUP FAILED: $*" >&2; exit 1; }
[[ "${BACKUP_ENVIRONMENT:-}" == ci && "${CI:-}" == true && "${GITHUB_ACTIONS:-}" == true ]] || fail 'Disposable GitHub Actions mode required'
expected='postgresql://ci_backup:ci_only_password@127.0.0.1:5432/phase4e_source?sslmode=disable'
[[ "${BACKUP_DATABASE_URL:-}" == "$expected" ]] || fail 'Source connection not allowlisted'
[[ "${BACKUP_EXPECTED_SYSTEM_ID:-}" =~ ^[0-9]+$ ]] || fail 'Missing expected cluster ID'
[[ -n "${BACKUP_OUTPUT_DIR:-}" && -n "${BACKUP_ENCRYPTION_RECIPIENT:-}" ]] || fail 'Missing output directory or recipient'
for cmd in psql pg_dump age sha256sum mktemp; do command -v "$cmd" >/dev/null || fail "Missing $cmd"; done
identity="$(psql "$BACKUP_DATABASE_URL" -AtX -v ON_ERROR_STOP=1 -c "SELECT current_database() || '|' || current_user || '|' || (SELECT system_identifier FROM pg_control_system())")" || fail 'Identity verification failed'
[[ "$identity" == "phase4e_source|ci_backup|$BACKUP_EXPECTED_SYSTEM_ID" ]] || fail 'Database identity mismatch'
mkdir -p "$BACKUP_OUTPUT_DIR"
plain=''
archive=''
success=0
trap '[[ -z "$plain" ]] || rm -f -- "$plain"; if [[ "$success" != 1 && -n "$archive" ]]; then rm -f -- "$archive" "$archive.sha256"; fi' EXIT
plain="$(mktemp "$BACKUP_OUTPUT_DIR/.plain.XXXXXXXX")" || fail 'Plaintext allocation failed'
[[ "$(stat -c %a "$plain")" == 600 ]] || fail 'Plaintext permissions not private'
archive="$(mktemp "$BACKUP_OUTPUT_DIR/backup.XXXXXXXX.age")" || fail 'Archive allocation failed'
pg_dump --dbname="$BACKUP_DATABASE_URL" --format=custom --no-owner --no-acl --file="$plain" || fail 'pg_dump failed'
[[ -s "$plain" ]] || fail 'Empty backup'
[[ "$(stat -c %a "$plain")" == 600 ]] || fail 'Plaintext permissions changed'
age --encrypt --recipient "$BACKUP_ENCRYPTION_RECIPIENT" --output "$archive" "$plain" || fail 'Encryption failed'
sha256sum "$archive" > "$archive.sha256"
sha256sum --check "$archive.sha256" >&2 || fail 'Checksum failed'
rm -f "$plain" || fail 'Plaintext removal failed'
[[ ! -e "$plain" ]] || fail 'Plaintext still exists'
success=1
printf '%s\n' "$archive"
