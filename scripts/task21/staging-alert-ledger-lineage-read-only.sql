-- ALPHA GLOBAL MEDIA Task #21 — persistent STAGING-only support ledger integrity check.
-- Executed read-only against existing Supabase development project:
--   wdlmcriajgsretgyusmo (alpha-first-five-staging)
-- No DDL, DML, HTTP calls, email, identity access or data export.
-- Returns only aggregate counts and one scoped classification.
-- Do NOT run this against production. A PASS is NOT production incident handling
-- or proof that the application-generated email has a verified human ACK.
-- Manual-email ACK and application-generated alerts are separate provenance lanes.
WITH
app AS (
 SELECT count(*)::int AS total,
   count(*) FILTER (
     WHERE state='PROVIDER_ACCEPTED'
       AND provider_message_id IS NOT NULL
       AND provider_accepted_at IS NOT NULL
       AND claim_token IS NOT NULL
   )::int AS provider_accepted_with_receipt,
   count(*) FILTER (
     WHERE state='PROVIDER_ACCEPTED'
       AND (provider_message_id IS NULL
         OR provider_accepted_at IS NULL
         OR claim_token IS NULL)
   )::int AS invalid_provider_acceptance,
   count(*) FILTER (
     WHERE NOT EXISTS (
       SELECT 1 FROM public.alpha_support_alert_intents i
       WHERE i.alert_id=o.alert_id
     )
   )::int AS orphan_count
 FROM alpha_support_private.staging_app_alert_outbox o
),
manual AS (
 SELECT count(*)::int AS total,
   count(*) FILTER (
     WHERE state='HUMAN_ACKNOWLEDGED'
       AND provider_message_id IS NOT NULL
       AND provider_accepted_at IS NOT NULL
       AND human_ack_message_id IS NOT NULL
       AND human_ack_at IS NOT NULL
       AND provider_message_id<>human_ack_message_id
   )::int AS structurally_valid_manual_ack,
   count(*) FILTER (
     WHERE state='HUMAN_ACKNOWLEDGED'
       AND (provider_message_id IS NULL
         OR human_ack_message_id IS NULL
         OR provider_message_id=human_ack_message_id
         OR provider_accepted_at IS NULL
         OR human_ack_at IS NULL)
   )::int AS invalid_manual_ack
 FROM alpha_support_private.staging_alert_dispatch_receipts
),
overlap AS (
 SELECT count(*)::int AS overlapping_alert_ids
 FROM alpha_support_private.staging_app_alert_outbox o
 JOIN alpha_support_private.staging_alert_dispatch_receipts m USING(alert_id)
),
privileges AS (
 SELECT count(*)::int AS protected_tables,
   coalesce(
     bool_and(
       c.relrowsecurity
       AND NOT has_table_privilege('anon',c.oid,'SELECT')
       AND NOT has_table_privilege('authenticated',c.oid,'SELECT')
       AND NOT has_table_privilege('service_role',c.oid,'UPDATE')
     ),
     false
   ) AS private_access_guard
 FROM pg_class c
 JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname='alpha_support_private'
   AND c.relname IN (
     'staging_app_alert_outbox',
     'staging_alert_dispatch_receipts'
   )
)
SELECT
 app.total AS app_alerts,
 app.provider_accepted_with_receipt,
 app.invalid_provider_acceptance,
 app.orphan_count AS app_orphans,
 manual.total AS manual_alert_receipts,
 manual.structurally_valid_manual_ack,
 manual.invalid_manual_ack,
 overlap.overlapping_alert_ids,
 CASE
   WHEN app.invalid_provider_acceptance=0
     AND app.orphan_count=0
     AND manual.invalid_manual_ack=0
     AND overlap.overlapping_alert_ids=0
     AND privileges.protected_tables=2
     AND privileges.private_access_guard
   THEN 'PASS_STAGING_LEDGER_LINEAGE_ONLY'
   ELSE 'BLOCKED_LEDGER_LINEAGE'
 END AS acceptance
FROM app,manual,overlap,privileges;
