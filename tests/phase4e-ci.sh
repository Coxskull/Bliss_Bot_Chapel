#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
[[ "${CI:-}" == true && "${GITHUB_ACTIONS:-}" == true ]] || { echo 'REFUSED: disposable GitHub Actions fixture required before database operations' >&2; exit 1; }
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
echo 'STAGE: resolve disposable PostgreSQL identity'
export BACKUP_EXPECTED_SYSTEM_ID="$(psql "$BACKUP_DATABASE_URL" -AtX -c 'SELECT system_identifier FROM pg_control_system()')"
echo 'STAGE: create synthetic records'
psql "$BACKUP_DATABASE_URL" -v ON_ERROR_STOP=1 -c "CREATE TABLE synthetic(id integer PRIMARY KEY, name text NOT NULL); INSERT INTO synthetic VALUES(1,'alpha'),(2,'beta')"
start="$(date +%s)"
# DR3-FIXTURE-STOP-RLS: named, fictional ALPHA-like state. These are NOT
# actual ALPHA migrations, users or receipts. Snapshot this along with
# the existing two-row synthetic fixture to test independent recovery.
psql "$BACKUP_DATABASE_URL" -v ON_ERROR_STOP=1 <<'SQL'
CREATE SCHEMA alpha_recovery_fixture;
CREATE TABLE alpha_recovery_fixture.factory_control (
  id text PRIMARY KEY CHECK (id='factory'),
  state text NOT NULL CHECK (state IN ('STOP','CLEAR'))
);
INSERT INTO alpha_recovery_fixture.factory_control VALUES ('factory','STOP');
CREATE TABLE alpha_recovery_fixture.receipts (
  id integer PRIMARY KEY, payload text NOT NULL
);
INSERT INTO alpha_recovery_fixture.receipts VALUES (1,'synthetic-only-a'),(2,'synthetic-only-b');
ALTER TABLE alpha_recovery_fixture.factory_control ENABLE ROW LEVEL SECURITY;
ALTER TABLE alpha_recovery_fixture.factory_control FORCE ROW LEVEL SECURITY;
ALTER TABLE alpha_recovery_fixture.receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE alpha_recovery_fixture.receipts FORCE ROW LEVEL SECURITY;
CREATE FUNCTION alpha_recovery_fixture.reject_receipt_mutation()
 RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
 RAISE EXCEPTION 'ALPHA_DISPOSABLE_RECEIPT_IMMUTABLE' USING ERRCODE='42501';
END
$body$;
CREATE TRIGGER immutable_receipt_update_delete
 BEFORE UPDATE OR DELETE ON alpha_recovery_fixture.receipts
 FOR EACH ROW EXECUTE FUNCTION alpha_recovery_fixture.reject_receipt_mutation();
CREATE TRIGGER immutable_receipt_truncate
 BEFORE TRUNCATE ON alpha_recovery_fixture.receipts
 FOR EACH STATEMENT EXECUTE FUNCTION alpha_recovery_fixture.reject_receipt_mutation();
