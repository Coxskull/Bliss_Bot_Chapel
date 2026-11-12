-- ALPHA Task21: production metadata-only incident notification outbox.
-- Existing single-human operator / business mailbox only. Zero new providers.
-- No service-role public grants, customer content, auto contact or retries.
DO $gate$
BEGIN
 IF to_regclass('alpha_support_private.manual_mail_cases') IS NULL OR
    to_regclass('alpha_support_private.manual_privacy_form_cases') IS NULL OR
    to_regclass('alpha_support_private.manual_operator_grants') IS NULL OR
    to_regclass('alpha_support_private.production_incident_alerts') IS NOT NULL OR
    (SELECT count(*) FROM alpha_support_private.manual_operator_grants
     WHERE enabled AND role='GENERAL_SUPPORT') <> 1
 THEN RAISE EXCEPTION 'TASK21_PROD_INCIDENT_OUTBOX_PREREQUISITE_FAILED'; END IF;
END $gate$;

CREATE TABLE alpha_support_private.production_incident_alerts(
  alert_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source_kind text NOT NULL CHECK(source_kind IN ('MAIL','PRIVACY_FORM')),
  source_case_id uuid NOT NULL,
  category text NOT NULL CHECK(category IN ('security','privacy','billing','refund')),
  severity text NOT NULL CHECK(severity IN ('HIGH','CRITICAL')),
  state text NOT NULL DEFAULT 'QUEUED'
    CHECK(state IN ('QUEUED','CLAIMED','PROVIDER_ACCEPTED','UNCERTAIN','HUMAN_ACKNOWLEDGED')),
  claim_token uuid UNIQUE,
  claimed_at timestamptz,
  provider_message_id text UNIQUE
    CHECK(provider_message_id IS NULL OR provider_message_id ~ '^[0-9a-f]{12,40}$'),
  provider_accepted_at timestamptz,
  human_ack_message_id text UNIQUE
    CHECK(human_ack_message_id IS NULL OR human_ack_message_id ~ '^[0-9a-f]{12,40}$'),
  human_ack_at timestamptz,
  queued_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE(source_kind,source_case_id),
  CONSTRAINT incident_claim_state_consistency CHECK (
    (state='QUEUED' AND claim_token IS NULL AND claimed_at IS NULL
      AND provider_message_id IS NULL AND provider_accepted_at IS NULL
      AND human_ack_message_id IS NULL AND human_ack_at IS NULL)
    OR (state IN ('CLAIMED','UNCERTAIN')
      AND claim_token IS NOT NULL AND claimed_at IS NOT NULL
      AND provider_message_id IS NULL AND provider_accepted_at IS NULL
      AND human_ack_message_id IS NULL AND human_ack_at IS NULL)
    OR (state='PROVIDER_ACCEPTED' AND claim_token IS NOT NULL AND claimed_at IS NOT NULL
      AND provider_message_id IS NOT NULL AND provider_accepted_at IS NOT NULL
      AND human_ack_message_id IS NULL AND human_ack_at IS NULL)
    OR (state='HUMAN_ACKNOWLEDGED' AND claim_token IS NOT NULL AND claimed_at IS NOT NULL
      AND provider_message_id IS NOT NULL AND provider_accepted_at IS NOT NULL
      AND human_ack_message_id IS NOT NULL AND human_ack_at IS NOT NULL
      AND human_ack_message_id<>provider_message_id AND human_ack_at>=provider_accepted_at)
  ),
  CONSTRAINT incident_severity_consistency CHECK (
    (category='security' AND severity='CRITICAL')
    OR (category IN ('privacy','billing','refund') AND severity='HIGH')
  )
);
ALTER TABLE alpha_support_private.production_incident_alerts ENABLE ROW LEVEL SECURITY;
CREATE INDEX production_incident_queued
 ON alpha_support_private.production_incident_alerts(queued_at,alert_id)
 WHERE state='QUEUED';

