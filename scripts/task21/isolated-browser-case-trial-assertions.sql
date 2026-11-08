-- PG17 isolated, zero-network, synthetic-only browser trial acceptance.
-- Executed only after the existing support, operator grant, alert tests.
-- Keep staging case RPC inaccessible to anon/authenticated and never activate
-- the global ALPHA_SUPPORT_MUTATIONS_ENABLED feature flag.
DO $preflight$
BEGIN
 IF has_table_privilege('anon',
   'alpha_support_private.staging_case_mutation_trials','SELECT')
 OR has_table_privilege('authenticated',
   'alpha_support_private.staging_case_mutation_trials','SELECT')
 OR has_table_privilege('service_role',
   'alpha_support_private.staging_case_mutation_trials','UPDATE')
 OR NOT has_table_privilege('service_role',
   'alpha_support_private.staging_case_mutation_trials','SELECT')
 OR has_function_privilege('anon',
   'public.alpha_support_staging_trial_action(uuid,uuid,text,text,text,text)','EXECUTE')
 OR has_function_privilege('authenticated',
   'public.alpha_support_staging_trial_allowed(uuid,uuid,text)','EXECUTE')
 THEN RAISE EXCEPTION 'Task21 browser trial privilege misconfiguration'; END IF;
END $preflight$;
-- This fixture's service_role lacks intake SELECT unlike real Supabase
-- staging. Provide that ONE read grant here, no write/update access.
GRANT SELECT ON public.alpha_public_intake_requests TO service_role;

INSERT INTO auth.users(id)
 VALUES ('00000000-0000-4000-8000-000000000921'::uuid);
INSERT INTO public.alpha_public_intake_requests
(request_key,kind,full_name,email,description,category,notice_version,marketing_opt_in)
VALUES ('36a681b0-9c4d-42a5-bfca-21b000000021'::uuid,
 'support','TASK21 BROWSER MUTATION TEST — FICTIONAL',
 'task21-browser-fixture@example.invalid',
 'CI only. No customer contact or actual on-call obligation.',
 'general','AGM-SUPPORT-2026-11-08',false);

INSERT INTO alpha_support_private.staging_operator_grants
(user_id,role,allowed_origin,enabled)
VALUES ('00000000-0000-4000-8000-000000000921'::uuid,
 'GENERAL_SUPPORT','https://bliss-task21ci-alpha-auto-s-projects.vercel.app',true);

INSERT INTO alpha_support_private.staging_case_mutation_trials
(case_id,actor_user_id,allowed_origin,enabled,expires_at)
SELECT c.case_id,'00000000-0000-4000-8000-000000000921'::uuid,
 'https://bliss-task21ci-alpha-auto-s-projects.vercel.app',true,
 clock_timestamp()+interval '2 hours'
FROM public.alpha_support_cases c
JOIN public.alpha_public_intake_requests r ON r.id=c.request_id
WHERE r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid;

SET ROLE service_role;
DO $task21_trial_success$
DECLARE cid uuid; person uuid:='00000000-0000-4000-8000-000000000921'::uuid;
 origin text:='https://bliss-task21ci-alpha-auto-s-projects.vercel.app';
 unrelated uuid;
BEGIN
 SELECT c.case_id INTO cid FROM public.alpha_support_cases c
 JOIN public.alpha_public_intake_requests r ON r.id=c.request_id
 WHERE r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid;
 SELECT case_id INTO unrelated FROM public.alpha_support_cases
 WHERE category='security_incident';
 IF NOT public.alpha_support_staging_trial_allowed(cid,person,origin)
 OR public.alpha_support_staging_trial_allowed(cid,person,'https://bliss-wrong-alpha-auto-s-projects.vercel.app')
 OR public.alpha_support_staging_trial_allowed(cid,'00000000-0000-4000-8000-000000000922'::uuid,origin)
 OR public.alpha_support_staging_trial_allowed(unrelated,person,origin)
 THEN RAISE EXCEPTION 'Trial allowlist accepted other actor/case/origin'; END IF;
 IF public.alpha_support_staging_trial_action(cid,person,origin,
   'TRANSITION','CLOSED','SYNTHETIC') IS DISTINCT FROM false
 THEN RAISE EXCEPTION 'Unowned case improperly transitioned'; END IF;
 IF public.alpha_support_staging_trial_action(cid,person,origin,'CLAIM',null,'')
   IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Browser trial claim failed'; END IF;
 IF public.alpha_support_staging_trial_action(cid,person,origin,'CLAIM',null,'')
   IS DISTINCT FROM true
 OR (SELECT count(*) FROM public.alpha_support_case_events
    WHERE case_id=cid AND action='ASSIGNED')<>1
 THEN RAISE EXCEPTION 'Claim was not idempotent'; END IF;
 IF public.alpha_support_staging_trial_action(cid,person,origin,'TRANSITION','IN_REVIEW','')
   IS DISTINCT FROM true
 OR public.alpha_support_staging_trial_action(cid,person,origin,'TRANSITION','RESOLVED',
   'FICTIONAL CI exercise complete; no contact') IS DISTINCT FROM true
 OR public.alpha_support_staging_trial_action(cid,person,origin,'TRANSITION','CLOSED',
   'FICTIONAL CI exercise complete; no contact') IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Complete scoped browser trial lifecycle failed'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_case_events WHERE case_id=cid)<>5
 OR NOT EXISTS(SELECT 1 FROM public.alpha_support_cases WHERE case_id=cid
   AND status='CLOSED' AND owner_user_id=person)
 THEN RAISE EXCEPTION 'Trial history was not immutable/persisted'; END IF;
 IF public.alpha_support_staging_trial_action(cid,person,origin,'TRANSITION',
   'AWAITING_CUSTOMER','') IS DISTINCT FROM false
 THEN RAISE EXCEPTION 'Trial allowed nonapproved status'; END IF;
 IF public.alpha_support_staging_trial_action(cid,person,'https://bliss-wrong-alpha-auto-s-projects.vercel.app',
    'CLAIM',null,'') IS DISTINCT FROM false
 THEN RAISE EXCEPTION 'Spoofed origin allowed'; END IF;
END $task21_trial_success$;
RESET ROLE;

-- Revocation and expiry are mandatory: no long-lived ability or forgotten
-- permissions even if the browser remains logged in.
UPDATE alpha_support_private.staging_case_mutation_trials
 SET enabled=false;
SET ROLE service_role;
DO $task21_trial_revoke$
DECLARE cid uuid;
BEGIN
 SELECT case_id INTO cid FROM alpha_support_private.staging_case_mutation_trials LIMIT 1;
 IF public.alpha_support_staging_trial_allowed(cid,
  '00000000-0000-4000-8000-000000000921'::uuid,
  'https://bliss-task21ci-alpha-auto-s-projects.vercel.app')
 THEN RAISE EXCEPTION 'Revoked trial remains enabled'; END IF;
END $task21_trial_revoke$;
RESET ROLE;
SELECT 'TASK21_SYNTHETIC_BROWSER_CASE_TRIAL_ISOLATED_PG17_PASS' AS acceptance_status;
