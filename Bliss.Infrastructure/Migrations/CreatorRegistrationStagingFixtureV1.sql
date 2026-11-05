-- COM-261-004: isolated creator intake SQL fixture. NOT an EF migration.
-- Only for freshly created disposable PostgreSQL CI databases.
-- Never execute against production, hosted staging, Supabase or Railway.
CREATE SCHEMA agm_creator_fixture;
REVOKE ALL ON SCHEMA agm_creator_fixture FROM PUBLIC;

CREATE TABLE agm_creator_fixture.intakes (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  request_key varchar(100) NOT NULL UNIQUE
    CHECK (request_key ~ '^[A-Za-z0-9_-]{8,100}$'),
  display_name varchar(120) NOT NULL
    CHECK (display_name ~ '^Synthetic [A-Za-z][A-Za-z0-9 ._-]{0,109}$'),
  normalized_email varchar(254) NOT NULL UNIQUE
    CHECK (normalized_email ~ '^[a-z0-9._%+-]+@example[.]invalid$'),
  channel_url varchar(300) NOT NULL
    CHECK (channel_url ~ '^https://example[.]invalid/[-/a-zA-Z0-9._~%]+$'),
  market varchar(100) NOT NULL
    CHECK (market ~ '^Test [A-Za-z][A-Za-z ]{0,94}$'),
  category varchar(16) NOT NULL
    CHECK (category IN ('podcast','livestream','video','other')),
  processing_ack boolean NOT NULL CHECK (processing_ack),
  marketing_opt_in boolean NOT NULL DEFAULT false,
  environment varchar(20) NOT NULL DEFAULT 'STAGING_SYNTHETIC'
    CHECK (environment = 'STAGING_SYNTHETIC'),
  source varchar(32) NOT NULL DEFAULT 'BLISS_INTERNAL_TEST'
    CHECK (source = 'BLISS_INTERNAL_TEST'),
  consent_purpose varchar(40) NOT NULL DEFAULT 'CREATOR_INTAKE_STAGING_ONLY'
    CHECK (consent_purpose = 'CREATOR_INTAKE_STAGING_ONLY'),
  notice_version varchar(40) NOT NULL DEFAULT 'AGM-CREATOR-STAGING-ONLY-V1'
    CHECK (notice_version = 'AGM-CREATOR-STAGING-ONLY-V1'),
  consent_recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE agm_creator_fixture.receipts (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  intake_id uuid NOT NULL UNIQUE
    REFERENCES agm_creator_fixture.intakes(id),
  event varchar(32) NOT NULL DEFAULT 'CREATOR_INTEREST_STAGED'
    CHECK (event = 'CREATOR_INTEREST_STAGED'),
  notice_version varchar(40) NOT NULL
    CHECK (notice_version = 'AGM-CREATOR-STAGING-ONLY-V1'),
  consent_recorded_at timestamptz NOT NULL,
  receipt_recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
REVOKE ALL ON ALL TABLES IN SCHEMA agm_creator_fixture FROM PUBLIC;

CREATE FUNCTION agm_creator_fixture.deny_historical_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'Immutable staging creator intake and receipt' USING ERRCODE = '23514';
END;
$$;
CREATE TRIGGER intakes_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
  ON agm_creator_fixture.intakes FOR EACH STATEMENT
  EXECUTE FUNCTION agm_creator_fixture.deny_historical_mutation();
CREATE TRIGGER receipts_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
  ON agm_creator_fixture.receipts FOR EACH STATEMENT
  EXECUTE FUNCTION agm_creator_fixture.deny_historical_mutation();

CREATE FUNCTION agm_creator_fixture.register_synthetic(
  p_key text, p_name text, p_email text, p_channel text, p_market text,
  p_category text, p_ack boolean, p_marketing boolean
) RETURNS TABLE(intake_id uuid, receipt_id uuid, is_replay boolean)
LANGUAGE plpgsql SECURITY DEFINER
SET search_path = pg_catalog, agm_creator_fixture
AS $$
DECLARE
  v_email text := lower(btrim(p_email));
  v_name text := btrim(p_name);
  v_channel text := btrim(p_channel);
  v_market text := btrim(p_market);
  v_id uuid;
  v_receipt uuid;
  v_when timestamptz;
  v_existing agm_creator_fixture.intakes%ROWTYPE;
BEGIN
  IF p_key IS NULL OR p_key !~ '^[A-Za-z0-9_-]{8,100}$'
    OR v_name IS NULL OR length(v_name) > 120
    OR v_name !~ '^Synthetic [A-Za-z][A-Za-z0-9 ._-]{0,109}$'
    OR v_email IS NULL OR length(v_email) > 254
    OR v_email !~ '^[a-z0-9._%+-]+@example[.]invalid$'
    OR v_channel IS NULL OR length(v_channel) > 300
    OR v_channel !~ '^https://example[.]invalid/[-/a-zA-Z0-9._~%]+$'
    OR v_market IS NULL OR length(v_market) > 100
    OR v_market !~ '^Test [A-Za-z][A-Za-z ]{0,94}$'
    OR p_category IS NULL OR p_category NOT IN ('podcast','livestream','video','other')
    OR p_ack IS DISTINCT FROM true OR p_marketing IS NULL
  THEN
    RAISE EXCEPTION 'Only explicit, strictly fictional and bounded staging intake is allowed'
      USING ERRCODE = '22023';
  END IF;

  -- Serialize on canonical identity across independent request IDs.
  PERFORM pg_advisory_xact_lock(hashtextextended(v_email, 261004));
  SELECT i.* INTO v_existing FROM agm_creator_fixture.intakes i
  WHERE i.normalized_email = v_email FOR UPDATE;
  IF FOUND THEN
    IF v_existing.request_key != p_key
      OR v_existing.display_name != v_name OR v_existing.channel_url != v_channel
      OR v_existing.market != v_market OR v_existing.category != p_category
      OR v_existing.marketing_opt_in != p_marketing
    THEN
      RAISE EXCEPTION 'Conflicting identity or idempotency key'
        USING ERRCODE = '22023';
    END IF;
    SELECT r.id INTO v_receipt FROM agm_creator_fixture.receipts r
    WHERE r.intake_id = v_existing.id;
    IF v_receipt IS NULL THEN
      RAISE EXCEPTION 'Staging receipt integrity violation' USING ERRCODE = '23514';
    END IF;
    RETURN QUERY SELECT v_existing.id, v_receipt, true;
    RETURN;
  END IF;

  INSERT INTO agm_creator_fixture.intakes
    (request_key,display_name,normalized_email,channel_url,market,
     category,processing_ack,marketing_opt_in)
  VALUES (p_key,v_name,v_email,v_channel,v_market,p_category,true,p_marketing)
  RETURNING id,consent_recorded_at INTO v_id,v_when;

  INSERT INTO agm_creator_fixture.receipts
    (intake_id,notice_version,consent_recorded_at)
  VALUES (v_id,'AGM-CREATOR-STAGING-ONLY-V1',v_when)
  RETURNING id INTO v_receipt;
  RETURN QUERY SELECT v_id,v_receipt,false;
END;
$$;
-- PostgreSQL grants EXECUTE on functions to PUBLIC by default: explicitly revoke.
REVOKE ALL ON FUNCTION agm_creator_fixture.deny_historical_mutation() FROM PUBLIC;
REVOKE ALL ON FUNCTION agm_creator_fixture.register_synthetic(
  text,text,text,text,text,text,boolean,boolean) FROM PUBLIC;
