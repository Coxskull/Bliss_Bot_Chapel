-- TASK21: release-preparation template. NEVER run with placeholder.
-- Execute ONLY after verified Vercel SSO-protected PREVIEW is READY, exact
-- reviewed draft PR SHA and host checked independently in Vercel API.
-- Replace __VERIFIED_NEW_PREVIEW_ORIGIN__ with ONE verified HTTPS origin
-- (no path/trailing slash). No production, no customer records, no spending.
-- This rotates the single staff-role and trial-origin together, leaving
-- ALL case mutations DISABLED. Only an ALPHA trusted staging admin can run.
BEGIN;
SET LOCAL statement_timeout='30s';
SET LOCAL lock_timeout='3s';
DO $task21_preview_rotation$
DECLARE v_new text:='__VERIFIED_NEW_PREVIEW_ORIGIN__';
  v_old text:='https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app';
  v_rows integer;
  v_case uuid:='619fe5d5-fd4f-4fee-9e2d-c8baee50cc79'::uuid;
BEGIN
 IF v_new !~ '^https://bliss-[a-z0-9]+-alpha-auto-s-projects[.]vercel[.]app$'
 OR v_new=v_old OR char_length(v_new)>180 THEN
  RAISE EXCEPTION 'New origin not a distinct, verified Vercel preview';
 END IF;
 IF (SELECT count(*) FROM alpha_support_private.staging_operator_grants
    WHERE enabled AND role='GENERAL_SUPPORT' AND allowed_origin=v_old)<>1
 OR (SELECT count(*) FROM alpha_support_private.staging_operator_grants
    WHERE enabled AND role='GENERAL_SUPPORT')<>1
 OR (SELECT count(*) FROM alpha_support_private.staging_case_mutation_trials)<>1
 OR NOT EXISTS (SELECT 1 FROM alpha_support_private.staging_case_mutation_trials
    WHERE case_id=v_case AND enabled=false AND allowed_origin=v_old)
 OR EXISTS (SELECT 1 FROM alpha_support_private.staging_case_mutation_trials
    WHERE enabled)
 OR NOT EXISTS (SELECT 1 FROM public.alpha_support_cases
    WHERE case_id=v_case AND status='OPEN' AND owner_user_id IS NULL)
 OR (SELECT count(*) FROM public.alpha_support_cases)<>4
 OR (SELECT count(*) FROM public.alpha_support_alert_intents
    WHERE delivery_state='UNROUTED')<>1
 THEN RAISE EXCEPTION 'Staging drift or active mutation trial: rotation refused'; END IF;
 UPDATE alpha_support_private.staging_operator_grants
 SET allowed_origin=v_new WHERE enabled AND role='GENERAL_SUPPORT'
 AND allowed_origin=v_old;
 GET DIAGNOSTICS v_rows=ROW_COUNT;
 IF v_rows<>1 THEN RAISE EXCEPTION 'Expected one operator origin update'; END IF;
 UPDATE alpha_support_private.staging_case_mutation_trials
 SET allowed_origin=v_new WHERE case_id=v_case AND enabled=false
 AND allowed_origin=v_old;
 GET DIAGNOSTICS v_rows=ROW_COUNT;
 IF v_rows<>1 THEN RAISE EXCEPTION 'Expected one disabled trial origin update'; END IF;
END;
$task21_preview_rotation$;
COMMIT;
SELECT
 (SELECT count(*)::int FROM alpha_support_private.staging_operator_grants WHERE enabled) AS enabled_staff,
 (SELECT count(*)::int FROM alpha_support_private.staging_case_mutation_trials WHERE enabled) AS enabled_case_trials,
 (SELECT count(*)::int FROM public.alpha_support_cases WHERE status='OPEN') AS open_cases,
 'TASK21_PROTECTED_PREVIEW_ORIGIN_ROTATED_TRIAL_DISABLED' AS result;
