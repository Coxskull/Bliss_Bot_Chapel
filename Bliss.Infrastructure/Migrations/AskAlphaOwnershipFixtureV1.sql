-- AGM-LAUNCH-015 Stage B. Isolated CI ONLY: financial_test on 127.0.0.1.
-- Not an EF migration; do not apply to Supabase/Railway/production.
CREATE SCHEMA IF NOT EXISTS agm_ownership_fixture;
CREATE TABLE IF NOT EXISTS agm_ownership_fixture.receipts (
  receipt_id uuid PRIMARY KEY,
  workspace_id uuid NOT NULL,
  session_id uuid NOT NULL UNIQUE,
  source_slug varchar(96) NOT NULL CHECK (source_slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  issuer varchar(200) NOT NULL,
  subject_sha256 char(64) NOT NULL CHECK(subject_sha256 ~ '^[0-9A-F]{64}$'),
  signature_sha256 char(64) NOT NULL CHECK(signature_sha256 ~ '^[0-9A-F]{64}$'),
  issued_at timestamptz NOT NULL,
  expires_at timestamptz NOT NULL,
  CHECK (expires_at > issued_at AND expires_at <= issued_at + interval '1 day')
);
CREATE TABLE IF NOT EXISTS agm_ownership_fixture.revocations (
  revocation_id uuid PRIMARY KEY,
  receipt_id uuid NOT NULL UNIQUE REFERENCES agm_ownership_fixture.receipts(receipt_id),
  revoked_at timestamptz NOT NULL,
  reason_code varchar(32) NOT NULL CHECK (reason_code IN ('VISITOR_WITHDRAWAL','SECURITY_REVOKE','FIXTURE_CLEANUP'))
);
CREATE OR REPLACE FUNCTION agm_ownership_fixture.block_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'AGM ownership fixture receipts and revocations are append-only' USING ERRCODE='23514';
END $$;
DROP TRIGGER IF EXISTS ownership_no_edit ON agm_ownership_fixture.receipts;
CREATE TRIGGER ownership_no_edit BEFORE UPDATE OR DELETE ON agm_ownership_fixture.receipts
 FOR EACH ROW EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
DROP TRIGGER IF EXISTS ownership_no_truncate ON agm_ownership_fixture.receipts;
CREATE TRIGGER ownership_no_truncate BEFORE TRUNCATE ON agm_ownership_fixture.receipts
 FOR EACH STATEMENT EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
DROP TRIGGER IF EXISTS revocation_no_edit ON agm_ownership_fixture.revocations;
CREATE TRIGGER revocation_no_edit BEFORE UPDATE OR DELETE ON agm_ownership_fixture.revocations
 FOR EACH ROW EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
DROP TRIGGER IF EXISTS revocation_no_truncate ON agm_ownership_fixture.revocations;
CREATE TRIGGER revocation_no_truncate BEFORE TRUNCATE ON agm_ownership_fixture.revocations
 FOR EACH STATEMENT EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
