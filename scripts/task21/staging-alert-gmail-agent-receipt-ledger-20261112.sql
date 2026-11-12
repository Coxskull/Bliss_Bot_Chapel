-- Task21: ISOLATED STAGING ONLY. No production support migration or live customer data.
-- Durable manual-agent internal Gmail bridge receipt ledger, not an automated sender.
-- Purpose: distinguish PREPARED, Gmail provider acceptance and a separate human ACK.
-- The original alert intent remains UNROUTED until a real application sender exists.
DO $task21_guard$
BEGIN
  IF to_regclass('public.alpha_support_alert_intents') IS NULL
     OR to_regclass('alpha_support_private.staging_operator_grants') IS NULL
     OR to_regclass('alpha_support_private.staging_alert_dispatch_receipts') IS NOT NULL
     OR current_setting('server_version_num')::integer < 170000
     OR (SELECT count(*) FROM public.alpha_support_alert_intents) <> 1
     OR NOT EXISTS (
       SELECT 1 FROM public.alpha_support_alert_intents a
       JOIN public.alpha_support_cases c ON c.case_id=a.case_id
       WHERE a.alert_id='1cf5e519-33e2-4d66-8984-185cc93f3641'::uuid
         AND a.delivery_state='UNROUTED' AND a.severity='CRITICAL'
         AND a.assigned_role='SECURITY_REVIEW' AND c.category='security_incident'
     )
  THEN RAISE EXCEPTION 'TASK21_STAGING_DISPATCH_GUARD_FAILED';
  END IF;
END $task21_guard$;

CREATE TABLE alpha_support_private.staging_alert_dispatch_receipts (
 alert_id uuid PRIMARY KEY REFERENCES public.alpha_support_alert_intents(alert_id) ON DELETE CASCADE,
 source text NOT NULL DEFAULT 'CONNECTED_GMAIL_MANUAL_AGENT'
   CHECK (source='CONNECTED_GMAIL_MANUAL_AGENT'),
 state text NOT NULL DEFAULT 'PREPARED'
   CHECK (state IN ('PREPARED','PROVIDER_ACCEPTED','HUMAN_ACKNOWLEDGED','UNCERTAIN')),
 provider_message_id text UNIQUE,
 provider_accepted_at timestamptz,
 human_ack_message_id text UNIQUE,
 human_ack_at timestamptz,
 prepared_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 CONSTRAINT task21_provider_receipt_consistent CHECK (
  (state='PREPARED' AND provider_message_id IS NULL AND provider_accepted_at IS NULL
    AND human_ack_message_id IS NULL AND human_ack_at IS NULL)
  OR (state='UNCERTAIN' AND human_ack_message_id IS NULL AND human_ack_at IS NULL)
  OR (state='PROVIDER_ACCEPTED' AND provider_message_id IS NOT NULL
    AND provider_accepted_at IS NOT NULL AND human_ack_message_id IS NULL
    AND human_ack_at IS NULL)
  OR (state='HUMAN_ACKNOWLEDGED' AND provider_message_id IS NOT NULL
    AND provider_accepted_at IS NOT NULL AND human_ack_message_id IS NOT NULL
    AND human_ack_at IS NOT NULL)
 )
);
ALTER TABLE alpha_support_private.staging_alert_dispatch_receipts ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE alpha_support_private.staging_alert_dispatch_receipts
 FROM PUBLIC, anon, authenticated, service_role;
COMMENT ON TABLE alpha_support_private.staging_alert_dispatch_receipts IS
 'STAGING ONLY: manual connected-Gmail sender audit. Provider acceptance != delivery or human ACK. Does not alter immutable UNROUTED app alert intents.';
