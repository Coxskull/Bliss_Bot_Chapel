-- Task #22 / Task #21 linked: deny automatic CRM upsert for marketing opt-outs.
-- Existing creator/advertiser inquiry stays safely in Supabase for manual review.
-- Exact existing production functions preserved with ONE additional gate each.
-- No change to delivered contact history, API credentials, source consent or CRM.
DO $crm$
BEGIN
 IF (SELECT count(*) FROM public.alpha_public_intake_requests
     WHERE kind IN ('advertiser','creator') AND marketing_opt_in IS NOT TRUE
     AND id IN (SELECT request_id FROM public.alpha_ghl_delivery))<>0
 OR (SELECT count(*) FROM public.alpha_ghl_delivery WHERE state='DELIVERED')<>5
 THEN RAISE EXCEPTION 'TASK22_CRM_OPT_OUT_SOURCE_SCOPE_CHANGED'; END IF;
END $crm$;
CREATE OR REPLACE FUNCTION public.alpha_ghl_delivery_on_intake()
 RETURNS trigger
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog'
AS $function$
BEGIN
  IF NEW.kind IN ('creator','advertiser')
     AND NEW.notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
     AND NEW.contact_consent = true
     AND NEW.marketing_opt_in IS TRUE
     AND NEW.contact_consent_version = NEW.notice_version
  THEN
    INSERT INTO public.alpha_ghl_delivery(request_id)
    VALUES(NEW.id)
    ON CONFLICT (request_id) DO NOTHING;
  END IF;
  RETURN NEW;
END;
$function$

CREATE OR REPLACE FUNCTION public.alpha_ghl_claim(p_request_id uuid)
 RETURNS TABLE(request_id uuid, claim_token uuid)
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog'
AS $function$
BEGIN
 RETURN QUERY
 UPDATE public.alpha_ghl_delivery d
 SET state='SENDING',
     attempts=d.attempts+1,
     claim_token=gen_random_uuid(),
     last_attempt_at=clock_timestamp(),
     next_attempt_at=clock_timestamp()+interval '5 minutes'
 WHERE d.request_id=p_request_id
   AND d.attempts<5
   AND EXISTS (
     SELECT 1
     FROM public.alpha_public_intake_requests r
     WHERE r.id=d.request_id
       AND r.kind IN ('creator','advertiser')
       AND r.notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
       AND r.contact_consent=true
       AND r.marketing_opt_in IS TRUE
       AND r.contact_consent_version=r.notice_version
       AND r.contact_consent_at IS NOT NULL
       AND r.followup_gate='CLEARED'
       AND r.business_identity_verified=true
       AND r.decision_authority_verified=true
       AND r.crm_suppression_checked=true
       AND r.followup_approved_at IS NOT NULL
   )
   AND (
     d.state='PENDING'
     OR (d.state IN ('RETRY','SENDING') AND d.next_attempt_at<=clock_timestamp())
   )
 RETURNING d.request_id,d.claim_token;
END;
$function$

-- Keep existing private server-only RPC and intake triggers non-callable by the browser.
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_claim(uuid) FROM PUBLIC,anon,authenticated;
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_delivery_on_intake() FROM PUBLIC,anon,authenticated;
COMMENT ON FUNCTION public.alpha_ghl_claim(uuid) IS
 'Task22: fail closed for no current contact consent, no marketing opt-in, missing identity/authority/suppression reviews or unknown follow-up gate. Does not prove remote DND/opt-out at HighLevel.';
