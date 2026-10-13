-- AGM-LAUNCH-010-SUB-001. Explicit isolated schema rehearsal only; not an application EF migration.
CREATE SCHEMA financials;
CREATE TABLE financials.journals (
 id uuid PRIMARY KEY, merchant_id uuid NOT NULL, module text NOT NULL CHECK(module IN ('BlissBot','CreatorAcademy','WeddingPlanner')),
 campaign_id uuid NOT NULL, currency text NOT NULL CHECK(currency IN ('USD','PHP','MYR','IDR','COP','PAB','DOP','THB')),
 retry_key varchar(120) NOT NULL, evidence varchar(300) NOT NULL, fingerprint varchar(64) NOT NULL,
 reverses_id uuid REFERENCES financials.journals(id), recorded_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(merchant_id,module,campaign_id,currency,retry_key)
);
CREATE UNIQUE INDEX one_full_reversal ON financials.journals(reverses_id) WHERE reverses_id IS NOT NULL;
CREATE TABLE financials.lines (
 journal_id uuid NOT NULL REFERENCES financials.journals(id), ordinal integer NOT NULL CHECK(ordinal>=0),
 account_id uuid NOT NULL,owner_id uuid NOT NULL,role varchar(64) NOT NULL,
 side text NOT NULL CHECK(side IN ('Debit','Credit')),minor_units bigint NOT NULL CHECK(minor_units>0),
 PRIMARY KEY(journal_id,ordinal)
);
CREATE INDEX lines_by_owner ON financials.lines(owner_id,journal_id);
CREATE FUNCTION financials.reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Financial evidence is append-only'; END $$;
CREATE TRIGGER journals_no_mutation BEFORE UPDATE OR DELETE ON financials.journals FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER lines_no_mutation BEFORE UPDATE OR DELETE ON financials.lines FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE FUNCTION financials.check_journal() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE j uuid; n integer; balance numeric;
BEGIN
 j := coalesce(to_jsonb(NEW)->>'journal_id',to_jsonb(NEW)->>'id')::uuid;
 SELECT count(*),sum(CASE WHEN side='Debit' THEN minor_units::numeric ELSE -minor_units::numeric END) INTO n,balance FROM financials.lines WHERE journal_id=j;
 IF n<2 OR balance<>0 THEN RAISE EXCEPTION 'Journal must have balanced lines'; END IF;
 IF EXISTS(SELECT 1 FROM financials.journals a JOIN financials.journals b ON b.id=a.reverses_id
 WHERE a.id=j AND (a.merchant_id<>b.merchant_id OR a.module<>b.module OR a.campaign_id<>b.campaign_id OR a.currency<>b.currency)) THEN RAISE EXCEPTION 'Reversal scope mismatch'; END IF;
 IF EXISTS(SELECT 1 FROM financials.journals a WHERE a.id=j AND a.reverses_id IS NOT NULL AND
 (EXISTS((SELECT ordinal,account_id,owner_id,role,CASE side WHEN 'Debit' THEN 'Credit' ELSE 'Debit' END,minor_units FROM financials.lines WHERE journal_id=a.reverses_id)
 EXCEPT (SELECT ordinal,account_id,owner_id,role,side,minor_units FROM financials.lines WHERE journal_id=j)) OR
 EXISTS((SELECT ordinal,account_id,owner_id,role,side,minor_units FROM financials.lines WHERE journal_id=j)
 EXCEPT (SELECT ordinal,account_id,owner_id,role,CASE side WHEN 'Debit' THEN 'Credit' ELSE 'Debit' END,minor_units FROM financials.lines WHERE journal_id=a.reverses_id)))) THEN RAISE EXCEPTION 'Reversal attribution mismatch'; END IF;
 RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER journal_balance AFTER INSERT ON financials.journals DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION financials.check_journal();
CREATE CONSTRAINT TRIGGER line_balance AFTER INSERT ON financials.lines DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION financials.check_journal();
CREATE FUNCTION financials.check_line_insert() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM financials.journals WHERE id=NEW.journal_id AND xmin::text::numeric=mod(pg_current_xact_id()::text::numeric,4294967296)) THEN RAISE EXCEPTION 'Cannot append to committed journal'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER lines_no_late_append BEFORE INSERT ON financials.lines FOR EACH ROW EXECUTE FUNCTION financials.check_line_insert();
CREATE TRIGGER journals_no_truncate BEFORE TRUNCATE ON financials.journals FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER lines_no_truncate BEFORE TRUNCATE ON financials.lines FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE TABLE financials.stripe_receipts (
 event_id varchar(120) PRIMARY KEY, payment_intent_id varchar(120) NOT NULL UNIQUE,
 journal_id uuid NOT NULL REFERENCES financials.journals(id),fingerprint varchar(64) NOT NULL,
 recorded_at timestamptz NOT NULL DEFAULT now()
);
CREATE TRIGGER receipts_no_mutation BEFORE UPDATE OR DELETE ON financials.stripe_receipts FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER receipts_no_truncate BEFORE TRUNCATE ON financials.stripe_receipts FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
