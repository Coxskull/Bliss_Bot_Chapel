#!/usr/bin/env bash
# ONLY the disposable PostgreSQL 18 / age environment provided by phase4e CI.
set -Eeuo pipefail
umask 077
[[ "${CI:-}" == true && "${GITHUB_ACTIONS:-}" == true ]] || { echo 'CI fixture required' >&2; exit 1; }
[[ "$(pg_dump --version)" == *'PostgreSQL) 18.'* ]] || { echo 'pg_dump 18 required' >&2; exit 1; }
root="$(cd "$(dirname "$0")/.." && pwd)"
script="$root/scripts/bliss-logical-backup-operator.sh"
work="$(mktemp -d "$HOME/bliss-operator-ci-XXXXXXXX")"
trap 'rm -rf -- "$work"' EXIT
mkdir -m 700 "$work/secrets" "$work/backups"
passfile="$work/secrets/pgpass"
printf '127.0.0.1:5432:phase4e_source:ci_backup:ci_only_password\n' > "$passfile"
chmod 600 "$passfile"
age-keygen -o "$work/secrets/key.txt" 2>/dev/null
export BLISS_BACKUP_ENCRYPTION_RECIPIENT="$(age-keygen -y "$work/secrets/key.txt")"
export CI=true GITHUB_ACTIONS=true BLISS_BACKUP_ENVIRONMENT=synthetic-ci BLISS_BACKUP_EXECUTION_APPROVED=SYNTHETIC
export PGHOST=127.0.0.1 PGPORT=5432 PGDATABASE=phase4e_source PGUSER=ci_backup PGSSLMODE=disable PGPASSFILE="$passfile"
unset PGPASSWORD
export BLISS_BACKUP_EXPECTED_HOST="$PGHOST" BLISS_BACKUP_EXPECTED_DATABASE="$PGDATABASE" BLISS_BACKUP_EXPECTED_USER="$PGUSER"
export BLISS_BACKUP_EXPECTED_SYSTEM_ID="$(psql -AtX -v ON_ERROR_STOP=1 -c 'SELECT system_identifier FROM pg_control_system()')"
export BLISS_BACKUP_DESTINATION="$work/backups"

reject() { local label="$1" code=0 output; shift; output="$("$@" 2>&1)" || code=$?; if [[ "$code" == 0 || "$output" != *'BLISS BACKUP REFUSED:'* ]]; then echo "FAIL: expected refusal: $label" >&2; exit 1; fi; echo "PASS negative: $label"; }
reject missing-approval env BLISS_BACKUP_EXECUTION_APPROVED= bash "$script" --execute
reject forbidden-host env BLISS_BACKUP_EXPECTED_HOST=not-the-fixture bash "$script" --execute
reject cluster-mismatch env BLISS_BACKUP_EXPECTED_SYSTEM_ID=99999999999999999 bash "$script" --execute
reject invalid-recipient env BLISS_BACKUP_ENCRYPTION_RECIPIENT=not-a-key bash "$script" --execute
printf 'unused-synthetic-placeholder\n' > "$work/secrets/permissive"
chmod 644 "$work/secrets/permissive"
reject permissive-passfile env PGPASSFILE="$work/secrets/permissive" bash "$script" --execute
reject production-CI env BLISS_BACKUP_ENVIRONMENT=production BLISS_BACKUP_EXECUTION_APPROVED=YES PGSSLMODE=verify-full PGSSLROOTCERT="$work/secrets/key.txt" bash "$script" --execute
reject remote-ci-host env PGHOST=postgres.internal bash "$script" --execute
reject public-temp-output env BLISS_BACKUP_DESTINATION=/tmp bash "$script" --execute
ln -s "$work/backups" "$work/not-allowed"
reject symlink-dir env BLISS_BACKUP_DESTINATION="$work/not-allowed" bash "$script" --execute
[[ -z "$(find "$work/backups" -mindepth 1 -type f -print -quit)" ]] || { echo 'FAIL negative guard made a backup' >&2; exit 1; }

bash "$script" --preflight | grep -F 'NO DATABASE CONNECTION attempted'
bash "$script" --execute | grep -F 'OFFSITE_COPY_NOT_VERIFIED'
files=("$work"/backups/*.age)
[[ "${#files[@]}" -eq 1 && -s "${files[0]}" ]] || { echo 'FAIL: missing encrypted artifact' >&2; exit 1; }
[[ "$(stat -c '%a' "${files[0]}")" == 600 ]] || { echo 'FAIL: encrypted backup permissions' >&2; exit 1; }
(cd "$work/backups" && sha256sum --check "$(basename "${files[0]}").sha256")
[[ -z "$(find "$work/backups" -name '*.partial' -print -quit)" ]] || { echo 'FAIL: leaked intermediate archive' >&2; exit 1; }
age --decrypt --identity "$work/secrets/key.txt" --output "$work/restored.dump" "${files[0]}"

# Restore to a THIRD, independently named disposable database in the CI restore cluster.
restore='postgresql://ci_restore:ci_restore_only_password@127.0.0.1:5433/phase4e_operator_restore?sslmode=disable'
PGPASSWORD=ci_restore_only_password createdb -h 127.0.0.1 -p 5433 -U ci_restore phase4e_operator_restore
PGPASSWORD=ci_restore_only_password pg_restore --dbname="$restore" --no-owner --no-acl --exit-on-error "$work/restored.dump"
count="$(PGPASSWORD=ci_restore_only_password psql "$restore" -AtX -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM synthetic')"
[[ "$count" == 2 ]] || { echo 'FAIL: restore row count' >&2; exit 1; }
src="$(psql -AtX -v ON_ERROR_STOP=1 -c "SELECT md5(string_agg(id::text||':'||name,',' ORDER BY id)) FROM synthetic")"
dst="$(PGPASSWORD=ci_restore_only_password psql "$restore" -AtX -v ON_ERROR_STOP=1 -c "SELECT md5(string_agg(id::text||':'||name,',' ORDER BY id)) FROM synthetic")"
[[ -n "$src" && "$src" == "$dst" ]] || { echo 'FAIL: restored data differs from source fixture' >&2; exit 1; }
[[ "$(find "$work/backups" -name '*.age' | wc -l)" == 1 ]] || { echo 'FAIL: unexpected backup count' >&2; exit 1; }
echo 'PASS: operator-mode synthetic streaming encryption, checksum, independent restore, content hash and negative guards'
