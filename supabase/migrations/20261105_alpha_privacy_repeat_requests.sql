-- ALPHA GLOBAL MEDIA LLC — Public intake rights requests may be repeated.
-- PostgreSQL/Supabase Bliss Bot, 2026-11-05.
--
-- Before: UNIQUE(kind,email) incorrectly rejected a second, distinct privacy
-- rights request from the same email even when it concerned a new matter.
--
-- After: creator and advertiser expressions of interest remain UNIQUE(kind,email);
-- privacy requests can be submitted repeatedly, each under its OWN request_key,
-- each receiving exactly one atomic receipt, without creating a GHL outbox row.
--
-- No rows are removed or copied. The public intake handler's request_key retry
-- idempotency, notice/consent validation, rate limit, RLS, and CRM data boundary
-- are unchanged. The unique identity index is not a named table constraint.
--
-- Guard against unexpected schema drift before changing the production index.
DO $$
DECLARE current_def text;
BEGIN
 SELECT pg_get_indexdef(indexrelid)
 INTO current_def
 FROM pg_index
 WHERE indexrelid = to_regclass('public.alpha_public_intake_identity');
 IF current_def IS NULL OR
    current_def !~ '^CREATE UNIQUE INDEX alpha_public_intake_identity ON public[.]alpha_public_intake_requests USING btree [(]kind, email[)]$'
 THEN
   RAISE EXCEPTION 'Unexpected alpha_public_intake_identity definition: migration refused';
 END IF;
END;
$$;

DROP INDEX public.alpha_public_intake_identity;
CREATE UNIQUE INDEX alpha_public_intake_identity
  ON public.alpha_public_intake_requests USING btree (kind, email)
  WHERE kind IN ('creator', 'advertiser');
