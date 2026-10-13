-- Additive SQL-managed dispatch evidence; no provider calls or destructive rollback.
CREATE TABLE financials.payment_dispatch_attempts (
 id uuid PRIMARY KEY, operation_id uuid NOT NULL REFERENCES financials.payment_operations(id),
 sequence bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
 started_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 lease_until timestamptz NOT NULL DEFAULT (clock_timestamp()+interval '2 minutes'),
 UNIQUE(id,operation_id),CHECK(lease_until>started_at)
);
CREATE INDEX dispatch_attempts_by_operation ON financials.payment_dispatch_attempts(operation_id,sequence DESC);
CREATE TABLE financials.payment_dispatch_results (
 attempt_id uuid PRIMARY KEY, operation_id uuid NOT NULL,
 state text NOT NULL CHECK(state IN ('UNKNOWN','BOUND')),
 payment_intent_id varchar(120),provider_status varchar(64),recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(attempt_id,operation_id) REFERENCES financials.payment_dispatch_attempts(id,operation_id),
 CHECK((state='BOUND' AND payment_intent_id IS NOT NULL AND provider_status IS NOT NULL AND left(payment_intent_id,3)='pi_' AND provider_status IN
 ('requires_payment_method','requires_confirmation','requires_action','processing','requires_capture','canceled','succeeded'))
 OR (state='UNKNOWN' AND payment_intent_id IS NULL AND provider_status IS NULL))
);
CREATE UNIQUE INDEX dispatch_one_bound_operation ON financials.payment_dispatch_results(operation_id) WHERE state='BOUND';
CREATE UNIQUE INDEX dispatch_one_bound_intent ON financials.payment_dispatch_results(payment_intent_id) WHERE state='BOUND';
CREATE TRIGGER dispatch_attempts_no_mutation BEFORE UPDATE OR DELETE ON financials.payment_dispatch_attempts FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER dispatch_results_no_mutation BEFORE UPDATE OR DELETE ON financials.payment_dispatch_results FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER dispatch_attempts_no_truncate BEFORE TRUNCATE ON financials.payment_dispatch_attempts FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER dispatch_results_no_truncate BEFORE TRUNCATE ON financials.payment_dispatch_results FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
