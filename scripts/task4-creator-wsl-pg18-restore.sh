#!/usr/bin/env bash
# Owner workstation WSL2 Ubuntu: one private, temporary PG18 database only.
# Expected args: base64 Windows archive path; decimal expected public table count.
# No Railway connection, exposed TCP port, plaintext dump, or persistent account.
set -euo pipefail
umask 077
fail() { printf '%s\n' "TASK4_WSL_PG18_RESTORE=REFUSED_OR_NOT_VERIFIED"; exit 2; }
[[ $# -eq 3 ]] || fail
[[ "$(id -u)" = "0" ]] || fail
[[ "$2" =~ ^[0-9]{1,8}$ ]] || fail
expected="$2"
(( expected >= 90 && expected <= 1000000 )) || fail
for cmd in base64 wslpath pg_restore psql createdb dropdb runuser date; do
    command -v "$cmd" >/dev/null 2>&1 || fail
done
[[ -x /usr/lib/postgresql/18/bin/pg_restore ]] || fail
[[ "$(/usr/lib/postgresql/18/bin/pg_restore --version)" == *"PostgreSQL) 18."* ]] || fail
# Ubuntu may have been stopped; start only its EXISTING local PG18 main
# cluster if systemd did not start it. Never initialize or replace any DB.
if [[ ! -S /var/run/postgresql/.s.PGSQL.5432 ]]; then
    command -v pg_ctlcluster >/dev/null 2>&1 || fail
    [[ -f /etc/postgresql/18/main/postgresql.conf ]] || fail
    pg_ctlcluster 18 main start >/dev/null 2>&1 || fail
fi
[[ -S /var/run/postgresql/.s.PGSQL.5432 ]] || fail
major="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 -h /var/run/postgresql -d postgres \
    -c "SELECT current_setting('server_version_num')::integer / 10000" 2>/dev/null)" || fail
[[ "$major" == "18" ]] || fail

archive="$(printf '%s' "$1" | base64 -d)" || fail
[[ "$archive" == [A-Za-z]:\\* ]] || fail
[[ "$archive" == *.gpg ]] || fail
gpg_windows="$(printf '%s' "$3" | base64 -d)" || fail
gpg_binary="$(wslpath -u "$gpg_windows")" || fail
[[ "$gpg_binary" == /mnt/* && "$gpg_binary" == *.exe && -x "$gpg_binary" ]] || fail
# No pathname expansion, eval, command substitution of file data or content print.
# Windows gpg.exe keeps decryption inside the existing owner-controlled keyring.
db="$(printf 'alpha_task4_wsl_restore_%s_%s_%s' "$(date -u +%Y%m%d%H%M%S)" "$RANDOM" "$RANDOM")"
[[ "$db" =~ ^alpha_task4_wsl_restore_[0-9]{14}_[0-9]{1,5}_[0-9]{1,5}$ ]] || fail
exists="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 -h /var/run/postgresql -d postgres \
    -c "SELECT count(*) FROM pg_database WHERE datname='$db'" 2>/dev/null)" || fail
[[ "$exists" == "0" ]] || fail
created=0
cleanup() {
    if [[ "$created" == "1" ]]; then
        runuser -u postgres -- dropdb -h /var/run/postgresql "$db" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT
runuser -u postgres -- createdb -h /var/run/postgresql "$db" >/dev/null 2>&1 || fail
created=1
# Binary pipe, exact exit checks via pipefail; no plaintext dump file.
# Restore against a brand-new LOCAL socket-only database that is destroyed
# before returning a success receipt. This is separate from historic WSL DBs.
if ! ("$gpg_binary" --batch --no-tty --decrypt --output - "$archive" 2>/dev/null |
      runuser -u postgres -- /usr/lib/postgresql/18/bin/pg_restore --exit-on-error --single-transaction \
          --no-owner --no-acl -h /var/run/postgresql -d "$db" >/dev/null 2>&1); then
    fail
fi
actual="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 \
  -h /var/run/postgresql -d "$db" \
  -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'" 2>/dev/null)" || fail
[[ "$actual" == "$expected" ]] || fail
history="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 \
  -h /var/run/postgresql -d "$db" \
  -c "SELECT CASE WHEN to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL THEN 1 ELSE 0 END" 2>/dev/null)" || fail
[[ "$history" == "1" ]] || fail
# Verify actual cleanup, not simply successful restore. Never drop an old DB.
runuser -u postgres -- dropdb -h /var/run/postgresql "$db" >/dev/null 2>&1 || fail
created=0
left="$(runuser -u postgres -- psql -XAt -v ON_ERROR_STOP=1 -h /var/run/postgresql -d postgres \
    -c "SELECT count(*) FROM pg_database WHERE datname='$db'" 2>/dev/null)" || fail
[[ "$left" == "0" ]] || fail
printf '%s\n' "TASK4_WSL_PG18_RESTORE=PASS"