SQL
echo 'STAGE: DR3-FIXTURE-STOP-RLS append-only receipt and protected STOP fixture seeded'
echo 'STAGE: encrypted backup'
archive="$(bash "$script")"
echo 'STAGE: checksum and decryption'
sha256sum --check "$archive.sha256"
age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output "$BACKUP_OUTPUT_DIR/decrypted.dump" "$archive"
echo 'STAGE: independent PostgreSQL 18 cluster restore'
export PGPASSWORD=ci_restore_only_password
restore_url='postgresql://ci_restore:ci_restore_only_password@127.0.0.1:5433/phase4e_restore?sslmode=disable'
source_id="$BACKUP_EXPECTED_SYSTEM_ID"
restore_id="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c 'SELECT system_identifier FROM pg_control_system()')"
[[ -n "$restore_id" && "$restore_id" != "$source_id" ]] || { echo 'FAIL independent cluster identity' >&2; exit 1; }
pg_restore --dbname="$restore_url" --no-owner --no-acl --exit-on-error "$BACKUP_OUTPUT_DIR/decrypted.dump"
src="$(psql "$BACKUP_DATABASE_URL" -AtX -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
dst="$(psql "$restore_url" -AtX -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
[[ "$src" == "$dst" && -n "$src" ]]
schema_src="$(pg_dump "$BACKUP_DATABASE_URL" --schema-only --no-owner --no-acl | grep -E '^CREATE TABLE|^    (id|name) ')"
schema_dst="$(pg_dump "$restore_url" --schema-only --no-owner --no-acl | grep -E '^CREATE TABLE|^    (id|name) ')"
[[ "$schema_src" == "$schema_dst" ]]
# On the independently restored nonempty database, verify that immutable
# receipts, forced RLS and STOP state survived encrypted snapshot transport.
fixture_query="SELECT (SELECT state FROM alpha_recovery_fixture.factory_control WHERE id='factory')||'|'||(SELECT md5(string_agg(id::text||':'||payload,',' ORDER BY id)) FROM alpha_recovery_fixture.receipts)"
fixture_src="$(psql "$BACKUP_DATABASE_URL" -AtX -v ON_ERROR_STOP=1 -c "$fixture_query")"
fixture_dst="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c "$fixture_query")"
[[ -n "$fixture_src" && "$fixture_src" == "$fixture_dst" && "$fixture_src" == STOP\|* ]] || {
 echo 'FAIL DR3: STOP or immutable-receipt content mismatch after snapshot restore' >&2
 exit 1
}
meta_query="SELECT string_agg(relname||':'||relrowsecurity::text||':'||relforcerowsecurity::text,',' ORDER BY relname) FROM pg_class WHERE relnamespace='alpha_recovery_fixture'::regnamespace AND relname IN ('factory_control','receipts')"
meta_src="$(psql "$BACKUP_DATABASE_URL" -AtX -v ON_ERROR_STOP=1 -c "$meta_query")"
meta_dst="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c "$meta_query")"
[[ "$meta_src" == 'factory_control:true:true,receipts:true:true' && "$meta_dst" == "$meta_src" ]] || {
 echo 'FAIL DR3: FORCE RLS / table metadata not preserved on restored fixture' >&2
 exit 1
}
# A dedicated nonlogin role receives SELECT and INSERT grants intentionally.
# FORCE RLS with NO policies must still deny any direct row visibility/write.
psql "$restore_url" -v ON_ERROR_STOP=1 -c \
 "CREATE ROLE alpha_ci_limited NOLOGIN; GRANT USAGE ON SCHEMA alpha_recovery_fixture TO alpha_ci_limited; GRANT SELECT, INSERT ON alpha_recovery_fixture.receipts TO alpha_ci_limited" >/dev/null
limited_count="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c \
 "SET ROLE alpha_ci_limited; SELECT count(*) FROM alpha_recovery_fixture.receipts" | tail -n 1)"
[[ "$limited_count" == 0 ]] || { echo 'FAIL DR3: limited role read protected rows' >&2; exit 1; }
fixture_reject() {
 local label="$1" expected="$2" command="$3" output="$BACKUP_OUTPUT_DIR/fixture-1.stderr"
 if psql "$restore_url" -v ON_ERROR_STOP=1 -c "$command" > /dev/null 2>"$output"; then
  echo "FAIL DR3: fixture $label was allowed" >&2; exit 1
 fi
 grep -Fq "$expected" "$output" || {
  echo "FAIL DR3: fixture $label failed for unexpected reason" >&2; exit 1
 }
}
fixture_reject denied-insert 'row-level security policy' \
 "SET ROLE alpha_ci_limited; INSERT INTO alpha_recovery_fixture.receipts VALUES (3,'should-deny')"
fixture_reject denied-update 'ALPHA_DISPOSABLE_RECEIPT_IMMUTABLE' \
 "UPDATE alpha_recovery_fixture.receipts SET payload='tampered' WHERE id=1"
fixture_reject denied-delete 'ALPHA_DISPOSABLE_RECEIPT_IMMUTABLE' \
 "DELETE FROM alpha_recovery_fixture.receipts WHERE id=1"
fixture_reject denied-truncate 'ALPHA_DISPOSABLE_RECEIPT_IMMUTABLE' \
 "TRUNCATE alpha_recovery_fixture.receipts"
[[ "$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c "$fixture_query")" == "$fixture_src" ]] || {
 echo 'FAIL DR3: STOP/receipt integrity changed during denied mutations' >&2; exit 1
}
echo 'PASS DR3 disposable STOP, immutable receipts and FORCE RLS preserved after encrypted restore; denied limited-role reads/writes and receipt changes'
echo "PASS backup/encryption/checksum/restore/schema/records; seconds=$(( $(date +%s) - start ))"

