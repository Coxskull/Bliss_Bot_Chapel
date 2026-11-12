-- ALPHA Task #21 — application-generated staging incident outbox, zero egress.
-- Isolated developer staging ONLY. No migrations to the live Bliss Bot database.
-- The app INSERT trigger creates a durable metadata-only QUEUED item.
-- A separate authorized sender must explicitly claim/send/record a provider ID.
-- Never infer sent, delivered or ACK from this queue.
DO $task21_stage_guard$
BEGIN
  IF to_regclass('public.alpha_support_alert_intents') IS NULL
     OR to_regclass('alpha_support_private.staging_operator_grants') IS NULL
     OR to_regclass('alpha_support_private.staging_alert_dispatch_receipts') IS NULL
     OR to_regclass('alpha_support_private.staging_app_alert_outbox') IS NOT NULL
     OR NOT EXISTS (
       SELECT 1 FROM public.alpha_support_alert_intents
       WHERE alert_id='1cf5e519-33e2-4d66-8984-185cc93f3641'::uuid
         AND delivery_state='UNROUTED' AND severity='CRITICAL'
         AND assigned_role='SECURITY_REVIEW')
  THEN RAISE EXCEPTION 'TASK21_ISOLATED_STAGING_GATE_FAILED';
  END IF;
END $task21_stage_guard$;

CREATE TABLE alpha_support_private.staging_app_alert_outbox (
  alert_id uuid PRIMARY KEY REFERENCES public.alpha_support_alert_intents(alert_id) ON DELETE RESTRICT,
  state text NOT NULL DEFAULT 'QUEUED'
    CHECK (state IN ('QUEUED','CLAIMED','PROVIDER_ACCEPTED','UNCERTAIN')),
  claim_token uuid UNIQUE,
  claimed_at timestamptz,
  provider_message_id text UNIQUE
    CHECK (provider_message_id IS NULL OR provider_message_id ~ '^[0-9a-f]{12,40}$'),
  provider_accepted_at timestamptz,
  queued_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CONSTRAINT task21_app_alert_state_consistency CHECK (
    (state='QUEUED' AND claim_token IS NULL AND claimed_at IS NULL
      AND provider_message_id IS NULL AND provider_accepted_at IS NULL)
    OR (state='CLAIMED' AND claim_token IS NOT NULL AND claimed_at IS NOT NULL
      AND provider_message_id IS NULL AND provider_accepted_at IS NULL)
    OR (state='PROVIDER_ACCEPTED' AND claim_token IS NOT NULL AND claimed_at IS NOT NULL
      AND provider_message_id IS NOT NULL AND provider_accepted_at IS NOT NULL)
    OR (state='UNCERTAIN' AND claim_token IS NOT NULL AND claimed_at IS NOT NULL
      AND provider_message_id IS NULL AND provider_accepted_at IS NULL)
  )
);
ALTER TABLE alpha_support_private.staging_app_alert_outbox ENABLE ROW LEVEL SECURITY;
CREATE INDEX staging_app_alert_outbox_queue ON alpha_support_private.staging_app_alert_outbox (queued_at,alert_id)
  WHERE state='QUEUED';

