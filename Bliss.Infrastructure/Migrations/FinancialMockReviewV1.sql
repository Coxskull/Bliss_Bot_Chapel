-- Annotation only. No payment approval, cancellation release or financial mutation.
CREATE TABLE financials.mock_review_notes(
 id uuid PRIMARY KEY,operation_id uuid NOT NULL REFERENCES financials.payment_operations(id),
 actor_subject varchar(200) NOT NULL CHECK(length(btrim(actor_subject))>0),retry_key varchar(120) NOT NULL CHECK(length(btrim(retry_key))>0),
 evidence_fingerprint varchar(64) NOT NULL CHECK(evidence_fingerprint ~ '^[0-9A-F]{64}$'),
 evidence_json jsonb NOT NULL CHECK(jsonb_typeof(evidence_json)='object'),
 note_text varchar(2000) NOT NULL CHECK(length(btrim(note_text))>0),
 recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),UNIQUE(actor_subject,retry_key),
 CHECK(evidence_json->'operation'->>'id' IS NOT NULL AND (evidence_json->'operation'->>'id')::uuid=operation_id),
 CHECK((evidence_json->>'review_required')::boolean IS TRUE)
);
CREATE INDEX mock_review_notes_by_operation ON financials.mock_review_notes(operation_id,recorded_at,id);
CREATE TRIGGER mock_review_notes_no_mutation BEFORE UPDATE OR DELETE ON financials.mock_review_notes FOR EACH ROW EXECUTE FUNCTION financials.reject_mutation();
CREATE TRIGGER mock_review_notes_no_truncate BEFORE TRUNCATE ON financials.mock_review_notes FOR EACH STATEMENT EXECUTE FUNCTION financials.reject_mutation();
CREATE FUNCTION financials.mock_review_evidence(op uuid) RETURNS jsonb LANGUAGE sql STABLE AS $$
 WITH captured AS (SELECT * FROM financials.payment_captures WHERE operation_id=op),
 requests AS (SELECT r.* FROM financials.refund_requests r JOIN captured c ON c.payment_intent_id=r.payment_intent_id),
 results AS (SELECT r.* FROM financials.refund_results r JOIN requests q ON q.id=r.request_id),
 linked_journals AS (SELECT journal_id FROM captured UNION SELECT refund_journal_id FROM results WHERE refund_journal_id IS NOT NULL),
 flags AS (SELECT
  EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=op AND state='CANCELED') OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=op AND provider_status='canceled') AS canceled,
  EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts WHERE operation_id=op AND started_at<=statement_timestamp()-interval '20 hours') AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=op AND state='BOUND') AND NOT EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=op) AS aged)
 SELECT jsonb_build_object(
  'operation',to_jsonb(o),'quote',to_jsonb(q)||jsonb_build_object('amount_minor',q.amount_minor::text),
  'review_required',flags.canceled OR flags.aged,
  'reason',CASE WHEN flags.canceled THEN 'CANCELLATION_REVIEW' WHEN flags.aged THEN 'AGED_UNRESOLVED_REVIEW' ELSE 'NO_CURRENT_EXCEPTION' END,
  'retry_aliases',coalesce((SELECT jsonb_agg(to_jsonb(k) ORDER BY k.advertiser_subject,k.retry_key) FROM financials.payment_retry_keys k WHERE operation_id=op),'[]'::jsonb),
  'attempts',coalesce((SELECT jsonb_agg(to_jsonb(a) ORDER BY a.sequence) FROM financials.payment_dispatch_attempts a WHERE operation_id=op),'[]'::jsonb),
  'dispatch_results',coalesce((SELECT jsonb_agg(to_jsonb(r) ORDER BY r.attempt_id) FROM financials.payment_dispatch_results r WHERE operation_id=op),'[]'::jsonb),
  'recoveries',coalesce((SELECT jsonb_agg(to_jsonb(r) ORDER BY r.id) FROM financials.mock_payment_recoveries r WHERE operation_id=op),'[]'::jsonb),
  'status_events',coalesce((SELECT jsonb_agg(to_jsonb(e) ORDER BY e.event_id) FROM financials.mock_payment_status_events e WHERE operation_id=op),'[]'::jsonb),
  'provider_bindings',coalesce((SELECT jsonb_agg(to_jsonb(b) ORDER BY b.operation_id) FROM financials.mock_provider_bindings b WHERE operation_id=op),'[]'::jsonb),
  'provider_events',coalesce((SELECT jsonb_agg(to_jsonb(e) ORDER BY e.event_id) FROM financials.mock_provider_events e WHERE operation_id=op),'[]'::jsonb),
  'captures',coalesce((SELECT jsonb_agg(to_jsonb(c) ORDER BY c.operation_id) FROM captured c),'[]'::jsonb),
  'journals',coalesce((SELECT jsonb_agg(to_jsonb(j) ORDER BY j.id) FROM financials.journals j WHERE j.id IN(SELECT journal_id FROM linked_journals)),'[]'::jsonb),
  'lines',coalesce((SELECT jsonb_agg(to_jsonb(l)||jsonb_build_object('minor_units',l.minor_units::text) ORDER BY l.journal_id,l.ordinal) FROM financials.lines l WHERE journal_id IN(SELECT journal_id FROM linked_journals)),'[]'::jsonb),
  'receipts',coalesce((SELECT jsonb_agg(to_jsonb(r) ORDER BY r.event_id) FROM financials.stripe_receipts r WHERE journal_id IN(SELECT journal_id FROM linked_journals)),'[]'::jsonb),
  'refund_requests',coalesce((SELECT jsonb_agg(to_jsonb(r)||jsonb_build_object('amount_minor',r.amount_minor::text) ORDER BY r.id) FROM requests r),'[]'::jsonb),
  'refund_results',coalesce((SELECT jsonb_agg(to_jsonb(r) ORDER BY r.request_id,r.status) FROM results r),'[]'::jsonb),
  'refund_provider_ids',coalesce((SELECT jsonb_agg(to_jsonb(p) ORDER BY p.provider_refund_id) FROM financials.refund_provider_ids p WHERE request_id IN(SELECT id FROM requests)),'[]'::jsonb))
 FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id CROSS JOIN flags WHERE o.id=op
$$;
