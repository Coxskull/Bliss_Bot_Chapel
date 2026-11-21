#!/usr/bin/env bash
# ALPHA Task #3B — WSL-only PostgreSQL 18 schema rehearsal.
# Default: READ ONLY. --apply: create/migrate a NEW local test DB, never
# the separately restored evidence DB, Railway, Supabase or DigitalOcean.
set -Eeuo pipefail
umask 077
readonly ORIGINAL=alpha_task3b_restore_202611
readonly TARGET=alpha_task3b_schema_202611
readonly SOCKET=/var/run/postgresql
fail() { printf 'TASK3B_WSL_SCHEMA=%s\n' "$1" >&2; exit 2; }

[[ $# == 0 || ( $# == 1 && "$1" == --apply ) ]] || fail INVALID_ARGUMENTS
mode=preflight
[[ $# == 0 ]] || mode=apply
[[ -n "${WSL_DISTRO_NAME:-}" && "$(uname -s)" == Linux ]] || fail NOT_WSL
for key in RAILWAY_PROJECT_ID RAILWAY_SERVICE_ID RAILWAY_ENVIRONMENT_ID \
  DATABASE_URL PGHOST PGHOSTADDR PGSERVICE PGSERVICEFILE \
  ConnectionStrings__DefaultConnection ConnectionStrings__DefaultConnection_FILE; do
  [[ -z "${!key:-}" ]] || fail INHERITED_DATABASE_CONFIGURATION_REFUSED
done
command -v psql >/dev/null || fail PSQL_MISSING
[[ -d "$SOCKET" && ! -L "$SOCKET" ]] || fail LOCAL_SOCKET_MISSING
who=$(id -un)
[[ "$who" =~ ^[a-z_][a-z0-9_-]*$ ]] || fail UNSAFE_USER
q() { psql -X -At -v ON_ERROR_STOP=1 -h "$SOCKET" -U "$who" -d "$1" -c "$2"; }
major=$(q postgres "SHOW server_version_num" 2>/dev/null) || fail LOCAL_SERVER_NOT_AVAILABLE
[[ "$major" =~ ^18[0-9]{4}$ ]] || fail LOCAL_PG18_REQUIRED
[[ "$(q postgres 'SELECT inet_server_addr() IS NULL' 2>/dev/null)" == t ]] || fail UNIX_SOCKET_REQUIRED

# Preserve the owner's completed empty-schema restore independently.
original_count=$(q "$ORIGINAL" \
  "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'" 2>/dev/null) \
  || fail ORIGINAL_RESTORE_UNAVAILABLE
[[ "$original_count" == 0 ]] || fail ORIGINAL_RESTORE_CHANGED
printf 'TASK3B_ORIGINAL_RESTORE=UNCHANGED\n'

# Must run from a checkout of the REVIEWED advertiser registration source.
[[ -f Bliss.Api/Bliss.Api.csproj \
  && -f Bliss.Infrastructure/Migrations/20261107090000_SecureAdvertiserRegistration.cs \
  && -f Bliss.Infrastructure/Migrations/20261115110000_AdvertiserPrivacyRequestIntake.cs ]] \
  || fail REGISTRATION_SOURCE_NOT_PRESENT
grep -Fq '"AdvertiserAccountMemberships"' Bliss.Infrastructure/Migrations/20261107090000_SecureAdvertiserRegistration.cs \
  || fail MEMBERSHIP_MIGRATION_MISSING
grep -Fq '"AdvertiserRegistrationReceipts"' Bliss.Infrastructure/Migrations/20261107090000_SecureAdvertiserRegistration.cs \
  || fail RECEIPT_MIGRATION_MISSING
grep -Fq '"AdvertiserPrivacyRequests"' Bliss.Infrastructure/Migrations/20261115110000_AdvertiserPrivacyRequestIntake.cs \
  || fail RIGHTS_MIGRATION_MISSING

existing=$(q postgres "SELECT count(*) FROM pg_database WHERE datname='$TARGET'" 2>/dev/null) \
  || fail TARGET_LOOKUP_FAILED
[[ "$existing" == 0 || "$existing" == 1 ]] || fail TARGET_LOOKUP_INVALID
if [[ "$mode" == preflight ]]; then
  printf 'TASK3B_SCHEMA_SOURCE=ACCOUNT_AND_RIGHTS_MIGRATIONS_PRESENT\n'
  printf 'TASK3B_SCHEMA_TARGET=%s\n' "$([[ "$existing" == 0 ]] && echo NOT_CREATED || echo EXISTS)"
  printf 'TASK3B_WSL_SCHEMA=READ_ONLY_PREFLIGHT_PASS\n'
  printf 'TASK3B_PUBLIC_REGISTRATION=HELD\n'
  exit 0
fi

command -v dotnet >/dev/null || fail DOTNET_MISSING
dotnet --list-sdks | grep -Eq '^8\.' || fail DOTNET8_SDK_REQUIRED
if [[ "$existing" == 0 ]]; then
  command -v sudo >/dev/null || fail SUDO_MISSING
  sudo -u postgres createdb -h "$SOCKET" -O "$who" "$TARGET" \
    || fail TARGET_CREATION_FAILED
fi
# No replay and no writes into an already-initialized database.
table_count=$(q "$TARGET" "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'" 2>/dev/null) \
  || fail TARGET_UNAVAILABLE
[[ "$table_count" == 0 ]] || fail TARGET_ALREADY_INITIALIZED

# One-shot EF migration only; no HTTP listener, no seeded customer data.
printf 'TASK3B_WSL_SCHEMA=STARTING_LOCAL_MIGRATION\n'
if ! env ASPNETCORE_ENVIRONMENT=Development \
  Authentication__Enabled=false Authentication__SupabaseEnabled=false \
  ConnectionStrings__DefaultConnection="Host=$SOCKET;Port=5432;Database=$TARGET;Username=$who;SSL Mode=Disable" \
  dotnet run --no-launch-profile --project Bliss.Api/Bliss.Api.csproj -- --migrate; then
  fail LOCAL_MIGRATION_FAILED
fi

result=$(q "$TARGET" "SELECT
 (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public'
  AND tablename IN ('Advertisers','AdvertiserAccountMemberships',
                    'AdvertiserRegistrationReceipts','AdvertiserPrivacyRequests'))::text || '|' ||
 (SELECT count(*) FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='public' AND c.relname IN
  ('AdvertiserAccountMemberships','AdvertiserRegistrationReceipts','AdvertiserPrivacyRequests')
  AND c.relrowsecurity)::text || '|' ||
 (SELECT count(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" IN
  ('20261107090000_SecureAdvertiserRegistration','20261115110000_AdvertiserPrivacyRequestIntake'))::text" 2>/dev/null) \
  || fail LOCAL_SCHEMA_READBACK_FAILED
[[ "$result" == "4|3|2" ]] || fail LOCAL_SCHEMA_CONTRACT_MISMATCH
printf 'TASK3B_WSL_SCHEMA=LOCAL_PG18_VERIFIED\n'
printf 'TASK3B_REGISTRATION_TABLES=3_RLS_PROTECTED\n'
printf 'TASK3B_PUBLIC_REGISTRATION=HELD\n'
