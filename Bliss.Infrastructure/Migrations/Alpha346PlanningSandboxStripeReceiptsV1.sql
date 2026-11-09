-- ALPHA-346: isolated TEST-MODE Stripe Payment Link webhook evidence only.
-- Apply only to a disposable/staging Postgres fixture after authorized review.
-- No real customer, financial journal, payout, invoice or fulfillment tables.
CREATE SCHEMA IF NOT EXISTS alpha_sandbox;
REVOKE ALL ON SCHEMA alpha_sandbox FROM PUBLIC;

CREATE TABLE alpha_sandbox.planning_checkout_webhook_receipts (
  event_id varchar(150) PRIMARY KEY CHECK (event_id ~ '^evt_[A-Za-z0-9_]{7,146}$'),
  checkout_session_id varchar(150) NOT NULL CHECK (checkout_session_id ~ '^cs_test_[A-Za-z0-9_]{7,142}$'),
  payment_intent_id varchar(150) NOT NULL CHECK (payment_intent_id ~ '^pi_[A-Za-z0-9_]{7,146}$'),
  receipt_state varchar(48) NOT NULL CHECK (receipt_state IN (
    'PAID_TEST_REVIEW_ONLY', 'PENDING_TEST_NO_FULFILLMENT', 'FAILED_TEST_NO_FULFILLMENT'
  )),
  payload_sha256 char(64) NOT NULL CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
  observed_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX planning_sandbox_session_idx
  ON alpha_sandbox.planning_checkout_webhook_receipts(checkout_session_id);
CREATE INDEX planning_sandbox_intent_idx
  ON alpha_sandbox.planning_checkout_webhook_receipts(payment_intent_id);
-- No second PAID test receipt for an already observed PaymentIntent.
CREATE UNIQUE INDEX planning_sandbox_one_paid_test_per_intent
  ON alpha_sandbox.planning_checkout_webhook_receipts(payment_intent_id)
  WHERE receipt_state = 'PAID_TEST_REVIEW_ONLY';
REVOKE ALL ON alpha_sandbox.planning_checkout_webhook_receipts FROM PUBLIC;

CREATE FUNCTION alpha_sandbox.deny_receipt_mutation() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog AS $$
BEGIN
  RAISE EXCEPTION 'Immutable ALPHA-346 sandbox webhook receipt';
END $$;
CREATE TRIGGER planning_sandbox_no_update_delete
  BEFORE UPDATE OR DELETE ON alpha_sandbox.planning_checkout_webhook_receipts
  FOR EACH ROW EXECUTE FUNCTION alpha_sandbox.deny_receipt_mutation();
CREATE TRIGGER planning_sandbox_no_truncate
  BEFORE TRUNCATE ON alpha_sandbox.planning_checkout_webhook_receipts
  FOR EACH STATEMENT EXECUTE FUNCTION alpha_sandbox.deny_receipt_mutation();

COMMENT ON TABLE alpha_sandbox.planning_checkout_webhook_receipts IS
  'Stripe SANDBOX signed event evidence only; not revenue, a charge journal, an approved order, or fulfillment authority. No customer PII.';
