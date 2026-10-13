-- Mock observations only; no account verification or actual provider activity.
CREATE TABLE financials.mock_payment_recoveries(
 id uuid PRIMARY KEY,operation_id uuid NOT NULL UNIQUE REFERENCES financials.payment_operations(id),
 profile_id varchar(64) NOT NULL CHECK(profile_id ~ '^[A-Za-z0-9_-]+$'),
 payment_intent_id varchar(120) NOT NULL UNIQUE CHECK(left(payment_intent_id,11)='pi_fixture_'),
 provider_status varchar(64) NOT NULL CHECK(provider_status IN ('requires_payment_method','requires_confirmation','requires_action','processing','requires_capture','canceled','succeeded')),
 actor_subject varchar(200) NOT NULL,recorded_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE financials.mock_payment_status_events(
 event_id varchar(120) PRIMARY KEY CHECK(left(event_id,4)='evt_'),
 operation_id uuid NOT NULL REFERENCES financials.payment_operations(id),profile_id varchar(64) NOT NULL,
 payment_intent_id varchar(120) NOT NULL CHECK(left(payment_intent_id,11)='pi_fixture_'),
 state text NOT NULL CHECK(state IN ('FAILED','CANCELED','PROCESSING')),
 fingerprint varchar(64) NOT NULL CHECK(fingerprint ~ '^[0-9A-Fa-f]{64}$'),
 recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX mock_status_by_operation ON financials.mock_payment_status_events(operation_id,recorded_at);
CREATE TRIGGER mock_recovery_no_mutation BEFORE UPDATE OR DELETE ON financials.mock_payment_recoveries FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_status_no_mutation BEFORE UPDATE OR DELETE ON financials.mock_payment_status_events FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_recovery_no_truncate BEFORE TRUNCATE ON financials.mock_payment_recoveries FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_status_no_truncate BEFORE TRUNCATE ON financials.mock_payment_status_events FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE OR REPLACE FUNCTION financials.check_mock_binding() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=NEW.operation_id AND state='BOUND' AND payment_intent_id=NEW.payment_intent_id)
 AND NOT EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=NEW.operation_id AND payment_intent_id=NEW.payment_intent_id AND profile_id=NEW.profile_id)
 THEN RAISE EXCEPTION 'Mock binding lacks matching dispatch/recovery evidence'; END IF;
 IF EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=NEW.operation_id AND profile_id<>NEW.profile_id)
 OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=NEW.operation_id AND profile_id<>NEW.profile_id)
 THEN RAISE EXCEPTION 'Mock profile cannot be rebound'; END IF;
 RETURN NULL;
END $$;
CREATE FUNCTION financials.check_mock_recovery() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts WHERE operation_id=NEW.operation_id AND started_at<=clock_timestamp()-interval '20 hours')
 OR EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts a WHERE operation_id=NEW.operation_id AND lease_until>clock_timestamp() AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results r WHERE r.attempt_id=a.id))
 OR EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE state='BOUND' AND (operation_id=NEW.operation_id OR payment_intent_id=NEW.payment_intent_id))
 OR EXISTS(SELECT 1 FROM financials.payment_captures WHERE operation_id=NEW.operation_id OR payment_intent_id=NEW.payment_intent_id)
 THEN RAISE EXCEPTION 'Recovery requires aged unresolved fixture authority'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER mock_recovery_authority AFTER INSERT ON financials.mock_payment_recoveries DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION financials.check_mock_recovery();
CREATE FUNCTION financials.check_mock_status() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=NEW.operation_id AND state='BOUND' AND payment_intent_id=NEW.payment_intent_id)
 AND NOT EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=NEW.operation_id AND payment_intent_id=NEW.payment_intent_id AND profile_id=NEW.profile_id)
 THEN RAISE EXCEPTION 'Status lacks matching mock identity'; END IF;
 IF EXISTS(SELECT 1 FROM financials.mock_provider_bindings WHERE operation_id=NEW.operation_id AND profile_id<>NEW.profile_id)
 OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=NEW.operation_id AND profile_id<>NEW.profile_id)
 OR EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=NEW.operation_id AND profile_id<>NEW.profile_id)
 OR EXISTS(SELECT 1 FROM financials.mock_provider_events WHERE event_id=NEW.event_id)
 OR EXISTS(SELECT 1 FROM financials.stripe_receipts WHERE event_id=NEW.event_id)
 THEN RAISE EXCEPTION 'Status profile/event conflicts with immutable evidence'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER mock_status_authority AFTER INSERT ON financials.mock_payment_status_events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION financials.check_mock_status();