CREATE TABLE alpha_support_private.staging_app_alert_events (
  event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  alert_id uuid NOT NULL REFERENCES alpha_support_private.staging_app_alert_outbox(alert_id) ON DELETE RESTRICT,
  state text NOT NULL CHECK(state IN ('QUEUED','CLAIMED','PROVIDER_ACCEPTED','UNCERTAIN')),
  occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.staging_app_alert_events ENABLE ROW LEVEL SECURITY;
CREATE INDEX staging_app_alert_events_order ON alpha_support_private.staging_app_alert_events(alert_id,event_id);

CREATE FUNCTION alpha_support_private.record_staging_app_alert_event()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $task21_events$
BEGIN
  IF TG_OP='UPDATE' AND NEW.state=OLD.state THEN RETURN NEW; END IF;
  INSERT INTO alpha_support_private.staging_app_alert_events(alert_id,state)
  VALUES (NEW.alert_id,NEW.state);
  RETURN NEW;
END $task21_events$;
CREATE TRIGGER staging_app_alert_event_created
 AFTER INSERT ON alpha_support_private.staging_app_alert_outbox
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.record_staging_app_alert_event();
CREATE TRIGGER staging_app_alert_event_transition
 AFTER UPDATE OF state ON alpha_support_private.staging_app_alert_outbox
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.record_staging_app_alert_event();

CREATE FUNCTION alpha_support_private.no_staging_app_alert_event_changes()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $task21_events_immutable$
BEGIN RAISE EXCEPTION 'Task21 staging app alert events are immutable'; END
$task21_events_immutable$;
CREATE TRIGGER staging_app_alert_events_immutable
 BEFORE UPDATE OR DELETE ON alpha_support_private.staging_app_alert_events
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.no_staging_app_alert_event_changes();

CREATE FUNCTION alpha_support_private.on_staging_application_alert()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $task21_application_trigger$
BEGIN
 IF NEW.delivery_state<>'UNROUTED' OR NEW.severity NOT IN ('HIGH','CRITICAL')
   THEN RETURN NEW; END IF;
 IF NOT ((NEW.severity='CRITICAL' AND NEW.assigned_role='SECURITY_REVIEW') OR
         (NEW.severity='HIGH' AND NEW.assigned_role IN ('BILLING_REVIEW','PRIVACY_REVIEW')))
   THEN RAISE EXCEPTION 'TASK21_ALERT_ROLE_SEVERITY_INCONSISTENT'; END IF;
 INSERT INTO alpha_support_private.staging_app_alert_outbox(alert_id) VALUES(NEW.alert_id)
   ON CONFLICT(alert_id) DO NOTHING;
 RETURN NEW;
END $task21_application_trigger$;
CREATE TRIGGER task21_staging_application_alert_queued
 AFTER INSERT ON public.alpha_support_alert_intents
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.on_staging_application_alert();

-- Claims are single-use with row locking. No automatic retry is possible after CLAIMED,
-- even if provider transport crashes before the acceptance receipt can be persisted.
CREATE FUNCTION alpha_support_private.claim_next_staging_app_alert()
RETURNS TABLE(alert_id uuid,claim_token uuid,case_id uuid,severity text,assigned_role text)
LANGUAGE sql SECURITY INVOKER SET search_path=pg_catalog AS $task21_claim$
  WITH selected AS (
    SELECT q.alert_id
    FROM alpha_support_private.staging_app_alert_outbox q
    JOIN public.alpha_support_alert_intents a ON a.alert_id=q.alert_id
    WHERE q.state='QUEUED' AND a.delivery_state='UNROUTED'
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

CREATE FUNCTION alpha_support_private.record_staging_app_provider_receipt(
  p_alert_id uuid,p_claim_token uuid,p_message_id text,p_accepted_at timestamptz
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $task21_accept$
DECLARE v_count integer;
BEGIN
 IF p_message_id IS NULL OR p_message_id !~ '^[0-9a-f]{12,40}$'
   OR p_accepted_at IS NULL OR p_accepted_at > clock_timestamp() + interval '5 minutes'
   OR p_accepted_at < clock_timestamp() - interval '1 day'
 THEN RETURN false; END IF;
 UPDATE alpha_support_private.staging_app_alert_outbox
 SET state='PROVIDER_ACCEPTED',provider_message_id=p_message_id,
     provider_accepted_at=p_accepted_at
 WHERE alert_id=p_alert_id AND claim_token=p_claim_token AND state='CLAIMED';
 GET DIAGNOSTICS v_count=ROW_COUNT;
 RETURN v_count=1;
END $task21_accept$;
CREATE FUNCTION alpha_support_private.mark_staging_app_alert_uncertain(
  p_alert_id uuid,p_claim_token uuid
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $task21_uncertain$
DECLARE v_count integer;
BEGIN
 UPDATE alpha_support_private.staging_app_alert_outbox
 SET state='UNCERTAIN'
 WHERE alert_id=p_alert_id AND claim_token=p_claim_token AND state='CLAIMED';
 GET DIAGNOSTICS v_count=ROW_COUNT;
 RETURN v_count=1;
END $task21_uncertain$;

-- Functions live in a non-exposed schema. Only trusted postgres SQL operators can
-- claim and reconcile. Never grant invoker rights to browser roles/service_role.
REVOKE ALL ON alpha_support_private.staging_app_alert_outbox,
              alpha_support_private.staging_app_alert_events
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON SEQUENCE alpha_support_private.staging_app_alert_events_event_id_seq
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.record_staging_app_alert_event()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.no_staging_app_alert_event_changes()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.on_staging_application_alert()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.claim_next_staging_app_alert()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.record_staging_app_provider_receipt(uuid,uuid,text,timestamptz)
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.mark_staging_app_alert_uncertain(uuid,uuid)
 FROM PUBLIC,anon,authenticated,service_role;

COMMENT ON TABLE alpha_support_private.staging_app_alert_outbox IS
 'STAGING ONLY. App-origin intent queues metadata, not email. An authorized separate Gmail sender must claim once, save real provider receipt and wait for independent human ACK. No automatic retries.';
