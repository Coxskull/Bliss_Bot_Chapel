-- AGM-LAUNCH-015 Stage E: DISPOSABLE FINANCIAL_TEST ONLY.
-- Adds an atomic synthetic session+ownership fixture. Not a hosted EF migration.
CREATE TABLE IF NOT EXISTS agm_ownership_fixture.new_sessions (
  session_id uuid PRIMARY KEY REFERENCES agm_ownership_fixture.receipts(session_id),
  receipt_id uuid NOT NULL UNIQUE REFERENCES agm_ownership_fixture.receipts(receipt_id),
  workspace_id uuid NOT NULL,
  issuer varchar(200) NOT NULL,
  subject_sha256 char(64) NOT NULL CHECK(subject_sha256 ~ '^[0-9A-F]{64}$'),
  source_slug varchar(96) NOT NULL CHECK(source_slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  idempotency_key varchar(100) NOT NULL CHECK(idempotency_key ~ '^[a-zA-Z0-9_-]{8,100}$'),
  created_at timestamptz NOT NULL,
  UNIQUE (workspace_id,issuer,subject_sha256,idempotency_key)
);
DROP TRIGGER IF EXISTS new_session_no_edit ON agm_ownership_fixture.new_sessions;
CREATE TRIGGER new_session_no_edit BEFORE UPDATE OR DELETE ON agm_ownership_fixture.new_sessions
 FOR EACH ROW EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
DROP TRIGGER IF EXISTS new_session_no_truncate ON agm_ownership_fixture.new_sessions;
CREATE TRIGGER new_session_no_truncate BEFORE TRUNCATE ON agm_ownership_fixture.new_sessions
 FOR EACH STATEMENT EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
