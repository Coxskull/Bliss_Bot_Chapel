-- Task #21: ONE bounded, durable, synthetic-only, GENERAL_SUPPORT
-- operator case closure in the existing isolated ALPHA staging branch.
-- Operator UUID is read from private trusted DB grants; never in source.
-- This script is NOT for production. Do NOT use for real customer cases.
-- No case status API, message sender, admin role or Global STOP change.
BEGIN;
SET LOCAL statement_timeout='60s';
SET LOCAL lock_timeout='4s';

DO $task21_safe_fixture_guard$
BEGIN
 IF to_regclass('alpha_support_private.staging_operator_grants') IS NULL
 OR (SELECT count(*) FROM public.alpha_support_cases)<>2
 OR (SELECT count(*) FROM public.alpha_public_intake_requests)<>2
 OR (SELECT count(*) FROM public.alpha_support_case_events)<>2
 OR (SELECT count(*) FROM public.alpha_support_alert_intents
      WHERE delivery_state='UNROUTED')<>1
 OR (SELECT count(*) FROM public.alpha_ghl_delivery WHERE state='BLOCKED')<>1
 OR (SELECT count(*) FROM auth.users)<>3
 OR EXISTS (SELECT 1 FROM public.alpha_public_intake_requests
   WHERE request_key='922fef6c-41de-4321-8e2c-1b21c6100001'::uuid)
 OR NOT EXISTS (SELECT 1 FROM public.alpha_support_cases
   WHERE case_id='931d5569-9958-4603-b3c4-fdad25c1acd6'::uuid
   AND status='OPEN' AND owner_user_id IS NULL
   AND assigned_role='GENERAL_SUPPORT')
 OR NOT EXISTS (SELECT 1 FROM public.alpha_support_cases
   WHERE case_id='90d58c01-c9ad-464a-99a9-dee900b09d98'::uuid
   AND status='OPEN' AND owner_user_id IS NULL
   AND assigned_role='SECURITY_REVIEW')
 OR (SELECT count(*) FROM alpha_support_private.staging_operator_grants
   WHERE enabled AND role='GENERAL_SUPPORT'
     AND allowed_origin='https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app')<>1
 THEN RAISE EXCEPTION 'TASK21 FICTIONAL STAGING-ONLY CLOSURE REFUSED: preflight changed'; END IF;
END $task21_safe_fixture_guard$;

SET ROLE service_role;

