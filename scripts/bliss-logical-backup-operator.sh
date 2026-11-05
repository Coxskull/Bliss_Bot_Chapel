#!/usr/bin/env bash
# Opt-in encrypted logical backup to an ALREADY provisioned private destination.
# Never targets production automatically. Do NOT use this CI tool's credentials.
set -Eeuo pipefail
umask 077

fail() { printf 'BLISS BACKUP REFUSED: %s\n' "$1" >&2; exit 1; }
usage() { printf 'Usage: %s --preflight|--execute\n' "$0"; }
[[ $# -eq 1 ]] || { usage >&2; exit 2; }
case "$1" in
  --preflight) mode=preflight ;;
  --execute) mode=execute ;;
  *) usage >&2; exit 2 ;;
esac

# Explicitly separate disposable CI from owner-attested production execution.
env_name="${BLISS_BACKUP_ENVIRONMENT:-}"
case "$env_name" in
  production)
    [[ "${BLISS_BACKUP_EXECUTION_APPROVED:-}" == YES ]] || fail 'production backup requires explicit owner approval for this database and storage'
    [[ "${CI:-}" != true && "${GITHUB_ACTIONS:-}" != true ]] || fail 'production mode must not run from CI'
    [[ "${PGSSLMODE:-}" == verify-full ]] || fail 'production PostgreSQL requires verify-full TLS'
    [[ -n "${PGSSLROOTCERT:-}" && -f "$PGSSLROOTCERT" ]] || fail 'production TLS root certificate is missing'
    ;;
  synthetic-ci)
    [[ "${BLISS_BACKUP_EXECUTION_APPROVED:-}" == SYNTHETIC ]] || fail 'synthetic mode requires a synthetic-only grant'
    [[ "${CI:-}" == true && "${GITHUB_ACTIONS:-}" == true ]] || fail 'synthetic mode must run inside disposable CI'
    [[ "${PGHOST:-}" == 127.0.0.1 && "${PGSSLMODE:-}" == disable ]] || fail 'synthetic CI source must be local, with no TLS bypass on remote hosts'
    [[ "${PGDATABASE:-}" == phase4e_source && "${PGUSER:-}" == ci_backup && "${PGPORT:-}" == 5432 ]] || fail 'synthetic source must match the disposable CI fixture'
    ;;
  *) fail 'environment must be production or the pinned synthetic-ci fixture' ;;
esac

[[ -z "${PGPASSWORD:-}" ]] || fail 'use a private passfile, not PGPASSWORD'
[[ "${PGHOST:-}" != '' && "${PGDATABASE:-}" != '' && "${PGUSER:-}" != '' && "${PGPORT:-}" =~ ^[0-9]+$ ]] || fail 'PostgreSQL endpoint identity is incomplete'
[[ "$PGDATABASE" =~ ^[a-zA-Z0-9_-]+$ && "$PGUSER" =~ ^[a-zA-Z0-9_.-]+$ ]] || fail 'database identity syntax refused'
[[ "$PGPORT" -ge 1 && "$PGPORT" -le 65535 ]] || fail 'invalid PostgreSQL port'
[[ "${BLISS_BACKUP_EXPECTED_HOST:-}" == "$PGHOST" && "${BLISS_BACKUP_EXPECTED_DATABASE:-}" == "$PGDATABASE" && "${BLISS_BACKUP_EXPECTED_USER:-}" == "$PGUSER" ]] || fail 'source does not match the approved host/database/user tuple'
[[ "${BLISS_BACKUP_EXPECTED_SYSTEM_ID:-}" =~ ^[0-9]{10,}$ ]] || fail 'approved PostgreSQL cluster identifier required'
[[ "$env_name" != production || ( "$PGHOST" != localhost && "$PGHOST" != 127.0.0.1 && "$PGHOST" != ::1 ) ]] || fail 'production source cannot be loopback'
[[ -n "${PGPASSFILE:-}" && -f "$PGPASSFILE" && ! -L "$PGPASSFILE" ]] || fail 'private PostgreSQL passfile required'
[[ "$(stat -c '%a' "$PGPASSFILE")" == 600 && "$(stat -c '%u' "$PGPASSFILE")" == "$(id -u)" ]] || fail 'PostgreSQL passfile must be owned by operator, mode 600'
[[ -n "${BLISS_BACKUP_ENCRYPTION_RECIPIENT:-}" && "${BLISS_BACKUP_ENCRYPTION_RECIPIENT}" =~ ^age1[023456789acdefghjklmnpqrstuvwxyz]{55,}$ ]] || fail 'valid age public recipient required; private key must stay outside backup directory'

