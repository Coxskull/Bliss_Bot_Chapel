#!/usr/bin/env bash
# ALPHA #330 — owner-workstation ENCRYPTED capture, not a recovery acceptance.
# Only the specifically identified Bliss Supabase PG17 project is allowed.
# Never invoke this in a public runner against production. No plaintext dump.
set -Eeuo pipefail
umask 077

fail() {
  printf 'TASK330_AUTH_CAPTURE=HELD_%s\n' "$1" >&2
  exit 1
}
ref='bkutbglzivfdnoerigyb'
mode="${TASK330_CAPTURE_MODE:-}"
host="${PGHOST:-}"
port="${PGPORT:-}"
database="${PGDATABASE:-}"
dbuser="${PGUSER:-}"
output="${TASK330_PRIVATE_OUTPUT_DIR:-}"
recipient="${TASK330_GPG_PUBLIC_FINGERPRINT:-}"

# Separate explicit modes prevent the synthetic CI connection from ever being
# interpreted as authorization for a hosted customer-data export.
case "$mode" in
  owner)
    [[ "${TASK330_OWNER_RECOVERY_AUTHORIZATION:-}" == 'AUTH_INTAKE_ENCRYPTED_CAPTURE' ]] ||
      fail OWNER_AUTHORIZATION_REQUIRED
    [[ "$database" == postgres && "$port" == 5432 ]] || fail WRONG_DATABASE
    if [[ "$host" == "db.${ref}.supabase.co" ]]; then
      [[ "$dbuser" == postgres ]] || fail WRONG_DATABASE_IDENTITY
    elif [[ "$host" =~ ^aws-[0-9]+-ap-southeast-1\.pooler\.supabase\.com$ ]]; then
      [[ "$dbuser" == "postgres.${ref}" ]] || fail WRONG_DATABASE_IDENTITY
    else
      fail WRONG_PROJECT_HOST
    fi
    [[ "${PGSSLMODE:-}" == 'verify-full' &&
       -f "${PGSSLROOTCERT:-/nonexistent}" ]] || fail TLS_VERIFICATION_REQUIRED
    [[ -z "${PGPASSWORD:-}" && -z "${PGSERVICE:-}" && -z "${PGSERVICEFILE:-}" ]] ||
      fail CREDENTIAL_INPUT_NOT_ACCEPTED
    [[ -f "${PGPASSFILE:-/nonexistent}" &&
       "$(stat -c '%a' -- "$PGPASSFILE")" == 600 ]] ||
      fail PRIVATE_PGPASSFILE_REQUIRED
    [[ "$output" == "$HOME/"* ]] || fail PRIVATE_LINUX_HOME_OUTPUT_REQUIRED
    ;;
  ci)
    [[ "${GITHUB_ACTIONS:-}" == true && "${CI:-}" == true ]] || fail CI_ONLY
    [[ "$host" == 127.0.0.1 && "$port" == 5432 &&
       "$database" == ci_alpha330 && "$dbuser" == postgres ]] ||
      fail CI_DATABASE_NOT_ISOLATED
    ;;
  *) fail EXPLICIT_MODE_REQUIRED ;;
esac