DO $task21_real_operator_synthetic_closure$
DECLARE actor uuid; request_id uuid; ticket_id uuid; n int; did_commit boolean;
BEGIN
 SELECT user_id INTO actor FROM alpha_support_private.staging_operator_grants
 WHERE role='GENERAL_SUPPORT' AND enabled
 AND allowed_origin='https://bliss-o5dlori6e-alpha-auto-s-projects.vercel.app';
 IF actor IS NULL OR NOT alpha_support_private.auth_user_exists(actor)
 THEN RAISE EXCEPTION 'No verified first operator grant'; END IF;

 INSERT INTO public.alpha_public_intake_requests (
  request_key,kind,full_name,email,description,category,notice_version,
  marketing_opt_in,processing_acknowledged,adult_confirmed)
 VALUES (
  '922fef6c-41de-4321-8e2c-1b21c6100001'::uuid,
  'support','TASK21 SYNTHETIC CLOSED EXERCISE — NO CUSTOMER',
  'task21-closure-staging@example.invalid',
  'FICTIONAL staging-only operator case completion. No external service, refund, campaign or outreach.',
  'general','AGM-SUPPORT-2026-11-08',false,true,true)
 RETURNING id INTO request_id;

 -- Block the ONLY CRM outbox row BEFORE COMMIT; no external provider
 -- can observe a pending committed fictional contact.
 UPDATE public.alpha_ghl_delivery
 SET state='BLOCKED',last_error_class='invalid_record'
 WHERE request_id=request_id AND state='PENDING';
 GET DIAGNOSTICS n=ROW_COUNT;
 IF n<>1 THEN RAISE EXCEPTION 'Synthetic CRM outbox was not atomically blocked'; END IF;

 SELECT case_id INTO ticket_id FROM public.alpha_support_cases
 WHERE request_id=request_id AND assigned_role='GENERAL_SUPPORT'
 AND category='general' AND severity='NORMAL' AND status='OPEN';
 IF ticket_id IS NULL
 OR (SELECT count(*) FROM public.alpha_public_intake_receipts
      WHERE request_id=request_id)<>1
 OR (SELECT count(*) FROM public.alpha_support_case_events
      WHERE case_id=ticket_id)<>1
 THEN RAISE EXCEPTION 'Atomic fictional intake, support receipt or history failed'; END IF;

 did_commit=public.alpha_support_claim(ticket_id,actor);
 IF did_commit IS DISTINCT FROM true THEN
 RAISE EXCEPTION 'Real verified operator claim failed'; END IF;
 -- Double-claim must be idempotent; no duplicate assignment event.
 did_commit=public.alpha_support_claim(ticket_id,actor);
 IF did_commit IS DISTINCT FROM true OR
   (SELECT count(*) FROM public.alpha_support_case_events
    WHERE case_id=ticket_id)<>2
 THEN RAISE EXCEPTION 'Repeated real operator claim not idempotent'; END IF;

 did_commit=public.alpha_support_transition(ticket_id,'IN_REVIEW',actor,'',actor);
 IF did_commit IS DISTINCT FROM true THEN RAISE EXCEPTION 'IN_REVIEW failed'; END IF;
 did_commit=public.alpha_support_transition(ticket_id,'RESOLVED',actor,
  'SYNTHETIC only: staging verification completed; no customer or provider contacted',actor);
 IF did_commit IS DISTINCT FROM true THEN RAISE EXCEPTION 'RESOLVED failed'; END IF;
 did_commit=public.alpha_support_transition(ticket_id,'CLOSED',actor,
  'SYNTHETIC only: staging verification completed; no customer or provider contacted',actor);
 IF did_commit IS DISTINCT FROM true THEN RAISE EXCEPTION 'CLOSED failed'; END IF;

 IF NOT EXISTS(SELECT 1 FROM public.alpha_support_cases
   WHERE case_id=ticket_id AND status='CLOSED'
     AND owner_user_id=actor AND resolved_at IS NOT NULL)
 OR (SELECT count(*) FROM public.alpha_support_case_events
      WHERE case_id=ticket_id)<>5
 OR EXISTS(SELECT 1 FROM public.alpha_support_alert_intents
      WHERE case_id=ticket_id)
 OR NOT EXISTS(SELECT 1 FROM public.alpha_ghl_delivery
      WHERE request_id=request_id AND state='BLOCKED')
 OR (SELECT count(*) FROM public.alpha_support_cases
      WHERE case_id IN (
        '931d5569-9958-4603-b3c4-fdad25c1acd6'::uuid,
        '90d58c01-c9ad-464a-99a9-dee900b09d98'::uuid)
      AND status='OPEN' AND owner_user_id IS NULL)<>2
 THEN RAISE EXCEPTION 'Synthetic closure acceptance or isolation failed'; END IF;
END $task21_real_operator_synthetic_closure$;
RESET ROLE;
COMMIT;

-- Idempotent evidence READBACK; no human Auth UUID or sensitive detail.
SELECT c.case_id,c.status,c.category,c.severity,c.assigned_role,
 (c.owner_user_id IS NOT NULL) AS assigned_to_verified_operator,
 (c.resolved_at IS NOT NULL) AS resolved,
 (SELECT count(*)::int FROM public.alpha_support_case_events e
   WHERE e.case_id=c.case_id) AS immutable_events,
 (SELECT count(*)::int FROM public.alpha_public_intake_receipts p
   WHERE p.request_id=c.request_id) AS receipts,
 (SELECT state FROM public.alpha_ghl_delivery d
   WHERE d.request_id=c.request_id) AS fictional_crm_state,
 (SELECT count(*)::int FROM public.alpha_support_cases
   WHERE status='OPEN' AND assigned_role='GENERAL_SUPPORT') AS original_open_queue_count
FROM public.alpha_public_intake_requests r
JOIN public.alpha_support_cases c ON c.request_id=r.id
WHERE r.request_key='922fef6c-41de-4321-8e2c-1b21c6100001'::uuid;
