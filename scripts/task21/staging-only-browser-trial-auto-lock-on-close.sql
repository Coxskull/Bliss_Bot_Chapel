-- Task21 hotfix for installed staging schema #56: immediately revoke
-- effective mutation permission when the single fictional case closes.
-- The original migration was applied before this stronger check was added.
-- Security invoker, existing service_role-only EXECUTE grants retained.
DO $task21_closed_preflight$
BEGIN
 IF to_regclass('alpha_support_private.staging_case_mutation_trials') IS NULL
 OR to_regclass('public.alpha_support_cases') IS NULL
 OR (SELECT count(*) FROM alpha_support_private.staging_case_mutation_trials
     WHERE enabled)<>0
 THEN RAISE EXCEPTION 'Task21 closed-case hardening: unexpected staging state'; END IF;
END $task21_closed_preflight$;
CREATE OR REPLACE FUNCTION public.alpha_support_staging_trial_allowed(
 p_case_id uuid,p_actor_user_id uuid,p_origin text
) RETURNS boolean LANGUAGE sql STABLE SECURITY INVOKER
SET search_path=pg_catalog AS $task21_trial_allowed$
 SELECT EXISTS (
   SELECT 1 FROM alpha_support_private.staging_case_mutation_trials t
   JOIN alpha_support_private.staging_operator_grants g
     ON g.user_id=t.actor_user_id AND g.allowed_origin=t.allowed_origin
     AND g.role='GENERAL_SUPPORT' AND g.enabled
   JOIN public.alpha_support_cases c ON c.case_id=t.case_id
   JOIN public.alpha_public_intake_requests r ON r.id=c.request_id
   WHERE t.enabled AND t.expires_at>clock_timestamp()
     AND t.case_id=p_case_id AND t.actor_user_id=p_actor_user_id
     AND t.allowed_origin=p_origin
     AND c.assigned_role='GENERAL_SUPPORT'
     AND c.category='general' AND c.severity='NORMAL'
     AND c.status IN ('OPEN','TRIAGED','IN_REVIEW','RESOLVED')
     AND r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid
     AND r.email='task21-browser-fixture@example.invalid'
     AND r.marketing_opt_in=false
     AND r.full_name='TASK21 BROWSER MUTATION TEST — FICTIONAL'
 );
$task21_trial_allowed$;
REVOKE ALL ON FUNCTION public.alpha_support_staging_trial_allowed(uuid,uuid,text)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_staging_trial_allowed(uuid,uuid,text)
 TO service_role;