# DR3 disposable failed-change rehearsal. Both source and target are separate
# fictional CI PostgreSQL 18 clusters. Never call this against a hosted DB.
echo 'STAGE: DR3 disposable failed-change transaction and nonempty rollback'
failed_sql="$BACKUP_OUTPUT_DIR/expected-failed-migration.stderr"
# The transaction first modifies seeded rows, then intentionally fails while
# adding a NOT NULL column to a nonempty table. Closing the failed connection
# must roll back BOTH the row mutation and the schema change.
if psql "$restore_url" -v ON_ERROR_STOP=1 -c \
  "BEGIN; UPDATE synthetic SET name='failure-fixture'; ALTER TABLE synthetic ADD COLUMN rejected_not_null integer NOT NULL; COMMIT;" \
  > /dev/null 2>"$failed_sql"; then
  echo 'FAIL DR3: deliberately invalid nonempty migration unexpectedly committed' >&2
  exit 1
fi
grep -q 'contains null values' "$failed_sql" || { echo 'FAIL DR3: wrong failure reason' >&2; exit 1; }
post_failure_hash="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
post_failure_column="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='synthetic' AND column_name='rejected_not_null'")"
[[ "$post_failure_hash" == "$src" && "$post_failure_column" == 0 ]] || {
  echo 'FAIL DR3: transaction did not preserve records and schema after failed migration' >&2
  exit 1
}
echo 'PASS DR3: failed nonempty migration rolled back data and schema in disposable restore cluster'

# Next, simulate a bad COMMITTED change to the disposable restore database.
# Reconstruct from the PRE-change encrypted snapshot into a NEW database,
# never by overwriting live/parent sources. This is a synthetic-only scenario.
psql "$restore_url" -v ON_ERROR_STOP=1 -c \
  "UPDATE synthetic SET name='committed-corruption-fixture' WHERE id=1" >/dev/null
damaged_hash="$(psql "$restore_url" -AtX -v ON_ERROR_STOP=1 -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
[[ -n "$damaged_hash" && "$damaged_hash" != "$src" ]] || {
  echo 'FAIL DR3: committed damage was not detected' >&2
  exit 1
}
rollback_name='phase4e_rollback_rehearsal'
createdb --host=127.0.0.1 --port=5433 --username=ci_restore "$rollback_name"
rollback_url="postgresql://ci_restore:ci_restore_only_password@127.0.0.1:5433/$rollback_name?sslmode=disable"
pg_restore --dbname="$rollback_url" --no-owner --no-acl --exit-on-error "$BACKUP_OUTPUT_DIR/decrypted.dump"
recovered_hash="$(psql "$rollback_url" -AtX -v ON_ERROR_STOP=1 -c "SELECT md5(string_agg(id||':'||name,',' ORDER BY id)) FROM synthetic")"
recovered_count="$(psql "$rollback_url" -AtX -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM synthetic")"
recovered_schema="$(pg_dump "$rollback_url" --schema-only --no-owner --no-acl | grep -E '^CREATE TABLE|^    (id|name) ')"
[[ "$recovered_hash" == "$src" && "$recovered_count" == 2 && "$recovered_schema" == "$schema_src" ]] || {
  echo 'FAIL DR3: independently restored encrypted snapshot did not match original records/schema' >&2
  exit 1
}
# Assert the independent, restored-from-original-snapshot third database also
# recovers the protected fixture as STOP + immutable receipt data + FORCE RLS.
recovered_fixture="$(psql "$rollback_url" -AtX -v ON_ERROR_STOP=1 -c "$fixture_query")"
recovered_metadata="$(psql "$rollback_url" -AtX -v ON_ERROR_STOP=1 -c "$meta_query")"
recovered_triggers="$(psql "$rollback_url" -AtX -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM pg_trigger WHERE tgrelid='alpha_recovery_fixture.receipts'::regclass AND NOT tgisinternal")"
[[ "$recovered_fixture" == "$fixture_src" && "$recovered_metadata" == "$meta_src" && "$recovered_triggers" == 2 ]] || {
 echo 'FAIL DR3: recovery lost STOP, immutable receipts, FORCE RLS or trigger guards' >&2
 exit 1
}
echo 'PASS DR3 disposable fresh recovery retains STOP state, RLS, immutable receipts and two mutation guards'
echo 'PASS DR3 disposable committed damage detected; fresh isolated encrypted-backup restore recovered nonempty data and schema; NOT production RPO/RTO'

