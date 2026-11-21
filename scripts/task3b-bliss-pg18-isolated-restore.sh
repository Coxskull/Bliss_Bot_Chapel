#!/usr/bin/env bash
# ALPHA GLOBAL MEDIA — Task #3B: isolated PostgreSQL 18 restore from owner GPG archive.
# Run only on a separate, owner-controlled OFFLINE recovery machine with a
# separately held decryption key and local PostgreSQL 18 UNIX socket.
# Never run on Railway production, DR362/DigitalOcean, or a live application host.
# Does not change registration flags, payment, outreach, migrations, or Global STOP.
set -Eeuo pipefail
umask 077
fail() { printf 'TASK3B_ISOLATED_PG18_RESTORE=FAILED_%s\n' "$1"; exit 1; }
usage() {
  printf 'Usage: bash %s archive.gpg manifest.pending.json [--preflight | --restore alpha_task3b_restore_<suffix>]\n' "$0" >&2
  exit 2
}
[[ $# -ge 2 && $# -le 4 ]] || usage
archive=$1
manifest=$2
mode=${3:---preflight}
[[ -f "$archive" && -f "$manifest" && ! -L "$archive" && ! -L "$manifest" ]] || fail FILES_MISSING_OR_SYMLINK
[[ "$mode" == --preflight && $# -ge 2 && $# -le 3 || "$mode" == --restore && $# -eq 4 ]] || usage
command -v python3 >/dev/null 2>&1 || fail PYTHON3_MISSING

# Verify source identity AND downloaded ciphertext bytes. Report no secrets.
source_count=$(python3 - "$manifest" "$archive" <<'PY'
import hashlib,json,os,re,sys
mp,ap=sys.argv[1:]
try:
    with open(mp,encoding="utf-8-sig") as f: m=json.load(f)
    assert isinstance(m,dict)
    assert m.get("schema")=="alpha-bliss-pg18-recovery/v1"
    assert m.get("sourceProjectId")=="fb33c1b7-03fb-42c8-89b1-081951a738fd"
    assert m.get("sourceEnvironmentId")=="b67a9836-36bf-4083-93f7-00c867b13f39"
    assert m.get("sourceServiceId")=="6aa78407-1632-48c9-8ea4-0289a7d36a5b"
    assert m.get("sourceDatabase")=="bliss"
    assert type(m.get("sourcePostgresMajor")) is int and m["sourcePostgresMajor"]==18
    assert m.get("encryption")=="GPG_RECIPIENT"
    assert re.fullmatch(r"bliss-pg18-[A-Za-z0-9-]+\.gpg",m.get("archiveFileName",""))
    assert os.path.basename(ap)==m["archiveFileName"]
    assert re.fullmatch(r"[0-9a-f]{64}",m.get("archiveSha256",""))
    assert type(m.get("sourcePublicTableCount")) is int and 0<=m["sourcePublicTableCount"]<=100000
    assert re.fullmatch(r"[0-9A-Fa-f]{40}",m.get("gpgRecipientFingerprint",""))
    assert 100<=os.path.getsize(ap)<=64*1024*1024+4096
    h=hashlib.sha256()
    with open(ap,"rb") as f:
        for block in iter(lambda:f.read(1024*1024),b""): h.update(block)
    assert h.hexdigest()==m["archiveSha256"]
    print(m["sourcePublicTableCount"])
except (AssertionError,OSError,ValueError,KeyError,TypeError):
    sys.exit(21)
PY
) || fail SOURCE_IDENTITY_OR_CIPHERTEXT_MISMATCH
[[ "$source_count" =~ ^[0-9]+$ ]] || fail BAD_SOURCE_COUNT
printf 'TASK3B_ARCHIVE_MANIFEST_SHA256=VERIFIED\n'
printf 'TASK3B_SOURCE_PUBLIC_TABLE_COUNT=%s\n' "$source_count"
if [[ "$mode" == --preflight ]]; then
  printf 'TASK3B_ISOLATED_PG18_RESTORE=NOT_EXECUTED_PREFLIGHT_ONLY\n'
  printf 'TASK3B_PUBLIC_REGISTRATION=HELD\n'
  exit 0
fi

target=$4
[[ "$target" =~ ^alpha_task3b_restore_[a-z0-9_]{6,32}$ ]] || fail TARGET_NAME_REFUSED
[[ "${TASK3B_OFFLINE_RECOVERY_HOST_ACK:-}" == OFFLINE_RECOVERY_HOST ]] || fail RECOVERY_HOST_ATTESTATION_MISSING
# Refuse an active Railway service, even with a mistaken operator attestation.
[[ -z "${RAILWAY_PROJECT_ID:-}" && -z "${RAILWAY_SERVICE_ID:-}" && -z "${RAILWAY_ENVIRONMENT_ID:-}" ]] || fail RAILWAY_RUNTIME_CONTEXT_REFUSED
for tool in psql pg_restore gpg; do
  command -v "$tool" >/dev/null 2>&1 || fail REQUIRED_TOOL_MISSING
done
[[ "$(pg_restore --version)" == pg_restore\ \(PostgreSQL\)\ 18.* ]] || fail PG18_CLIENT_REQUIRED
# Force local UNIX socket. Never honor inherited DATABASE_URL/PGHOST/TCP settings.
unset PGHOST PGHOSTADDR PGPORT PGSERVICE PGSERVICEFILE PGDATABASE PGOPTIONS
socket=/var/run/postgresql
[[ -d "$socket" && ! -L "$socket" ]] || fail LOCAL_SOCKET_MISSING
version=$(psql -X -At -v ON_ERROR_STOP=1 -h "$socket" -d postgres -c 'SHOW server_version_num' 2>/dev/null) || fail LOCAL_CONNECTION_REFUSED
[[ "$version" =~ ^18[0-9]{4}$ ]] || fail LOCAL_SERVER_NOT_PG18
# Operator must provision a NEW, EMPTY disposable local DB with this exact prefix.
# This helper never creates, drops, overwrites, or cleans any database.
before=$(psql -X -At -v ON_ERROR_STOP=1 -h "$socket" -d "$target" -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema') AND schemaname NOT LIKE 'pg_toast%' AND schemaname NOT LIKE 'pg_temp%'" 2>/dev/null) || fail TARGET_DB_MISSING
[[ "$before" == 0 ]] || fail TARGET_NOT_EMPTY
# In a shell with pipefail, both GPG decryption and pg_restore must succeed.
# No plaintext file, --clean, --create, --jobs, or remote DB connection.
if ! (gpg --batch --no-tty --decrypt -- "$archive" 2>/dev/null | pg_restore --exit-on-error --single-transaction --no-owner --no-acl -h "$socket" -d "$target" >/dev/null 2>&1); then
  fail DECRYPT_OR_RESTORE_FAILED
fi
after=$(psql -X -At -v ON_ERROR_STOP=1 -h "$socket" -d "$target" -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'" 2>/dev/null) || fail POST_RESTORE_READ_FAILED
[[ "$after" =~ ^[0-9]+$ && "$after" == "$source_count" ]] || fail PUBLIC_TABLE_COUNT_MISMATCH
history=$(psql -X -At -v ON_ERROR_STOP=1 -h "$socket" -d "$target" -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public' AND tablename='__EFMigrationsHistory'" 2>/dev/null) || fail POST_RESTORE_READ_FAILED
printf 'TASK3B_ISOLATED_PG18_RESTORE=VERIFIED\n'
printf 'TASK3B_RESTORED_PUBLIC_TABLE_COUNT=%s\n' "$after"
if [[ "$after" == 0 || "$history" != 1 ]]; then
  printf 'TASK3B_APP_SCHEMA_BASELINE=NOT_PRESENT\n'
else
  printf 'TASK3B_APP_SCHEMA_BASELINE=METADATA_PRESENT_NOT_ACCEPTED\n'
fi
printf 'TASK3B_PUBLIC_REGISTRATION=HELD\n'