[[ -n "$output" && "$output" = /* && -d "$output" &&
   ! -L "$output" && "$(realpath -e -- "$output")" == "$output" &&
   "$(stat -c '%a' -- "$output")" == 700 &&
   "$(stat -c '%u' -- "$output")" == "$(id -u)" ]] ||
  fail PRIVATE_OUTPUT_DIRECTORY_REQUIRED
[[ "$recipient" =~ ^[0-9A-Fa-f]{40}$ ]] || fail PUBLIC_RECIPIENT_REQUIRED

for exe in psql pg_dump gpg sha256sum stat mktemp realpath date; do
  command -v "$exe" >/dev/null 2>&1 || fail REQUIRED_LOCAL_TOOL_MISSING
done
[[ "$(pg_dump --version | awk '{print $3}' | cut -d. -f1)" == 17 ]] ||
  fail POSTGRESQL17_CLIENT_REQUIRED

# A public recipient is not a substitute for a recoverable owner-held secret key.
gpg --batch --no-tty --list-keys "$recipient" >/dev/null 2>&1 ||
  fail GPG_PUBLIC_RECIPIENT_NOT_FOUND
if [[ "$mode" == owner ]]; then
  [[ "${TASK330_PRIVATE_DECRYPTION_KEY_HELD:-}" == 'RECOVERY_KEY_HELD_SEPARATELY' ]] ||
    fail PRIVATE_KEY_CUSTODY_NOT_CONFIRMED
fi

export PGCONNECT_TIMEOUT=10
identity_sql="SELECT CASE WHEN current_database() = '$database'
  AND current_setting('server_version_num')::integer / 10000 = 17
  AND to_regclass('auth.users') IS NOT NULL
  AND to_regclass('auth.sessions') IS NOT NULL
  AND to_regclass('auth.mfa_factors') IS NOT NULL
  AND to_regclass('public.alpha_public_intake_requests') IS NOT NULL
  AND to_regclass('public.alpha_public_intake_receipts') IS NOT NULL
  AND to_regclass('public.alpha_ghl_delivery') IS NOT NULL
  AND to_regclass('alpha_support_private.manual_operator_grants') IS NOT NULL
  THEN 'SOURCE_MATCH' ELSE 'SOURCE_MISMATCH' END;"
identity="$(psql --no-password -X -q -A -t -v ON_ERROR_STOP=1 \
  -c "$identity_sql" 2>/dev/null)" || fail SOURCE_CONNECT_OR_QUERY_FAILED
[[ "$identity" == 'SOURCE_MATCH' ]] || fail SOURCE_SCHEMA_NOT_ACCEPTED

# Auth, public intake and private support are inseparable for this role's
# rollback; --table alongside --schema would silently exclude schema objects.
# pg_dump takes a consistent read-only snapshot; the encrypted stream is
# written only into an owner-private directory, never to a plaintext file.
pending="$(mktemp "$output/.alpha330-pending.XXXXXXXX.pgdump.gpg")" ||
  fail PRIVATE_ARCHIVE_ALLOCATION_FAILED
final=''
receipt=''
completed=0
cleanup() {
  rm -f -- "$pending"
  if [[ "$completed" != 1 ]]; then
    [[ -z "$final" ]] || rm -f -- "$final"
    [[ -z "$receipt" ]] || rm -f -- "$receipt"
  fi
}
trap cleanup EXIT

if ! pg_dump --no-password --format=custom --no-owner --no-acl \
  --lock-wait-timeout=5s --schema=auth --schema=public \
  --schema=alpha_support_private --dbname="$database" 2>/dev/null |
  gpg --batch --yes --no-tty --trust-model always \
    --recipient "$recipient" --encrypt --output "$pending" - 2>/dev/null; then
  fail DUMP_OR_ENCRYPTION_FAILED
fi
[[ -s "$pending" && "$(stat -c '%a' -- "$pending")" == 600 ]] ||
  fail ENCRYPTED_ARCHIVE_EMPTY_OR_NOT_PRIVATE

name="alpha330-auth-intake-$(date -u +%Y%m%dT%H%M%SZ)-$$.pgdump.gpg"
final="$output/$name"
[[ ! -e "$final" ]] || fail ARCHIVE_NAME_COLLISION
mv -- "$pending" "$final" || fail ARCHIVE_FINALIZE_FAILED
ciphertext_sha="$(sha256sum "$final" | awk '{print $1}')"
ciphertext_size="$(stat -c '%s' -- "$final")"
[[ "$ciphertext_sha" =~ ^[0-9a-f]{64}$ && "$ciphertext_size" =~ ^[0-9]+$ ]] ||
  fail CIPHERTEXT_CHECKSUM_FAILED
receipt="$final.receipt.txt"
{
  printf '%s\n' 'TASK330_CAPTURE_PROJECT=bkutbglzivfdnoerigyb'
  printf '%s\n' 'TASK330_CAPTURE_SCOPE=AUTH_PUBLIC_ALPHA_SUPPORT_PRIVATE'
  printf 'TASK330_CAPTURE_UTC=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf 'TASK330_CIPHERTEXT_BYTES=%s\n' "$ciphertext_size"
  printf 'TASK330_CIPHERTEXT_SHA256=%s\n' "$ciphertext_sha"
  printf '%s\n' 'TASK330_ENCRYPTED_LOCAL_CAPTURE=COMPLETE'
  printf '%s\n' 'TASK330_ISOLATED_RESTORE=NOT_VERIFIED'
  printf '%s\n' 'TASK330_OFFHOST_CUSTODY=NOT_VERIFIED'
  printf '%s\n' 'TASK330_PRODUCTION_REVIEW_ACCESS=HELD'
} > "$receipt" || fail RECEIPT_WRITE_FAILED
[[ "$(stat -c '%a' -- "$receipt")" == 600 ]] || fail RECEIPT_NOT_PRIVATE
completed=1
printf '%s\n' 'TASK330_AUTH_CAPTURE=ENCRYPTED_LOCAL_ONLY'
printf '%s\n' 'TASK330_AUTH_CAPTURE_CIPHERTEXT_SHA256=AVAILABLE_IN_PRIVATE_RECEIPT'
printf '%s\n' 'TASK330_AUTH_ISOLATED_RESTORE=NOT_VERIFIED'
printf '%s\n' 'TASK330_REVIEWER_RELEASE=HELD'
