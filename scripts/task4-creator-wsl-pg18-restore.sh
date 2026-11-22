#!/usr/bin/env bash
# Owner workstation WSL2 Ubuntu: one private, temporary PG18 database only.
# Expected args: base64 Windows archive path; decimal expected public table count.
# No Railway connection, exposed TCP port, plaintext dump, or persistent account.
set -euo pipefail
umask 077
# Fixed, non-sensitive progress markers. No filenames, keys or SQL output.
# Used only to locate an unexpected silent exit when the normal fail stage is lost.
checkpoint() {
    case "${1:-}" in
      PG18_READY|ARCHIVE_PATH_ACCEPTED|GPG_BINARY_READY|TEMP_DB_CREATED|PIPELINE_COMPLETE|TABLE_COUNT_VERIFIED|EF_HISTORY_VERIFIED|TEMP_DB_REMOVED)
        printf 'TASK4_WSL_PG18_CHECKPOINT=%s\n' "$1" ;;
      *) return 2 ;;
    esac
}
# Fixed, non-sensitive classifications, never emit paths, keys or SQL errors.
fail() {
    case "${1:-UNKNOWN}" in
      ARGUMENTS|NOT_ROOT|COUNT_INVALID|TOOLS_MISSING|PG18_BINARY_MISSING|PG18_VERSION_MISMATCH|PG18_CLUSTER_NOT_READY|PG18_SOCKET_MISSING|PG18_QUERY_REFUSED|PG18_MAJOR_MISMATCH|ARCHIVE_PATH_INVALID|GPG_EXE_MISSING|TEMP_DB_NAME_INVALID|TEMP_DB_CHECK_REFUSED|TEMP_DB_EXISTS|TEMP_DB_CREATE_REFUSED|GPG_DECRYPT_REFUSED|PG_RESTORE_REFUSED|TABLE_COUNT_QUERY_REFUSED|TABLE_COUNT_MISMATCH|EF_HISTORY_QUERY_REFUSED|EF_HISTORY_MISSING|TEMP_DB_REMOVE_REFUSED|TEMP_DB_CLEANUP_UNVERIFIED|UNKNOWN) ;;
      *) set -- UNKNOWN ;;
    esac
    printf 'TASK4_WSL_PG18_STAGE=%s\n' "${1:-UNKNOWN}"
    printf '%s\n' "TASK4_WSL_PG18_RESTORE=REFUSED_OR_NOT_VERIFIED"
    exit 2
}
[[ $# -eq 3 ]] || fail ARGUMENTS
[[ "$(id -u)" = "0" ]] || fail NOT_ROOT
[[ "$2" =~ ^[0-9]{1,8}$ ]] || fail COUNT_INVALID
expected="$2"
(( expected >= 90 && expected <= 1000000 )) || fail COUNT_INVALID
for cmd in base64 wslpath pg_restore psql createdb dropdb runuser date; do
    command -v "$cmd" >/dev/null 2>&1 || fail TOOLS_MISSING
done
[[ -x /usr/lib/postgresql/18/bin/pg_restore ]] || fail PG18_BINARY_MISSING
[[ "$(/usr/lib/postgresql/18/bin/pg_restore --version)" == *"PostgreSQL) 18."* ]] || fail PG18_VERSION_MISMATCH
# Ubuntu may have been stopped; start only its EXISTING local PG18 main
# cluster if systemd did not start it. Never initialize or replace any DB.
if [[ ! -S /var/run/postgresql/.s.PGSQL.5432 ]]; then
    command -v pg_ctlcluster >/dev/null 2>&1 || fail PG18_CLUSTER_NOT_READY
    [[ -f /etc/postgresql/18/main/postgresql.conf ]] || fail PG18_CLUSTER_NOT_READY
    pg_ctlcluster 18 main start >/dev/null 2>&1 || fail PG18_CLUSTER_NOT_READY
fi
[[ -S /var/run/postgresql/.s.PGSQL.5432 ]] || fail PG18_SOCKET_MISSING
major="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 -h /var/run/postgresql -d postgres \
    -c "SELECT current_setting('server_version_num')::integer / 10000" 2>/dev/null)" || fail PG18_QUERY_REFUSED
[[ "$major" == "18" ]] || fail PG18_MAJOR_MISMATCH
checkpoint PG18_READY

archive="$(printf '%s' "$1" | base64 -d)" || fail ARCHIVE_PATH_INVALID
[[ "$archive" == [A-Za-z]:\\* ]] || fail ARCHIVE_PATH_INVALID
[[ "$archive" == *.gpg ]] || fail ARCHIVE_PATH_INVALID
checkpoint ARCHIVE_PATH_ACCEPTED
gpg_windows="$(printf '%s' "$3" | base64 -d)" || fail GPG_EXE_MISSING
gpg_binary="$(wslpath -u "$gpg_windows")" || fail GPG_EXE_MISSING
[[ "$gpg_binary" == /mnt/* && "$gpg_binary" == *.exe && -x "$gpg_binary" ]] || fail GPG_EXE_MISSING
checkpoint GPG_BINARY_READY
# No pathname expansion, eval, command substitution of file data or content print.
# Windows gpg.exe keeps decryption inside the existing owner-controlled keyring.
db="$(printf 'alpha_task4_wsl_restore_%s_%s_%s' "$(date -u +%Y%m%d%H%M%S)" "$RANDOM" "$RANDOM")"
[[ "$db" =~ ^alpha_task4_wsl_restore_[0-9]{14}_[0-9]{1,5}_[0-9]{1,5}$ ]] || fail TEMP_DB_NAME_INVALID
exists="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 -h /var/run/postgresql -d postgres \
    -c "SELECT count(*) FROM pg_database WHERE datname='$db'" 2>/dev/null)" || fail TEMP_DB_CLEANUP_UNVERIFIED TEMP_DB_CHECK_REFUSED
[[ "$exists" == "0" ]] || fail TEMP_DB_EXISTS
created=0
cleanup() {
    if [[ "$created" == "1" ]]; then
        runuser -u postgres -- dropdb -h /var/run/postgresql "$db" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT
runuser -u postgres -- createdb -h /var/run/postgresql "$db" >/dev/null 2>&1 || fail TEMP_DB_CREATE_REFUSED
created=1
checkpoint TEMP_DB_CREATED
# Binary pipe, exact exit checks via pipefail; no plaintext dump file.
# Restore against a brand-new LOCAL socket-only database that is destroyed
# before returning a success receipt. This is separate from historic WSL DBs.
# Preserve both process exit statuses without touching the decrypted stream.
# Exit statuses are fixed numeric evidence; stderr is suppressed (no secrets).
set +e
"$gpg_binary" --batch --no-tty --decrypt --output - "$archive" 2>/dev/null |
    runuser -u postgres -- /usr/lib/postgresql/18/bin/pg_restore --exit-on-error --single-transaction \
        --no-owner --no-acl -h /var/run/postgresql -d "$db" >/dev/null 2>&1
pipe_status=( "${PIPESTATUS[@]}" )
set -e
[[ "${pipe_status[0]}" == "0" ]] || fail GPG_DECRYPT_REFUSED
[[ "${pipe_status[1]}" == "0" ]] || fail PG_RESTORE_REFUSED
checkpoint PIPELINE_COMPLETE
actual="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 \
  -h /var/run/postgresql -d "$db" \
  -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'" 2>/dev/null)" || fail TABLE_COUNT_QUERY_REFUSED
[[ "$actual" == "$expected" ]] || fail TABLE_COUNT_MISMATCH
checkpoint TABLE_COUNT_VERIFIED
history="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 \
  -h /var/run/postgresql -d "$db" \
  -c "SELECT CASE WHEN to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL THEN 1 ELSE 0 END" 2>/dev/null)" || fail EF_HISTORY_QUERY_REFUSED
[[ "$history" == "1" ]] || fail EF_HISTORY_MISSING
checkpoint EF_HISTORY_VERIFIED
# Verify actual cleanup, not simply successful restore. Never drop an old DB.
runuser -u postgres -- dropdb -h /var/run/postgresql "$db" >/dev/null 2>&1 || fail TEMP_DB_REMOVE_REFUSED
created=0
left="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 -h /var/run/postgresql -d postgres \
    -c "SELECT count(*) FROM pg_database WHERE datname='$db'" 2>/dev/null)" || fail
[[ "$left" == "0" ]] || fail TEMP_DB_CLEANUP_UNVERIFIED
checkpoint TEMP_DB_REMOVED
printf '%s\n' "TASK4_WSL_PG18_RESTORE=PASS"
