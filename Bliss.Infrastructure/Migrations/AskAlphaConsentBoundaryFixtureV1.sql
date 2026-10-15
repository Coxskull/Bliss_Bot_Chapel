-- AGM-LAUNCH-015 Stage G: disposable PostgreSQL fixture only.
-- DO NOT apply to hosted Supabase, Railway or production Wedding Planner.
-- Consent for a real visitor can only come from that visitor, not owner signoff.
CREATE TABLE IF NOT EXISTS agm_ownership_fixture.consent_events (
  event_id uuid PRIMARY KEY,
  session_id uuid NOT NULL REFERENCES agm_ownership_fixture.new_sessions(session_id),
  workspace_id uuid NOT NULL,
  issuer varchar(200) NOT NULL,
  subject_sha256 char(64) NOT NULL CHECK(subject_sha256 ~ '^[0-9A-F]{64}$'),
  action varchar(12) NOT NULL CHECK(action IN ('OPT_IN','REVOKE')),
  purpose varchar(64) NOT NULL CHECK(purpose = 'CREATIVE_PREVIEW_ONLY'),
  consent_version varchar(64) NOT NULL CHECK(consent_version = 'AGM-CONSENT-FIXTURE-V1'),
  receipt_key varchar(100) NOT NULL CHECK(receipt_key ~ '^[A-Za-z0-9_-]{8,100}$'),
  occurred_at timestamptz NOT NULL,
  UNIQUE (session_id,action),
  UNIQUE (session_id,receipt_key)
);
CREATE OR REPLACE FUNCTION agm_ownership_fixture.guard_consent_event()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
  scope agm_ownership_fixture.new_sessions%ROWTYPE;
  opt_in_at timestamptz;
  prior_revoke boolean;
BEGIN
  -- Serialize all state transitions on this session even under concurrent inserts.
  PERFORM pg_advisory_xact_lock(hashtextextended(NEW.session_id::text, 4117));
  SELECT * INTO scope FROM agm_ownership_fixture.new_sessions
    WHERE session_id=NEW.session_id FOR SHARE;
  IF NOT FOUND OR scope.workspace_id <> NEW.workspace_id OR
     scope.issuer <> NEW.issuer OR scope.subject_sha256 <> NEW.subject_sha256 THEN
    RAISE EXCEPTION 'Synthetic visitor consent scope mismatch' USING ERRCODE='23514';
  END IF;
  SELECT occurred_at INTO opt_in_at FROM agm_ownership_fixture.consent_events
    WHERE session_id=NEW.session_id AND action='OPT_IN';
  SELECT EXISTS(SELECT 1 FROM agm_ownership_fixture.consent_events
    WHERE session_id=NEW.session_id AND action='REVOKE') INTO prior_revoke;
  IF prior_revoke OR
     (NEW.action='OPT_IN' AND (opt_in_at IS NOT NULL OR NEW.occurred_at < scope.created_at)) OR
     (NEW.action='REVOKE' AND (opt_in_at IS NULL OR NEW.occurred_at < opt_in_at)) THEN
    RAISE EXCEPTION 'Synthetic consent transition denied' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS consent_guard_insert ON agm_ownership_fixture.consent_events;
CREATE TRIGGER consent_guard_insert BEFORE INSERT ON agm_ownership_fixture.consent_events
 FOR EACH ROW EXECUTE FUNCTION agm_ownership_fixture.guard_consent_event();
DROP TRIGGER IF EXISTS consent_no_edit ON agm_ownership_fixture.consent_events;
CREATE TRIGGER consent_no_edit BEFORE UPDATE OR DELETE ON agm_ownership_fixture.consent_events
 FOR EACH ROW EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
DROP TRIGGER IF EXISTS consent_no_truncate ON agm_ownership_fixture.consent_events;
CREATE TRIGGER consent_no_truncate BEFORE TRUNCATE ON agm_ownership_fixture.consent_events
 FOR EACH STATEMENT EXECUTE FUNCTION agm_ownership_fixture.block_mutation();