CREATE TABLE alpha_support_private.production_incident_events(
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 alert_id uuid NOT NULL REFERENCES alpha_support_private.production_incident_alerts(alert_id)
   ON DELETE CASCADE,
 state text NOT NULL CHECK(state IN
 ('QUEUED','CLAIMED','PROVIDER_ACCEPTED','UNCERTAIN','HUMAN_ACKNOWLEDGED')),
 occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.production_incident_events ENABLE ROW LEVEL SECURITY;
CREATE INDEX production_incident_events_case
 ON alpha_support_private.production_incident_events(alert_id,event_id);

CREATE FUNCTION alpha_support_private.production_incident_state_guard()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $guard$
BEGIN
 IF (NEW.alert_id,NEW.source_kind,NEW.source_case_id,NEW.category,NEW.severity,NEW.queued_at)
  IS DISTINCT FROM
    (OLD.alert_id,OLD.source_kind,OLD.source_case_id,OLD.category,OLD.severity,OLD.queued_at)
 THEN RAISE EXCEPTION 'Task21 incident identity immutable'; END IF;
 IF NOT (
  (OLD.state='QUEUED' AND NEW.state='CLAIMED')
  OR (OLD.state='CLAIMED' AND NEW.state IN ('PROVIDER_ACCEPTED','UNCERTAIN'))
  OR (OLD.state='PROVIDER_ACCEPTED' AND NEW.state='HUMAN_ACKNOWLEDGED')
 )
 THEN RAISE EXCEPTION 'Task21 incident state transition forbidden'; END IF;
 IF OLD.state='CLAIMED' AND
  (NEW.claim_token IS DISTINCT FROM OLD.claim_token OR
   NEW.claimed_at IS DISTINCT FROM OLD.claimed_at)
 THEN RAISE EXCEPTION 'Task21 claimed sender lease immutable'; END IF;
 IF OLD.state='PROVIDER_ACCEPTED' AND
  (NEW.claim_token IS DISTINCT FROM OLD.claim_token OR
   NEW.claimed_at IS DISTINCT FROM OLD.claimed_at OR
   NEW.provider_message_id IS DISTINCT FROM OLD.provider_message_id OR
   NEW.provider_accepted_at IS DISTINCT FROM OLD.provider_accepted_at)
 THEN RAISE EXCEPTION 'Task21 provider acceptance immutable'; END IF;
 RETURN NEW;
END $guard$;
CREATE TRIGGER production_incident_guard_update
 BEFORE UPDATE ON alpha_support_private.production_incident_alerts
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.production_incident_state_guard();

CREATE FUNCTION alpha_support_private.production_incident_audit()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $audit$
BEGIN
 IF TG_OP='INSERT' OR OLD.state IS DISTINCT FROM NEW.state THEN
  INSERT INTO alpha_support_private.production_incident_events(alert_id,state)
  VALUES(NEW.alert_id,NEW.state);
 END IF;
 RETURN NEW;
END $audit$;
CREATE TRIGGER production_incident_created_audit
 AFTER INSERT ON alpha_support_private.production_incident_alerts
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.production_incident_audit();
CREATE TRIGGER production_incident_changed_audit
 AFTER UPDATE ON alpha_support_private.production_incident_alerts
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.production_incident_audit();

CREATE FUNCTION alpha_support_private.production_incident_events_no_update()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $immut$
BEGIN RAISE EXCEPTION 'Task21 provider/ACK audit event immutable'; END $immut$;
CREATE TRIGGER production_incident_events_immutable
 BEFORE UPDATE ON alpha_support_private.production_incident_events
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.production_incident_events_no_update();

CREATE FUNCTION alpha_support_private.enqueue_production_mail_incident()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $mail$
BEGIN
 IF NEW.category IN ('security','privacy','billing','refund') THEN
  INSERT INTO alpha_support_private.production_incident_alerts
   (source_kind,source_case_id,category,severity)
  VALUES('MAIL',NEW.case_id,NEW.category,
    CASE WHEN NEW.category='security' THEN 'CRITICAL' ELSE 'HIGH' END)
  ON CONFLICT(source_kind,source_case_id) DO NOTHING;
 END IF;
 RETURN NEW;
END $mail$;
CREATE TRIGGER task21_production_mail_incident_queue
 AFTER INSERT ON alpha_support_private.manual_mail_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.enqueue_production_mail_incident();

CREATE FUNCTION alpha_support_private.enqueue_production_privacy_incident()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $privacy$
BEGIN
 INSERT INTO alpha_support_private.production_incident_alerts
   (source_kind,source_case_id,category,severity)
 VALUES('PRIVACY_FORM',NEW.case_id,'privacy','HIGH')
 ON CONFLICT(source_kind,source_case_id) DO NOTHING;
 RETURN NEW;
END $privacy$;
CREATE TRIGGER task21_production_privacy_incident_queue
 AFTER INSERT ON alpha_support_private.manual_privacy_form_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.enqueue_production_privacy_incident();

-- Retention: when the existing source case is deleted under the existing
-- 90-day cleanup, remove the matching alert and its events. No orphan PII.
CREATE FUNCTION alpha_support_private.cleanup_production_incident_source()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $cleanup$
BEGIN
 DELETE FROM alpha_support_private.production_incident_alerts a
 WHERE a.source_kind=TG_ARGV[0] AND a.source_case_id=OLD.case_id;
 RETURN OLD;
END $cleanup$;
CREATE TRIGGER task21_production_mail_incident_retention
 AFTER DELETE ON alpha_support_private.manual_mail_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.cleanup_production_incident_source('MAIL');
CREATE TRIGGER task21_production_privacy_incident_retention
 AFTER DELETE ON alpha_support_private.manual_privacy_form_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.cleanup_production_incident_source('PRIVACY_FORM');

-- Only trusted Postgres service operators (not anon, Auth, service_role or
-- public API) may claim/reconcile. No automatic retry of uncertain sends.
CREATE FUNCTION alpha_support_private.claim_next_production_incident()
RETURNS TABLE(alert_id uuid,claim_token uuid,source_case_id uuid,
 source_kind text,category text,severity text)
LANGUAGE sql SECURITY INVOKER SET search_path=pg_catalog AS $claim$
 WITH picked AS (
  SELECT q.alert_id
  FROM alpha_support_private.production_incident_alerts q
  WHERE q.state='QUEUED'
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

CREATE FUNCTION alpha_support_private.record_production_incident_provider(
 p_alert_id uuid,p_claim_token uuid,p_message_id text,p_provider_at timestamptz
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $accept$
DECLARE n integer;
BEGIN
 IF p_message_id IS NULL OR p_message_id !~ '^[0-9a-f]{12,40}$'
 OR p_provider_at IS NULL OR p_provider_at>clock_timestamp()+interval '5 minutes'
 OR p_provider_at<clock_timestamp()-interval '1 day' THEN RETURN false; END IF;
 UPDATE alpha_support_private.production_incident_alerts
 SET state='PROVIDER_ACCEPTED',provider_message_id=p_message_id,
  provider_accepted_at=p_provider_at
 WHERE alert_id=p_alert_id AND claim_token=p_claim_token AND state='CLAIMED';
 GET DIAGNOSTICS n=ROW_COUNT;
 RETURN n=1;
END $accept$;

CREATE FUNCTION alpha_support_private.mark_production_incident_uncertain(
 p_alert_id uuid,p_claim_token uuid
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $uncertain$
DECLARE n integer;
BEGIN
 UPDATE alpha_support_private.production_incident_alerts SET state='UNCERTAIN'
 WHERE alert_id=p_alert_id AND claim_token=p_claim_token AND state='CLAIMED';
 GET DIAGNOSTICS n=ROW_COUNT;
 RETURN n=1;
END $uncertain$;

CREATE FUNCTION alpha_support_private.record_production_incident_human_ack(
 p_alert_id uuid,p_provider_message_id text,
 p_ack_message_id text,p_ack_at timestamptz
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $human$
DECLARE n integer;
BEGIN
 IF p_ack_message_id IS NULL OR p_ack_message_id !~ '^[0-9a-f]{12,40}$'
 OR p_ack_at IS NULL OR p_ack_at>clock_timestamp()+interval '5 minutes'
 OR p_ack_at<clock_timestamp()-interval '90 days'
 OR p_ack_message_id=p_provider_message_id THEN RETURN false; END IF;
 UPDATE alpha_support_private.production_incident_alerts
 SET state='HUMAN_ACKNOWLEDGED',human_ack_message_id=p_ack_message_id,
  human_ack_at=p_ack_at
 WHERE alert_id=p_alert_id AND state='PROVIDER_ACCEPTED'
 AND provider_message_id=p_provider_message_id
 AND provider_accepted_at<=p_ack_at;
 GET DIAGNOSTICS n=ROW_COUNT;
 RETURN n=1;
END $human$;

REVOKE ALL ON SCHEMA alpha_support_private FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON alpha_support_private.production_incident_alerts,
 alpha_support_private.production_incident_events
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON SEQUENCE alpha_support_private.production_incident_events_event_id_seq
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.production_incident_state_guard()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.production_incident_audit()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.production_incident_events_no_update()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.enqueue_production_mail_incident()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.enqueue_production_privacy_incident()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.cleanup_production_incident_source()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.claim_next_production_incident()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.record_production_incident_provider(uuid,uuid,text,timestamptz)
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.mark_production_incident_uncertain(uuid,uuid)
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.record_production_incident_human_ack(uuid,text,text,timestamptz)
 FROM PUBLIC,anon,authenticated,service_role;

COMMENT ON TABLE alpha_support_private.production_incident_alerts IS
 'Task21: real high-risk support cases only; metadata and internal Gmail IDs. Single-claim sender, provider acceptance and distinct independent human acknowledgment. No PII, customer outreach or automatic retries.';
