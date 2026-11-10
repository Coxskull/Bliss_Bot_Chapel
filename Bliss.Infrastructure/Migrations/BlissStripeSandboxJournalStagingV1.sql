-- Bliss Bot dedicated Stripe sandbox event journal.
-- Apply ONLY to a separately approved staging database. NOT an automatic EF migration.
-- Never apply to production or Restricted Preview's database.
BEGIN;

CREATE SCHEMA IF NOT EXISTS bliss_stripe_sandbox;

CREATE TABLE IF NOT EXISTS bliss_stripe_sandbox.event_receipts (
    event_id varchar(120) PRIMARY KEY CHECK (event_id LIKE 'evt_%'),
    event_type varchar(100) NOT NULL,
    object_id varchar(160) NOT NULL,
    payload_sha256 varchar(64) NOT NULL CHECK (payload_sha256 ~ '^[0-9A-F]{64}$'),
    disposition varchar(48) NOT NULL CHECK (disposition IN
        ('NO_FULFILLMENT', 'REVIEW_ONLY', 'RECONCILIATION_REQUIRED')),
    received_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX IF NOT EXISTS bliss_stripe_sandbox_receipt_review
    ON bliss_stripe_sandbox.event_receipts (disposition, received_at);

CREATE OR REPLACE FUNCTION bliss_stripe_sandbox.reject_receipt_changes()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Bliss Stripe receipts are append-only';
END
$$;
DROP TRIGGER IF EXISTS reject_receipt_changes ON bliss_stripe_sandbox.event_receipts;
CREATE TRIGGER reject_receipt_changes
    BEFORE UPDATE OR DELETE OR TRUNCATE ON bliss_stripe_sandbox.event_receipts
    FOR EACH STATEMENT EXECUTE FUNCTION bliss_stripe_sandbox.reject_receipt_changes();

COMMIT;

-- Restricted staging application DB role:
-- GRANT USAGE ON SCHEMA bliss_stripe_sandbox TO <staging_webhook_role>;
-- GRANT SELECT, INSERT ON bliss_stripe_sandbox.event_receipts TO <staging_webhook_role>;
-- Do NOT grant UPDATE, DELETE, TRUNCATE, or access to payment fulfillment tables.
