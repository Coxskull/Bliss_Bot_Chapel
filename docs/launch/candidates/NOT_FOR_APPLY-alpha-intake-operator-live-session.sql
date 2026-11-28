-- NOT FOR APPLY: Issue #330 proposed live-session guard. REVIEW FIRST.
-- Connected project: Bliss public intake (bkutbglzivfdnoerigyb), NOT the
-- separate ALPHA factory or Railway PG. This migration MUST NOT be applied by
-- automatic pipelines or executed against live data as part of this draft PR.
--
-- Prerequisites: independent security review; check auth.sessions schema and
-- MFA behavior in a truly isolated nonempty restore; confirm backed-up Auth
-- including valid and revoked sessions; owner approval for migration.
-- No new paid plan, no public role EXECUTE grants, no customer invitations.
BEGIN;
CREATE OR REPLACE FUNCTION public.alpha_intake_operator_session_active(
  p_user_id uuid, p_session_id uuid
) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER
SET search_path = pg_catalog
AS $body$
  SELECT EXISTS (
    SELECT 1
    FROM auth.sessions AS s
    JOIN auth.mfa_factors AS f
      ON f.id = s.factor_id
      AND f.user_id = s.user_id
      AND f.status::text = 'verified'
    -- Owner authorized a reviewer scope only for the EXISTING support
    -- operator. A support revocation must immediately deny this review RPC.
    JOIN auth.users AS u
      ON u.id = s.user_id AND u.email_confirmed_at IS NOT NULL
      -- Recheck the separate reviewer grant in the database, not only in the
      -- earlier Auth API response. Missing, false and string-valued flags deny.
      AND u.raw_app_meta_data->'alpha_intake_operator' = 'true'::jsonb
    JOIN alpha_support_private.manual_operator_grants AS g
      ON g.user_id = s.user_id
      AND g.role = 'GENERAL_SUPPORT'
      AND g.enabled IS TRUE
      AND g.allowed_origin = 'https://alphacreativecontent.com'
    WHERE s.id = p_session_id
      AND s.user_id = p_user_id
      AND s.aal::text = 'aal2'
      AND (s.not_after IS NULL OR s.not_after > pg_catalog.now())
  );
$body$;
REVOKE ALL ON FUNCTION public.alpha_intake_operator_session_active(uuid,uuid)
  FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_intake_operator_session_active(uuid,uuid)
  TO service_role;
COMMIT;

-- SECURITY REVIEW CHECKLIST:
-- 1. Verify function OWNER and that only service_role can execute (not merely
--    no access to anonymous users through CORS).
-- 2. Test valid signed-in, revoked, expired, unverified MFA, AAL1 and cross-user
--    ID combinations, plus disabled/missing GENERAL_SUPPORT grant, wrong
--    trusted Origin, removed Auth confirmation, withdrawn reviewer metadata and concurrent revocation.
--    Auth API getUser must also
--    verify a fresh session before trusting claims.
-- 3. Ensure Edge secret SERVICE_ROLE_KEY never leaves its server runtime.
-- 4. No public user profile reads or auth-session export.
-- 5. If no proven authenticated owner-side role can be enrolled safely,
--    leave ALPHA_INTAKE_REVIEW_ENABLED disabled and DO NOT deploy.
