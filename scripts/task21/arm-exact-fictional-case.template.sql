-- TASK21: release-stage one-case trial activation template.
-- NEVER run with __VERIFIED_NEW_PREVIEW_ORIGIN__ placeholder. Only a trusted
-- staging admin may run after a READY Vercel SSO-protected PREVIEW proves
-- the *same* latest source, the user's actual staging Auth + TOTP login and
-- exact-origin staff roster readback. Global mutations remain OFF.
-- This is ONLY the fictional general case below; 90-minute expiration.
-- User must separately perform browser claim/transition/closure afterward.
BEGIN;
SET LOCAL statement_timeout='30s';
SET LOCAL lock_timeout='3s';
DO $task21_arm_single_case$
DECLARE v_origin text:='__VERIFIED_NEW_PREVIEW_ORIGIN__';
  v_case uuid:='619fe5d5-fd4f-4fee-9e2d-c8baee50cc79'::uuid;
  v_count integer;
BEGIN
 IF v_origin !~ '^https://bliss-[a-z0-9]+-alpha-auto-s-projects[.]vercel[.]app$'
 OR (SELECT count(*) FROM alpha_support_private.staging_case_mutation_trials)<>1
 OR NOT EXISTS (SELECT 1 FROM alpha_support_private.staging_case_mutation_trials t
    JOIN alpha_support_private.staging_operator_grants g
      ON g.user_id=t.actor_user_id AND g.role='GENERAL_SUPPORT'
      AND g.allowed_origin=t.allowed_origin AND g.enabled
    JOIN public.alpha_support_cases c ON c.case_id=t.case_id
    JOIN public.alpha_public_intake_requests r ON c.request_id=r.id
    WHERE t.case_id=v_case AND t.allowed_origin=v_origin AND t.enabled=false
      AND c.status='OPEN' AND c.owner_user_id IS NULL
      AND c.assigned_role='GENERAL_SUPPORT' AND c.category='general'
      AND c.severity='NORMAL'
      AND r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid
      AND r.email='task21-browser-fixture@example.invalid'
      AND r.marketing_opt_in=false)
 OR EXISTS (SELECT 1 FROM alpha_support_private.staging_case_mutation_trials
            WHERE enabled)
 THEN RAISE EXCEPTION 'Exact fictional case/actor/origin not eligible'; END IF;
 UPDATE alpha_support_private.staging_case_mutation_trials
 SET enabled=true,created_at=clock_timestamp(),
     expires_at=clock_timestamp()+interval '90 minutes'
 WHERE case_id=v_case AND enabled=false AND allowed_origin=v_origin;
 GET DIAGNOSTICS v_count=ROW_COUNT;
 IF v_count<>1 THEN RAISE EXCEPTION 'Expected one active case-specific trial'; END IF;
END;
$task21_arm_single_case$;
COMMIT;
SELECT
 (SELECT count(*)::int FROM alpha_support_private.staging_case_mutation_trials
  WHERE enabled) AS enabled_scoped_trials,
 'TASK21_ONE_FICTIONAL_CASE_TRIAL_ARMED_90_MINUTES' AS receipt;