reject(){ local label="$1" expected="$2" output rc=0; shift 2; output="$("$@" 2>&1)" || rc=$?; if [[ "$rc" == 0 || "$output" != *"$expected"* ]]; then printf 'FAIL negative %s (exit=%s, expected=%s): %s\\n' "$label" "$rc" "$expected" "$output" >&2; exit 1; fi; echo "PASS negative: $label"; }
reject_exit(){ local label="$1" rc=0; shift; "$@" >/dev/null 2>&1 || rc=$?; if [[ "$rc" == 0 ]]; then echo "FAIL negative: $label" >&2; exit 1; fi; echo "PASS negative: $label"; }
echo 'STAGE: negative security cases'
reject environment-production 'Disposable GitHub Actions mode required' env BACKUP_ENVIRONMENT=production bash "$script"
reject ci-missing 'Disposable GitHub Actions mode required' env -u CI bash "$script"
reject ci-false 'Disposable GitHub Actions mode required' env CI=false bash "$script"
reject actions-missing 'Disposable GitHub Actions mode required' env -u GITHUB_ACTIONS bash "$script"
reject actions-false 'Disposable GitHub Actions mode required' env GITHUB_ACTIONS=false bash "$script"
reject unauthorized-target 'Source connection not allowlisted' env BACKUP_DATABASE_URL='postgresql://ci_backup:ci_only_password@127.0.0.1:5433/phase4e_restore?sslmode=disable' bash "$script"
reject incorrect-database-name 'Source connection not allowlisted' env BACKUP_DATABASE_URL='postgresql://ci_backup:ci_only_password@127.0.0.1:5432/postgres?sslmode=disable' bash "$script"
reject incorrect-database-user 'Source connection not allowlisted' env BACKUP_DATABASE_URL='postgresql://ci_restore:ci_only_password@127.0.0.1:5432/phase4e_source?sslmode=disable' bash "$script"
reject malformed-cluster-id 'Missing expected cluster ID' env BACKUP_EXPECTED_SYSTEM_ID=not-a-number bash "$script"
reject unset-cluster-id 'Missing expected cluster ID' env -u BACKUP_EXPECTED_SYSTEM_ID bash "$script"
reject missing-cluster-id 'Missing expected cluster ID' env BACKUP_EXPECTED_SYSTEM_ID='' bash "$script"
reject unauthorized-target 'Source connection not allowlisted' env BACKUP_DATABASE_URL='postgresql://prod@production.invalid/prod' bash "$script"
reject wrong-cluster-id 'Database identity mismatch' env BACKUP_EXPECTED_SYSTEM_ID=0 bash "$script"
reject unauthorized-target 'Source connection not allowlisted' env BACKUP_DATABASE_URL='postgresql://ci_backup:bad@127.0.0.1:5432/phase4e_source?sslmode=disable' bash "$script"
reject invalid-recipient 'Encryption failed' env BACKUP_ENCRYPTION_RECIPIENT='bad-recipient' bash "$script"
reject missing-recipient 'Missing output directory or recipient' env BACKUP_ENCRYPTION_RECIPIENT='' bash "$script"
reject missing-tools 'Missing psql' env PATH=/nonexistent /bin/bash "$script"
cp "$archive" "$BACKUP_OUTPUT_DIR/corrupt.age"
printf X >> "$BACKUP_OUTPUT_DIR/corrupt.age"
reject_exit age-decryption age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output /dev/null "$BACKUP_OUTPUT_DIR/corrupt.age"
printf '%s  %s\n' "$(sha256sum "$archive" | awk '{print $1}')" "$BACKUP_OUTPUT_DIR/corrupt.age" > "$BACKUP_OUTPUT_DIR/corrupt.sha256"
reject_exit corrupted-checksum sha256sum --check "$BACKUP_OUTPUT_DIR/corrupt.sha256"
reject_exit invalid-restore pg_restore --dbname="$restore_url" --exit-on-error "$BACKUP_OUTPUT_DIR/corrupt.age"
reject_exit age-decryption age --decrypt --identity /dev/null --output /dev/null "$archive"
age-keygen -o "$BACKUP_OUTPUT_DIR/wrong-key.txt" 2>/dev/null
reject_exit age-decryption age --decrypt --identity "$BACKUP_OUTPUT_DIR/wrong-key.txt" --output /dev/null "$archive"
reject_exit age-decryption age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output /dev/null "$BACKUP_OUTPUT_DIR/missing.age"
: > "$BACKUP_OUTPUT_DIR/empty.age"
reject_exit age-decryption age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output /dev/null "$BACKUP_OUTPUT_DIR/empty.age"
head -c 16 "$archive" > "$BACKUP_OUTPUT_DIR/truncated.age"
reject_exit age-decryption age --decrypt --identity "$BACKUP_OUTPUT_DIR/key.txt" --output /dev/null "$BACKUP_OUTPUT_DIR/truncated.age"
reject_exit invalid-restore pg_restore --dbname="$restore_url" --exit-on-error "$BACKUP_OUTPUT_DIR/empty.age"
reject missing-output-dir 'Missing output directory or recipient' env BACKUP_OUTPUT_DIR='' bash "$script"
reject invalid-output-dir 'Not a directory' env BACKUP_OUTPUT_DIR='/dev/null/not-a-dir' bash "$script"
echo 'STAGE: independent P0 cleanup and permission checks'
security_dir="$BACKUP_OUTPUT_DIR/security"
mkdir -m 700 "$security_dir" "$security_dir/bin"
real_dump="$(command -v pg_dump)"
real_age="$(command -v age)"
export real_dump real_age
cat > "$security_dir/bin/pg_dump" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
for arg in "$@"; do
  case "$arg" in --file=*) plain="${arg#--file=}";; esac
