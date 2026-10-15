-- Configurable fixture profiles only; no verified/approved Stripe account is inferred.
CREATE TABLE financials.mock_provider_bindings(
 operation_id uuid PRIMARY KEY REFERENCES financials.payment_operations(id),
 profile_id varchar(64) NOT NULL CHECK(profile_id ~ '^[A-Za-z0-9_-]+$'),
 payment_intent_id varchar(120) NOT NULL UNIQUE CHECK(left(payment_intent_id,11)='pi_fixture_'),
 payer_account uuid NOT NULL CHECK(payer_account<>'00000000-0000-0000-0000-000000000000'::uuid),recipient_owner uuid NOT NULL CHECK(recipient_owner<>'00000000-0000-0000-0000-000000000000'::uuid),recorded_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE financials.mock_provider_events(
 event_id varchar(120) PRIMARY KEY CHECK(left(event_id,4)='evt_'),
 operation_id uuid NOT NULL REFERENCES financials.mock_provider_bindings(operation_id),
 fingerprint varchar(64) NOT NULL CHECK(fingerprint ~ '^[0-9A-Fa-f]{64}$'),journal_id uuid NOT NULL REFERENCES financials.journals(id),
 recorded_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX mock_events_by_operation ON financials.mock_provider_events(operation_id);
CREATE TRIGGER mock_bindings_no_mutation BEFORE UPDATE OR DELETE ON financials.mock_provider_bindings FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_events_no_mutation BEFORE UPDATE OR DELETE ON financials.mock_provider_events FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_bindings_no_truncate BEFORE TRUNCATE ON financials.mock_provider_bindings FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_events_no_truncate BEFORE TRUNCATE ON financials.mock_provider_events FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE FUNCTION financials.check_mock_binding() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=NEW.operation_id AND state='BOUND' AND payment_intent_id=NEW.payment_intent_id) THEN RAISE EXCEPTION 'Mock binding lacks matching dispatch evidence'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER mock_binding_authority AFTER INSERT ON financials.mock_provider_bindings DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION financials.check_mock_binding();
CREATE FUNCTION financials.check_mock_event() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM financials.mock_provider_bindings b JOIN financials.payment_captures c ON c.operation_id=b.operation_id
 WHERE b.operation_id=NEW.operation_id AND c.journal_id=NEW.journal_id AND c.payment_intent_id=b.payment_intent_id) THEN RAISE EXCEPTION 'Mock event attribution lacks matching capture'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER mock_event_attribution AFTER INSERT ON financials.mock_provider_events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION financials.check_mock_event();
