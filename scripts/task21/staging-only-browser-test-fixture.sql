-- TASK21: prepare ONE fictional, DISABLED browser mutation exercise
-- for the already-verified first GENERAL_SUPPORT operator. STAGING ONLY.
-- Requires reviewed per-case trial migration. Not a production script.
-- The real Auth UUID is read from the non-exposed private grant at runtime.
BEGIN;
SET LOCAL statement_timeout='45s';
SET LOCAL lock_timeout='4s';
DO $preflight$
BEGIN
 IF to_regclass('alpha_support_private.staging_case_mutation_trials') IS NULL
 OR (SELECT count(*) FROM public.alpha_support_cases)<>3
 OR (SELECT count(*) FROM public.alpha_public_intake_requests)<>3
 OR (SELECT count(*) FROM public.alpha_support_alert_intents
     WHERE delivery_state='UNROUTED')<>1
 OR (SELECT count(*) FROM public.alpha_ghl_delivery WHERE state='BLOCKED')<>2
 OR (SELECT count(*) FROM alpha_support_private.staging_case_mutation_trials)<>0
 OR (SELECT count(*) FROM alpha_support_private.staging_operator_grants
     WHERE enabled AND role='GENERAL_SUPPORT' AND
       allowed_origin='https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app')<>1
 OR EXISTS(SELECT 1 FROM public.alpha_public_intake_requests
     WHERE request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid)
 OR NOT EXISTS(SELECT 1 FROM public.alpha_support_cases
     WHERE case_id='931d5569-9958-4603-b3c4-fdad25c1acd6'::uuid
     AND status='OPEN' AND owner_user_id IS NULL)
 OR NOT EXISTS(SELECT 1 FROM public.alpha_support_cases
     WHERE case_id='922fdedc-31a1-4dd6-9dd0-31772694922c'::uuid
     AND status='CLOSED' AND owner_user_id IS NOT NULL)
 OR NOT EXISTS(SELECT 1 FROM public.alpha_support_cases
     WHERE case_id='90d58c01-c9ad-464a-99a9-dee900b09d98'::uuid
     AND status='OPEN' AND assigned_role='SECURITY_REVIEW')
 THEN RAISE EXCEPTION 'TASK21 isolated synthetic trial fixture preflight failed'; END IF;
END $preflight$;
SET ROLE service_role;
INSERT INTO public.alpha_public_intake_requests
 (request_key,kind,full_name,email,description,category,notice_version,
  marketing_opt_in,processing_acknowledged,adult_confirmed)
VALUES(
 '36a681b0-9c4d-42a5-bfca-21b000000021'::uuid,
 'support','TASK21 BROWSER MUTATION TEST — FICTIONAL',
 'task21-browser-fixture@example.invalid',
 'FICTIONAL task21 browser-only operator test. No real customer, payment, refund or outreach.',
 'general','AGM-SUPPORT-2026-11-08',false,true,true);
-- Outbox is never committed in PENDING state: blocking is atomic.
UPDATE public.alpha_ghl_delivery d
 SET state='BLOCKED',last_error_class='invalid_record'
 FROM public.alpha_public_intake_requests r
 WHERE d.request_id=r.id
   AND r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid
   AND d.state='PENDING';
DO $check_blocked$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM public.alpha_ghl_delivery d
  JOIN public.alpha_public_intake_requests r ON d.request_id=r.id
  WHERE r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid
    AND d.state='BLOCKED')
 THEN RAISE EXCEPTION 'Synthetic contact not blocked before COMMIT'; END IF;
END $check_blocked$;
RESET ROLE;
INSERT INTO alpha_support_private.staging_case_mutation_trials
 (case_id,actor_user_id,allowed_origin,enabled,expires_at)
SELECT c.case_id,g.user_id,
 'https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app',
 false,clock_timestamp()+interval '4 hours'
FROM public.alpha_support_cases c
JOIN public.alpha_public_intake_requests r ON r.id=c.request_id
JOIN alpha_support_private.staging_operator_grants g ON
 g.role='GENERAL_SUPPORT' AND g.enabled
 AND g.allowed_origin='https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app'
WHERE r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid;
DO $disabled_confirm$
BEGIN
 IF (SELECT count(*) FROM alpha_support_private.staging_case_mutation_trials
  WHERE enabled=false)<>1
 THEN RAISE EXCEPTION 'Synthetic browser trial is not DISABLED'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_cases
  WHERE status='OPEN' AND assigned_role='GENERAL_SUPPORT')<>2
 THEN RAISE EXCEPTION 'General queue fixtures unexpected'; END IF;
END $disabled_confirm$;
COMMIT;
-- Output intentionally excludes staff UUIDs, requester contents or tokens.
SELECT c.case_id,c.status,c.assigned_role,
 t.enabled AS trial_enabled,
 (SELECT count(*)::int FROM public.alpha_support_case_events e
   WHERE e.case_id=c.case_id) AS history_events,
 (SELECT state FROM public.alpha_ghl_delivery d
   WHERE d.request_id=c.request_id) AS crm_status
FROM public.alpha_support_cases c
JOIN public.alpha_public_intake_requests r ON c.request_id=r.id
JOIN alpha_support_private.staging_case_mutation_trials t ON t.case_id=c.case_id
WHERE r.request_key='36a681b0-9c4d-42a5-bfca-21b000000021'::uuid;
