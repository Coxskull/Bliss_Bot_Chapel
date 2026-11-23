-- ALPHA Task #22 — stricter future-consent gate, immutable opt-out receipts,
-- and aggregate-only staff readiness. No new services, customer contact, or
-- modification of historical delivered records.
DO $pre$
BEGIN
 IF to_regclass('public.alpha_public_intake_requests') IS NULL
 OR to_regclass('public.alpha_ghl_delivery') IS NULL
 OR to_regclass('alpha_support_private.manual_operator_grants') IS NULL
 OR to_regclass('alpha_support_private.crm_consent_revocations') IS NOT NULL
 OR EXISTS(SELECT 1 FROM public.alpha_public_intake_requests
   WHERE followup_gate='CLEARED'
     AND (marketing_opt_in IS DISTINCT FROM true
       OR kind NOT IN ('creator','advertiser')
       OR notice_version<>'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'))
 THEN RAISE EXCEPTION 'TASK22_OPT_OUT_GATE_PRECONDITION_FAILED'; END IF;
END $pre$;

-- Existing CHECK requires consent, authority, CRM review and timestamp to
-- CLEAR, but did not require marketing_opt_in=true or the current notice.
ALTER TABLE public.alpha_public_intake_requests
 ADD CONSTRAINT alpha_task22_cleared_requires_current_explicit_optin
 CHECK (followup_gate<>'CLEARED' OR (
   kind IN ('creator','advertiser')
   AND notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
   AND contact_consent IS TRUE
   AND marketing_opt_in IS TRUE
   AND contact_consent_version=notice_version
   AND contact_consent_at IS NOT NULL
 )) NOT VALID;
ALTER TABLE public.alpha_public_intake_requests
 VALIDATE CONSTRAINT alpha_task22_cleared_requires_current_explicit_optin;

CREATE TABLE alpha_support_private.crm_consent_revocations(
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 source_request_id uuid NOT NULL REFERENCES public.alpha_public_intake_requests(id)
  ON DELETE CASCADE,
 event_kind text NOT NULL CHECK(event_kind IN ('CONTACT_CONSENT_REVOKED','MARKETING_OPTED_OUT')),
 happened_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX crm_consent_revocations_request
 ON alpha_support_private.crm_consent_revocations(source_request_id,event_id);
ALTER TABLE alpha_support_private.crm_consent_revocations ENABLE ROW LEVEL SECURITY;

-- When a trusted workflow records a genuine withdrawal, forcibly revoke any
-- previously cleared local permission. This does NOT accept unauthenticated
-- user claims and does NOT call HighLevel or toggle provider DND.
CREATE FUNCTION alpha_support_private.force_hold_on_consent_withdrawal()
 RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $before$
BEGIN
 IF (OLD.contact_consent IS TRUE AND NEW.contact_consent IS FALSE)
   OR (OLD.marketing_opt_in IS TRUE AND NEW.marketing_opt_in IS FALSE)
 THEN
   NEW.followup_gate='HOLD';
   NEW.followup_approved_at=NULL;
   NEW.crm_suppression_checked=false;
 END IF;
 RETURN NEW;
END $before$;
CREATE TRIGGER task22_consent_revocation_force_hold
 BEFORE UPDATE OF contact_consent,marketing_opt_in
 ON public.alpha_public_intake_requests
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.force_hold_on_consent_withdrawal();

CREATE FUNCTION alpha_support_private.record_consent_withdrawal_metadata()
 RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $after$
BEGIN
 IF OLD.contact_consent IS TRUE AND NEW.contact_consent IS FALSE THEN
   INSERT INTO alpha_support_private.crm_consent_revocations(source_request_id,event_kind)
   VALUES(NEW.id,'CONTACT_CONSENT_REVOKED');
 END IF;
 IF OLD.marketing_opt_in IS TRUE AND NEW.marketing_opt_in IS FALSE THEN
   INSERT INTO alpha_support_private.crm_consent_revocations(source_request_id,event_kind)
   VALUES(NEW.id,'MARKETING_OPTED_OUT');
 END IF;
 RETURN NEW;
END $after$;
CREATE TRIGGER task22_consent_revocation_audit
 AFTER UPDATE OF contact_consent,marketing_opt_in
 ON public.alpha_public_intake_requests
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.record_consent_withdrawal_metadata();

CREATE FUNCTION alpha_support_private.crm_consent_revocations_immutable()
 RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $immutable$
BEGIN
 RAISE EXCEPTION 'Task22 consent revocation audit cannot be modified';
END $immutable$;
CREATE TRIGGER task22_consent_revocation_no_update
 BEFORE UPDATE ON alpha_support_private.crm_consent_revocations
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.crm_consent_revocations_immutable();
-- Ordinary callers cannot delete, even though the trusted existing 90-day
-- public intake retention can cascade source deletion, including audit rows.

-- The staff console receives aggregate counts ONLY, not request IDs, emails,
-- names, budgets, provider contact IDs or any source description.
CREATE FUNCTION public.alpha_ghl_consent_readiness_summary()
 RETURNS jsonb LANGUAGE sql STABLE SECURITY DEFINER
 SET search_path=pg_catalog AS $summary$
 SELECT jsonb_build_object(
   'inquiries',count(*),
   'contact_consent_recorded',count(*) FILTER (WHERE r.contact_consent IS TRUE
     AND r.contact_consent_version=r.notice_version AND r.contact_consent_at IS NOT NULL),
   'missing_current_contact_consent',count(*) FILTER (WHERE r.contact_consent IS NOT TRUE
     OR r.contact_consent_version IS DISTINCT FROM r.notice_version
     OR r.contact_consent_at IS NULL),
   'marketing_opted_in',count(*) FILTER (WHERE r.marketing_opt_in IS TRUE),
   'marketing_opted_out',count(*) FILTER (WHERE r.marketing_opt_in IS NOT TRUE),
   'pending_operator_review',count(*) FILTER (
     WHERE r.followup_gate='HOLD' AND r.contact_consent IS TRUE
       AND r.marketing_opt_in IS TRUE
       AND r.notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'),
   'locally_cleared',count(*) FILTER (WHERE r.followup_gate='CLEARED'),
   'hold_count',count(*) FILTER (WHERE r.followup_gate='HOLD'),
   'reviewed_identity_and_authority',count(*) FILTER (WHERE
     r.business_identity_verified IS TRUE AND r.decision_authority_verified IS TRUE),
   'marketing_followup_authorized',false,
   'revocation_events',(SELECT count(*)
      FROM alpha_support_private.crm_consent_revocations)
 )
 FROM public.alpha_public_intake_requests r
 WHERE r.kind IN ('creator','advertiser');
$summary$;

REVOKE ALL ON alpha_support_private.crm_consent_revocations
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON SEQUENCE alpha_support_private.crm_consent_revocations_event_id_seq
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.force_hold_on_consent_withdrawal()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.record_consent_withdrawal_metadata()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.crm_consent_revocations_immutable()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION public.alpha_ghl_consent_readiness_summary()
 FROM PUBLIC,anon,authenticated,service_role;
GRANT EXECUTE ON FUNCTION public.alpha_ghl_consent_readiness_summary()
 TO service_role;

COMMENT ON TABLE alpha_support_private.crm_consent_revocations IS
 'Task22 metadata-only genuine revocation receipts, no PII; existing 90-day intake retention cascades deletions. Never interpret as HighLevel DND write.';
COMMENT ON FUNCTION public.alpha_ghl_consent_readiness_summary() IS
 'Task22 protected service-role aggregate readiness report. The current marketing/no-outreach HOLD is never released by reporting.';
