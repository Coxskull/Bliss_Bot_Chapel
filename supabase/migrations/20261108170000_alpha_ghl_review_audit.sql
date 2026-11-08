-- Append-only, PII-free audit trail for read-only HighLevel reconciliation.
CREATE TABLE IF NOT EXISTS public.alpha_ghl_review_audit (
  run_id uuid NOT NULL,
  request_id uuid NOT NULL REFERENCES public.alpha_public_intake_requests(id) ON DELETE RESTRICT,
  checked_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  source_delivery_state text NOT NULL CHECK (source_delivery_state = 'DELIVERED'),
  crm_match_count integer NOT NULL CHECK (crm_match_count >= 0),
  delivered_contact_match boolean NOT NULL,
  email_identity_match boolean NOT NULL,
  suppression_status text NOT NULL CHECK (suppression_status IN ('SUPPRESSED','CLEAR','UNKNOWN')),
  consent_valid boolean NOT NULL,
  business_identity_verified boolean NOT NULL,
  decision_authority_verified boolean NOT NULL,
  followup_gate text NOT NULL CHECK (followup_gate IN ('HOLD','CLEARED','UNKNOWN')),
  classification text NOT NULL,
  error_class text,
  PRIMARY KEY (run_id, request_id)
);

ALTER TABLE public.alpha_ghl_review_audit ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.alpha_ghl_review_audit FORCE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.alpha_ghl_review_audit FROM PUBLIC, anon, authenticated, service_role;
GRANT SELECT, INSERT ON TABLE public.alpha_ghl_review_audit TO service_role;
CREATE INDEX IF NOT EXISTS alpha_ghl_review_audit_checked_at_idx
  ON public.alpha_ghl_review_audit(checked_at DESC);
COMMENT ON TABLE public.alpha_ghl_review_audit IS
  'Append-only, PII-free CRM readback evidence. Only service_role can read or append; no policy exposes rows to clients.';
