-- AGM-LAUNCH-014: DISPOSABLE financial_test fixture only.
-- Never apply in production. Digest-only, synthetic visitor consent intents.
CREATE SCHEMA IF NOT EXISTS agm_consent_fixture;
CREATE TABLE IF NOT EXISTS agm_consent_fixture.events (
  id uuid PRIMARY KEY,
  advertiser_id uuid NOT NULL,
  workspace_id uuid NOT NULL,
  handoff_id uuid NOT NULL,
  session_id uuid NOT NULL,
  quote_id uuid NOT NULL,
  source_slug varchar(96) NOT NULL CHECK(source_slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  actor_subject varchar(128) NOT NULL CHECK(actor_subject LIKE 'fixture-visitor-%'),
  decision varchar(8) NOT NULL CHECK(decision IN ('OPT_IN','REVOKE')),
  purpose varchar(40) NOT NULL DEFAULT 'CAMPAIGN_TRANSCRIPT_CORRELATION'
    CHECK(purpose = 'CAMPAIGN_TRANSCRIPT_CORRELATION'),
  transcript_sha256 char(64) NOT NULL CHECK(transcript_sha256 ~ '^[0-9A-F]{64}$'),
  turn_count integer NOT NULL CHECK(turn_count BETWEEN 1 AND 10000),
  version integer NOT NULL CHECK(version BETWEEN 1 AND 100),
  expected_version integer NOT NULL CHECK(expected_version = version - 1),
  retry_key varchar(100) NOT NULL CHECK(length(btrim(retry_key)) BETWEEN 8 AND 100),
  previous_sha256 char(64) NOT NULL CHECK(previous_sha256 ~ '^[0-9A-F]{64}$'),
  event_sha256 char(64) NOT NULL CHECK(event_sha256 ~ '^[0-9A-F]{64}$'),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE (handoff_id,version),
  UNIQUE (handoff_id,actor_subject,retry_key),
  CHECK (length(btrim(actor_subject)) > 16)
);
CREATE INDEX IF NOT EXISTS consent_fixture_scope_events
  ON agm_consent_fixture.events(advertiser_id,workspace_id,handoff_id,version);
CREATE OR REPLACE FUNCTION agm_consent_fixture.reject_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'AGM fixture consent history is append-only'
    USING ERRCODE='23514';
END $$;
DROP TRIGGER IF EXISTS consent_no_update_delete ON agm_consent_fixture.events;
CREATE TRIGGER consent_no_update_delete BEFORE UPDATE OR DELETE ON agm_consent_fixture.events
FOR EACH ROW EXECUTE FUNCTION agm_consent_fixture.reject_mutation();
DROP TRIGGER IF EXISTS consent_no_truncate ON agm_consent_fixture.events;
CREATE TRIGGER consent_no_truncate BEFORE TRUNCATE ON agm_consent_fixture.events
FOR EACH STATEMENT EXECUTE FUNCTION agm_consent_fixture.reject_mutation();
