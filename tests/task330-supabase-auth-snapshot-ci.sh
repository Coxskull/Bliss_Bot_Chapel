#!/usr/bin/env bash
# Test only: fake source/restore, never customer data or managed Supabase.
set -Eeuo pipefail
umask 077
[[ "${GITHUB_ACTIONS:-}" == true && "${CI:-}" == true ]] || exit 2
[[ "${PGHOST:-}" == 127.0.0.1 && "${PGPORT:-}" == 5432 &&
   "${PGUSER:-}" == postgres && "${PGDATABASE:-}" == ci_alpha330 ]] || exit 2

root="$(mktemp -d)"
trap 'rm -rf -- "$root"' EXIT
export GNUPGHOME="$root/gpg"
mkdir -m 700 "$GNUPGHOME"
private="$root/private"
mkdir -m 700 "$private"

psql -X -v ON_ERROR_STOP=1 -q <<'SQL'
CREATE SCHEMA auth;
CREATE SCHEMA alpha_support_private;
CREATE TABLE auth.users(id uuid PRIMARY KEY, email text NOT NULL);
CREATE TABLE auth.sessions(id uuid PRIMARY KEY, user_id uuid NOT NULL);
CREATE TABLE auth.mfa_factors(id uuid PRIMARY KEY, user_id uuid NOT NULL);
CREATE TABLE alpha_support_private.manual_operator_grants(user_id uuid PRIMARY KEY, enabled boolean);
CREATE TABLE public.alpha_public_intake_requests(id uuid PRIMARY KEY, kind text);
CREATE TABLE public.alpha_public_intake_receipts(id uuid PRIMARY KEY, request_id uuid);
CREATE TABLE public.alpha_ghl_delivery(id uuid PRIMARY KEY, state text);
INSERT INTO auth.users VALUES ('11111111-1111-4111-8111-111111111111','fake-user@example.test');
INSERT INTO auth.sessions VALUES ('22222222-2222-4222-8222-222222222222','11111111-1111-4111-8111-111111111111');
INSERT INTO auth.mfa_factors VALUES ('33333333-3333-4333-8333-333333333333','11111111-1111-4111-8111-111111111111');
INSERT INTO alpha_support_private.manual_operator_grants VALUES ('11111111-1111-4111-8111-111111111111',true);
INSERT INTO public.alpha_public_intake_requests VALUES ('44444444-4444-4444-8444-444444444444','advertiser');
INSERT INTO public.alpha_public_intake_receipts VALUES ('55555555-5555-4555-8555-555555555555','44444444-4444-4444-8444-444444444444');
INSERT INTO public.alpha_ghl_delivery VALUES ('66666666-6666-4666-8666-666666666666','DELIVERED');
SQL

gpg --batch --yes --pinentry-mode loopback --passphrase '' \
  --quick-generate-key "ALPHA Synthetic Restore <ci@example.test>" rsa2048 encr 1d >/dev/null 2>&1
fpr="$(gpg --batch --with-colons --list-keys | awk -F: '$1=="fpr" {print $10; exit}')"
[[ "$fpr" =~ ^[0-9A-Fa-f]{40}$ ]] || exit 2
export TASK330_CAPTURE_MODE=ci TASK330_GPG_PUBLIC_FINGERPRINT="$fpr"
export TASK330_PRIVATE_OUTPUT_DIR="$private"

# Fail closed on a wrong PostgreSQL source before any dump is attempted.
if PGHOST=not-local.invalid bash scripts/task330-bliss-supabase-auth-intake-capture.sh \
  >/dev/null 2>"$root/fail.log"; then
  echo 'FAIL: unexpected remote source accepted' >&2; exit 1
fi
grep -q '^TASK330_AUTH_CAPTURE=HELD_CI_DATABASE_NOT_ISOLATED' "$root/fail.log" ||
  { echo 'FAIL: incorrect denial reason' >&2; exit 1; }

bash scripts/task330-bliss-supabase-auth-intake-capture.sh > "$root/results.txt"
grep -q '^TASK330_AUTH_CAPTURE=ENCRYPTED_LOCAL_ONLY$' "$root/results.txt"
grep -q '^TASK330_AUTH_ISOLATED_RESTORE=NOT_VERIFIED$' "$root/results.txt"
shopt -s nullglob
archives=("$private"/*.pgdump.gpg)
[[ "${#archives[@]}" == 1 ]] || { echo 'FAIL: wrong archive count' >&2; exit 1; }
archive="${archives[0]}"
[[ -s "$archive" && -f "$archive.receipt.txt" ]] || exit 1
grep -q '^TASK330_ISOLATED_RESTORE=NOT_VERIFIED$' "$archive.receipt.txt"
grep -q '^TASK330_OFFHOST_CUSTODY=NOT_VERIFIED$' "$archive.receipt.txt"
[[ "$(stat -c %a "$archive")" == 600 ]] || exit 1
expected="$(awk -F= '/^TASK330_CIPHERTEXT_SHA256=/ {print $2}' "$archive.receipt.txt")"
[[ "$expected" == "$(sha256sum "$archive" | awk '{print $1}')" ]] || exit 1

# This decrypt/restore is only for the fictional fixture in CI.
createdb ci_alpha330_restore
gpg --batch --decrypt "$archive" 2>/dev/null |
  pg_restore --no-owner --no-acl --clean --if-exists --exit-on-error \
    --dbname=ci_alpha330_restore >/dev/null
readback="$(psql -X -q -A -t -d ci_alpha330_restore -v ON_ERROR_STOP=1 -c \
  "SELECT (SELECT count(*) FROM auth.users) || '|' ||
          (SELECT count(*) FROM auth.sessions) || '|' ||
          (SELECT count(*) FROM auth.mfa_factors) || '|' ||
          (SELECT count(*) FROM alpha_support_private.manual_operator_grants) || '|' ||
          (SELECT count(*) FROM public.alpha_public_intake_requests) || '|' ||
          (SELECT count(*) FROM public.alpha_public_intake_receipts) || '|' ||
          (SELECT count(*) FROM public.alpha_ghl_delivery)")"
[[ "$readback" == '1|1|1|1|1|1|1' ]] || {
  echo 'FAIL: synthetic restored tables did not match' >&2; exit 1;
}
echo 'TASK330_CI_ENCRYPTED_CAPTURE=PASS_FIXTURE_ONLY'
echo 'TASK330_CI_ISOLATED_RESTORE=PASS_FIXTURE_ONLY'
echo 'TASK330_PRODUCTION_AUTH_RESTORE=NOT_VERIFIED'
