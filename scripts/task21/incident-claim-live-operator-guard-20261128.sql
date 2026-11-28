-- Separate additive Task21 runtime hardening; do not replay original install.
-- No staff grant, claim, send, acknowledgment, or existing case mutation.
BEGIN;
DO $gate$
BEGIN
 IF to_regprocedure('alpha_support_private.claim_next_production_incident()') IS NULL
 OR to_regprocedure('public.alpha_support_manual_operator_authorized(uuid)') IS NULL
 OR to_regclass('alpha_support_private.manual_operator_grants') IS NULL
 OR to_regclass('alpha_support_private.production_incident_alerts') IS NULL
 THEN RAISE EXCEPTION 'TASK21_OPERATOR_CLAIM_GUARD_PREREQUISITE_FAILED'; END IF;
END $gate$;
CREATE OR REPLACE FUNCTION alpha_support_private.claim_next_production_incident()
RETURNS TABLE(alert_id uuid,claim_token uuid,source_case_id uuid,
 source_kind text,category text,severity text)
LANGUAGE sql SECURITY INVOKER SET search_path=pg_catalog AS $claim$
 WITH picked AS (
  SELECT q.alert_id
  FROM alpha_support_private.production_incident_alerts q
  WHERE q.state='QUEUED'
    AND (SELECT count(*) FROM alpha_support_private.manual_operator_grants
         WHERE enabled)=1
    AND (SELECT count(*) FROM alpha_support_private.manual_operator_grants g
         WHERE public.alpha_support_manual_operator_authorized(g.user_id))=1
  ORDER BY q.queued_at,q.alert_id
  FOR UPDATE SKIP LOCKED LIMIT 1
 ), changed AS (
  UPDATE alpha_support_private.production_incident_alerts q
  SET state='CLAIMED',claim_token=gen_random_uuid(),claimed_at=clock_timestamp()
  FROM picked p
  WHERE q.alert_id=p.alert_id AND q.state='QUEUED'
  RETURNING q.alert_id,q.claim_token,q.source_case_id,q.source_kind,q.category,q.severity
 )
 SELECT * FROM changed;
$claim$;
REVOKE EXECUTE ON FUNCTION alpha_support_private.claim_next_production_incident()
 FROM PUBLIC,anon,authenticated,service_role;
COMMIT;
