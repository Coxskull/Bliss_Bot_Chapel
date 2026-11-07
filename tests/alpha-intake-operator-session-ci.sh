#!/usr/bin/env bash
set -Eeuo pipefail
# Never target a Supabase/Railway instance: ephemeral GitHub Actions PG service only.
[[ "${GITHUB_ACTIONS:-}" == "true" && "${CI:-}" == "true" ]] ||
  { echo "REFUSED: disposable GitHub Actions fixture required" >&2; exit 1; }
[[ "${PGHOST:-}" == "127.0.0.1" && "${PGPORT:-}" == "5432" &&
   "${PGUSER:-}" == "postgres" && "${PGDATABASE:-}" == "postgres" ]] ||
  { echo "REFUSED: unexpected PostgreSQL target" >&2; exit 1; }
command -v psql >/dev/null
export PGCONNECT_TIMEOUT=5

psql -X -v ON_ERROR_STOP=1 <<'SQL'
CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
CREATE ROLE service_role NOLOGIN;
CREATE SCHEMA auth;
CREATE TABLE auth.sessions(
  id uuid PRIMARY KEY, user_id uuid NOT NULL, aal text NOT NULL,
  factor_id uuid, not_after timestamptz
);
CREATE TABLE auth.mfa_factors(
  id uuid PRIMARY KEY, user_id uuid NOT NULL, status text NOT NULL
);
SQL

# Only the candidate function (no real Supabase migration) is executed here.
psql -X -v ON_ERROR_STOP=1 -f docs/launch/candidates/NOT_FOR_APPLY-alpha-intake-operator-live-session.sql >/dev/null
psql -X -v ON_ERROR_STOP=1 <<'SQL'
INSERT INTO auth.mfa_factors(id,user_id,status)
VALUES ('22222222-2222-4222-8222-222222222222','11111111-1111-4111-8111-111111111111','verified');
INSERT INTO auth.sessions(id,user_id,aal,factor_id,not_after)
VALUES ('33333333-3333-4333-8333-333333333333',
        '11111111-1111-4111-8111-111111111111','aal2',
        '22222222-2222-4222-8222-222222222222',now()+interval '1 day');
DO $test$
DECLARE
  u uuid := '11111111-1111-4111-8111-111111111111';
  s uuid := '33333333-3333-4333-8333-333333333333';
  other uuid := '44444444-4444-4444-8444-444444444444';
BEGIN
  IF NOT has_function_privilege('service_role','public.alpha_intake_operator_session_active(uuid,uuid)','EXECUTE')
    OR has_function_privilege('anon','public.alpha_intake_operator_session_active(uuid,uuid)','EXECUTE')
    OR has_function_privilege('authenticated','public.alpha_intake_operator_session_active(uuid,uuid)','EXECUTE')
  THEN RAISE EXCEPTION 'unexpected function EXECUTE privileges'; END IF;

  IF NOT public.alpha_intake_operator_session_active(u,s)
  THEN RAISE EXCEPTION 'verified current AAL2 session must pass'; END IF;

  IF public.alpha_intake_operator_session_active(other,s)
    OR public.alpha_intake_operator_session_active(u,other)
  THEN RAISE EXCEPTION 'cross-user or unknown session accepted'; END IF;

  UPDATE auth.sessions SET aal='aal1' WHERE id=s;
  IF public.alpha_intake_operator_session_active(u,s)
  THEN RAISE EXCEPTION 'AAL1 session accepted'; END IF;
  UPDATE auth.sessions SET aal='aal2' WHERE id=s;

  UPDATE auth.mfa_factors SET status='unverified' WHERE id=
    '22222222-2222-4222-8222-222222222222';
  IF public.alpha_intake_operator_session_active(u,s)
  THEN RAISE EXCEPTION 'unverified factor accepted'; END IF;
  UPDATE auth.mfa_factors SET status='verified' WHERE id=
    '22222222-2222-4222-8222-222222222222';

  UPDATE auth.sessions SET not_after=now()-interval '1 day' WHERE id=s;
  IF public.alpha_intake_operator_session_active(u,s)
  THEN RAISE EXCEPTION 'expired session accepted'; END IF;
  UPDATE auth.sessions SET not_after=now()+interval '1 day' WHERE id=s;

  UPDATE auth.sessions SET factor_id=NULL WHERE id=s;
  IF public.alpha_intake_operator_session_active(u,s)
  THEN RAISE EXCEPTION 'missing MFA factor accepted'; END IF;
  UPDATE auth.sessions SET factor_id='22222222-2222-4222-8222-222222222222' WHERE id=s;

  DELETE FROM auth.sessions WHERE id=s;
  IF public.alpha_intake_operator_session_active(u,s)
  THEN RAISE EXCEPTION 'revoked/removed session accepted'; END IF;
END
$test$;
SQL

# A simulated unprivileged API principal must not directly invoke the RPC.
error_file="$(mktemp)"
trap 'rm -f "$error_file"' EXIT
if psql -X -v ON_ERROR_STOP=1 -c \
  "SET ROLE authenticated; SELECT public.alpha_intake_operator_session_active('11111111-1111-4111-8111-111111111111','33333333-3333-4333-8333-333333333333');" \
  > /dev/null 2>"$error_file"; then
  echo "FAIL: authenticated public principal could call session guard" >&2; exit 1
fi
grep -q "permission denied" "$error_file" || {
  echo "FAIL: wrong reason for public role denial" >&2; exit 1
}
echo "PASS alpha-intake-operator-session: disposable PG17 verified AAL2, current/revoked/expired session, cross-user and SQL privileges"