done
[[ "$(stat -c %a "$plain")" == 600 ]]
[[ "$(stat -c %a "$(dirname "$plain")")" == 700 ]]
echo 'PASS observed plaintext mode=600 directory mode=700 before dump' >&2
if [[ "${P0_DUMP_FAILURE:-}" == 1 ]]; then
  printf 'partial synthetic dump' > "$plain"
  exit 42
fi
"$real_dump" "$@"
[[ "$(stat -c %a "$plain")" == 600 ]]
SH
cat > "$security_dir/bin/age" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
plain="${!#}"
[[ -s "$plain" && "$(stat -c %a "$plain")" == 600 ]]
echo 'PASS observed nonempty plaintext mode=600 before encryption' >&2
"$real_age" "$@"
SH
chmod 700 "$security_dir/bin/"*
for scenario in success dump-failure encryption-failure; do
  case_dir="$security_dir/$scenario"
  mkdir -m 700 "$case_dir"
  case "$scenario" in
    success)
      result="$(env PATH="$security_dir/bin:$PATH" BACKUP_OUTPUT_DIR="$case_dir" bash "$script")"
      [[ -s "$result" && -s "$result.sha256" ]]
      [[ "$(stat -c %a "$result")" == 600 && "$(stat -c %a "$result.sha256")" == 600 ]]
      echo 'PASS archive and checksum mode=600'
      ;;
    dump-failure)
      reject partial-dump-failure 'pg_dump failed' env PATH="$security_dir/bin:$PATH" P0_DUMP_FAILURE=1 BACKUP_OUTPUT_DIR="$case_dir" bash "$script"
      [[ -z "$(find "$case_dir" -mindepth 1 -print -quit)" ]]
      ;;
    encryption-failure)
      reject independent-encryption-failure 'Encryption failed' env PATH="$security_dir/bin:$PATH" BACKUP_OUTPUT_DIR="$case_dir" BACKUP_ENCRYPTION_RECIPIENT=bad-recipient bash "$script"
      [[ -z "$(find "$case_dir" -mindepth 1 -print -quit)" ]]
      ;;
  esac
  [[ -z "$(find "$case_dir" -name '.plain.*' -print -quit)" ]]
  echo "PASS P0 plaintext cleanup: $scenario"
done

echo 'STAGE: backup failure cleanup and tampered manifest'
faildir="$(mktemp -d)"
reject encryption-failure 'Encryption failed' env BACKUP_OUTPUT_DIR="$faildir" BACKUP_ENCRYPTION_RECIPIENT='bad-recipient' bash "$script"
[[ -z "$(find "$faildir" -type f -print -quit)" ]] || { echo 'FAIL failed-backup cleanup' >&2; exit 1; }
echo 'PASS failed-backup cleanup and encryption-failure plaintext removal'
rm -rf "$faildir"
[[ -z "$(find "$BACKUP_OUTPUT_DIR" -name '.plain.*' -print -quit)" ]] || { echo 'FAIL successful-backup plaintext cleanup' >&2; exit 1; }
echo 'PASS successful-backup plaintext cleanup'
printf '%064d  %s\n' 0 "$archive" > "$BACKUP_OUTPUT_DIR/tampered.sha256"
reject_exit tampered-manifest sha256sum --check "$BACKUP_OUTPUT_DIR/tampered.sha256"

echo 'PASS expected negative failures; independent clusters and security scenarios verified'
