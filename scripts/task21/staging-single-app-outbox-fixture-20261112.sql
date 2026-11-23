-- ALPHA Task21 exactly one FICTIONAL staging app-generated privacy incident.
-- No real user. No Gmail send. No GHL marketing. No production DB writes.
BEGIN;
DO $task21_one_fixture$
BEGIN
 IF to_regclass('alpha_support_private.staging_app_alert_outbox') IS NULL
   OR (SELECT count(*) FROM alpha_support_private.staging_app_alert_outbox)<>0
   OR (SELECT count(*) FROM public.alpha_support_alert_intents)<>1
   OR (SELECT count(*) FROM public.alpha_support_cases)<>4
   OR (SELECT count(*) FROM public.alpha_public_intake_requests)<>4
   OR EXISTS(SELECT 1 FROM public.alpha_public_intake_requests
             WHERE request_key='f3ae64e1-c7be-4c98-9441-e8c6c2bf7211'::uuid)
 THEN RAISE EXCEPTION 'TASK21_ONE_SYNTHETIC_STAGING_FIXTURE_GUARD_FAILED'; END IF;
END $task21_one_fixture$;
SET LOCAL ROLE service_role;
INSERT INTO public.alpha_public_intake_requests(
  request_key,kind,full_name,email,category,description,notice_version,
  marketing_opt_in,processing_acknowledged,adult_confirmed
) VALUES(
  'f3ae64e1-c7be-4c98-9441-e8c6c2bf7211'::uuid,
  'support','TASK21 APP OUTBOX FICTIONAL PRIVACY',
  'task21-app-outbox@example.invalid','privacy',
  'Synthetic isolated internal privacy routing fixture; never contact or process as a customer.',
  'AGM-SUPPORT-2026-11-08',false,true,true);
RESET ROLE;
COMMIT;
-- Verify by joining only metadata. NEVER select email/name/description here.
SELECT c.case_id,c.category,c.severity,c.assigned_role,a.alert_id,
       a.delivery_state,q.state AS app_outbox_state,
       (SELECT count(*) FROM alpha_support_private.staging_app_alert_events e
        WHERE e.alert_id=a.alert_id) AS durable_app_events,
       (SELECT count(*) FROM public.alpha_ghl_delivery g
        WHERE g.request_id=r.id) AS crm_entries
FROM public.alpha_public_intake_requests r
JOIN public.alpha_support_cases c ON c.request_id=r.id
JOIN public.alpha_support_alert_intents a ON a.case_id=c.case_id
JOIN alpha_support_private.staging_app_alert_outbox q ON q.alert_id=a.alert_id
WHERE r.request_key='f3ae64e1-c7be-4c98-9441-e8c6c2bf7211'::uuid;
