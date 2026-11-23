-- Task21 staging-only synthetic transport restriction. The original durable outbox
-- remains open for future app-generated incidents; only fictional @example.invalid
-- fixtures may be CLAIMED by the current internal Gmail adapter.
DO $task21_synthetic_guard$
BEGIN
 IF to_regclass('alpha_support_private.staging_app_alert_outbox') IS NULL
 OR to_regclass('alpha_support_private.staging_alert_dispatch_receipts') IS NULL
 OR NOT EXISTS(SELECT 1 FROM public.alpha_support_alert_intents WHERE alert_id='1cf5e519-33e2-4d66-8984-185cc93f3641'::uuid AND delivery_state='UNROUTED')
 THEN RAISE EXCEPTION 'TASK21_STAGING_SYNTHETIC_CLAIM_GUARD_FAILED'; END IF;
END $task21_synthetic_guard$;
CREATE OR REPLACE FUNCTION alpha_support_private.claim_next_staging_app_alert()
RETURNS TABLE(alert_id uuid,claim_token uuid,case_id uuid,severity text,assigned_role text)
LANGUAGE sql SECURITY INVOKER SET search_path=pg_catalog AS $task21_claim$
  WITH selected AS (
    SELECT q.alert_id
    FROM alpha_support_private.staging_app_alert_outbox q
    JOIN public.alpha_support_alert_intents a ON a.alert_id=q.alert_id
    JOIN public.alpha_support_cases c ON c.case_id=a.case_id
    JOIN public.alpha_public_intake_requests r ON r.id=c.request_id
    WHERE q.state='QUEUED' AND a.delivery_state='UNROUTED'
      AND r.kind='support' AND r.notice_version='AGM-SUPPORT-2026-11-08'
      AND lower(r.email) LIKE '%@example.invalid'
      AND r.full_name LIKE 'TASK21 %' AND r.marketing_opt_in IS FALSE
      AND NOT EXISTS (
        SELECT 1 FROM alpha_support_private.staging_alert_dispatch_receipts l
        WHERE l.alert_id=q.alert_id
          AND l.state IN ('PROVIDER_ACCEPTED','HUMAN_ACKNOWLEDGED','UNCERTAIN'))
    ORDER BY q.queued_at,q.alert_id
    FOR UPDATE OF q SKIP LOCKED LIMIT 1
  ), updated AS (
    UPDATE alpha_support_private.staging_app_alert_outbox q
    SET state='CLAIMED',claim_token=gen_random_uuid(),claimed_at=clock_timestamp()
    FROM selected s WHERE q.alert_id=s.alert_id AND q.state='QUEUED'
    RETURNING q.alert_id,q.claim_token
  )
  SELECT u.alert_id,u.claim_token,a.case_id,a.severity,a.assigned_role
  FROM updated u JOIN public.alpha_support_alert_intents a ON a.alert_id=u.alert_id;
$task21_claim$;
REVOKE EXECUTE ON FUNCTION alpha_support_private.claim_next_staging_app_alert() FROM PUBLIC,anon,authenticated,service_role;