backup_dir="${BLISS_BACKUP_DESTINATION:-}"
[[ -n "$backup_dir" && -d "$backup_dir" && ! -L "$backup_dir" ]] || fail 'existing private backup destination directory required'
backup_dir="$(realpath -e -- "$backup_dir")" || fail 'backup destination is inaccessible'
[[ "$backup_dir" != /tmp && "$backup_dir" != /var/tmp && "$backup_dir" != /data && "$backup_dir" != / ]] || fail 'temporary and app runtime storage destinations are refused'
[[ "$backup_dir" != /tmp/* && "$backup_dir" != /var/tmp/* && "$backup_dir" != /var/lib/postgresql/* && "$backup_dir" != */.git/* ]] || fail 'temporary, live database, or repository destinations are refused'
[[ "$(stat -c '%a' "$backup_dir")" == 700 && "$(stat -c '%u' "$backup_dir")" == "$(id -u)" ]] || fail 'backup destination must be operator-owned, mode 700'
[[ "$backup_dir" != "$(dirname "$(realpath -e "$PGPASSFILE")")" ]] || fail 'separate encrypted backups from passfile/key directory'
ancestor="$backup_dir"
while [[ "$ancestor" != / ]]; do
  [[ ! -e "$ancestor/.git" ]] || fail 'backup destination must not be inside a Git checkout'
  ancestor="$(dirname "$ancestor")"
done

for command_name in psql pg_dump age sha256sum flock mktemp; do command -v "$command_name" >/dev/null 2>&1 || fail "required local tool unavailable: $command_name"; done
pgdump_version="$(pg_dump --version 2>/dev/null)" || fail 'pg_dump version unavailable'
[[ "$pgdump_version" =~ PostgreSQL\)\ 18\. ]] || fail 'PostgreSQL 18 pg_dump required for this source'
if [[ "$mode" == preflight ]]; then
  printf 'PREFLIGHT_PASS: local configuration and encryption destination; NO DATABASE CONNECTION attempted.\n'
  printf 'A real backup and isolated restore remain unverified.\n'
  exit 0
fi

# The SQL reads only server metadata; never output source identity or credentials.
actual="$(psql -AtX -v ON_ERROR_STOP=1 --dbname="$PGDATABASE" -c "SELECT current_database() || '|' || current_user || '|' || (SELECT system_identifier FROM pg_control_system())" 2>/dev/null)" || fail 'live source identity attestation query failed'
[[ "$actual" == "$PGDATABASE|$PGUSER|$BLISS_BACKUP_EXPECTED_SYSTEM_ID" ]] || fail 'live PostgreSQL cluster identity mismatch'
server_version="$(psql -AtX -v ON_ERROR_STOP=1 --dbname="$PGDATABASE" -c 'SHOW server_version_num' 2>/dev/null)" || fail 'server version check failed'
[[ "$server_version" =~ ^[0-9]+$ && "$((server_version / 10000))" -eq 18 ]] || fail 'PostgreSQL 18 server required'

# No unencrypted dump file is ever written. Use an atomic, private output name.
exec 9>"$backup_dir/.bliss-backup.lock"
flock -n 9 || fail 'another backup execution already holds the destination lock'
partial="$(mktemp "$backup_dir/bliss-backup-$(date -u +%Y%m%dT%H%M%SZ)-XXXXXXXX.age.partial")" || fail 'cannot allocate encrypted output'
archive="${partial%.partial}"
manifest="${archive}.sha256"
complete=0
cleanup() {
  if [[ "$complete" != 1 ]]; then
    rm -f -- "${partial:-}" "${archive:-}" "${manifest:-}"
  fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
[[ "$(stat -c '%a' "$partial")" == 600 ]] || fail 'encrypted output permissions are not private'
# pg_dump streams directly into age. With pipefail, a failed dump or encryption
# fails the entire command and removes its incomplete archive.
pg_dump --dbname="$PGDATABASE" --format=custom --no-owner --no-acl --no-comments | age --encrypt --recipient "$BLISS_BACKUP_ENCRYPTION_RECIPIENT" > "$partial" || fail 'dump or encryption failed; incomplete artifact removed'
[[ -s "$partial" ]] || fail 'encrypted archive is empty'
[[ ! -e "$archive" && ! -e "$manifest" ]] || fail 'archive name collision'
mv -- "$partial" "$archive"
(
  cd -- "$backup_dir"
  sha256sum "$(basename "$archive")" > "$(basename "$manifest")"
  sha256sum --check "$(basename "$manifest")" >/dev/null
) || fail 'encrypted artifact checksum failed'
[[ "$(stat -c '%a' "$archive")" == 600 && "$(stat -c '%a' "$manifest")" == 600 ]] || fail 'artifact permissions became unsafe'
complete=1
printf 'LOCAL_ENCRYPTED_BACKUP_CREATED: %s\n' "$(basename "$archive")"
printf 'OFFSITE_COPY_NOT_VERIFIED: real isolated restoration and retention are separate acceptance gates.\n'