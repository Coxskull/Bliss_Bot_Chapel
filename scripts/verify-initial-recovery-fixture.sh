#!/usr/bin/env bash
# Runner-local, synthetic PostgreSQL 16/18 only. No hosted connection accepted.
set -euo pipefail
: "${POSTGRES_FIXTURE_CONTAINER:?GitHub service container ID required}"
if [[ ! "$POSTGRES_FIXTURE_CONTAINER" =~ ^[a-f0-9]{64}$ ]]; then
  echo 'REFUSED: expected runner service container ID' >&2; exit 1
fi
image="$(docker inspect --format '{{.Config.Image}}' "$POSTGRES_FIXTURE_CONTAINER")"
if [[ "$image" != postgres:16 && "$image" != postgres:18 ]]; then
  echo 'REFUSED: expected official disposable PostgreSQL service image' >&2; exit 1
fi
# Clear inherited libpq configuration inside the isolated container as well.
pgtool() { docker exec "$POSTGRES_FIXTURE_CONTAINER" env -i \
  PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
  PGPASSWORD=postgres PGSSLMODE=disable PGCONNECT_TIMEOUT=5 "$@"; }
pg() { pgtool psql -X --no-password -h 127.0.0.1 -p 5432 -U postgres -v ON_ERROR_STOP=1 "$@"; }
source_db=bliss_initial_migration_ci
restore_db=bliss_recovery_fixture
marker=00000000-0000-4000-8000-000000000518
expected="$(pg -d "$source_db" -Atc 'SELECT count(*) FROM "__EFMigrationsHistory"')"
[[ "$expected" =~ ^[0-9]+$ && "$expected" -ge 48 ]] || { echo 'FAIL: initial migration fixture incomplete' >&2; exit 1; }
pg -d "$source_db" -c "INSERT INTO \"Advertisers\" (\"Id\",\"Name\",\"CreatedAt\") VALUES ('$marker','SYNTHETIC RECOVERY FIXTURE',CURRENT_TIMESTAMP);" >/dev/null
pgtool pg_dump --no-password -h 127.0.0.1 -p 5432 -U postgres -d "$source_db" \
  --format=custom --no-owner --file=/tmp/bliss-recovery-fixture.dump
pg -d postgres -c "CREATE DATABASE $restore_db;" >/dev/null
SECONDS=0
pgtool pg_restore --no-password -h 127.0.0.1 -p 5432 -U postgres -d "$restore_db" \
  --no-owner --no-privileges --exit-on-error /tmp/bliss-recovery-fixture.dump
elapsed=$SECONDS
[[ "$(pg -d "$restore_db" -Atc 'SELECT count(*) FROM "__EFMigrationsHistory"')" == "$expected" ]]
[[ "$(pg -d "$restore_db" -Atc "SELECT count(*) FROM \"Advertisers\" WHERE \"Id\"='$marker' AND \"Name\"='SYNTHETIC RECOVERY FIXTURE'")" == 1 ]]
[[ "$(pg -d "$source_db" -Atc "SELECT count(*) FROM \"Advertisers\" WHERE \"Id\"='$marker'")" == 1 ]]
mkdir -p RecoveryEvidence
checksum="$(pgtool sha256sum /tmp/bliss-recovery-fixture.dump | cut -d' ' -f1)"
printf 'PASS: %s synthetic dump/restore; %s migration records retained; marker preserved; source unchanged; restore_seconds=%s; dump_sha256=%s\nNOT TESTED: hosted backup schedule, hosted restore, production RPO/RTO.\n' \
  "$image" "$expected" "$elapsed" "$checksum" | tee RecoveryEvidence/result.txt
# The archive contains only the disposable fixture and synthetic marker.
docker cp "$POSTGRES_FIXTURE_CONTAINER:/tmp/bliss-recovery-fixture.dump" RecoveryEvidence/fixture.dump
