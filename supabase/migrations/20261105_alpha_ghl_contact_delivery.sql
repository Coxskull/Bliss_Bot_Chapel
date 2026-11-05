-- ALPHA / GoHighLevel limited CRM bridge. No real data sent until an operator
-- stores a scoped Private Integration Token, location ID, and explicitly enables sync.
-- Existing old consent records are never forwarded without the new notice.
ALTER TABLE public.alpha_public_intake_requests
  DROP CONSTRAINT alpha_public_intake_requests_notice_version_check;
ALTER TABLE public.alpha_public_intake_requests
  ADD CONSTRAINT alpha_public_intake_requests_notice_version_check
  CHECK (notice_version IN (
    'AGM-INTEREST-2026-11-05',
    'AGM-INTEREST-2026-11-05-GHL'
  ));
ALTER TABLE public.alpha_public_intake_receipts
  DROP CONSTRAINT alpha_public_intake_receipts_notice_version_check;
ALTER TABLE public.alpha_public_intake_receipts
  ADD CONSTRAINT alpha_public_intake_receipts_notice_version_check
  CHECK (notice_version IN (
    'AGM-INTEREST-2026-11-05',
    'AGM-INTEREST-2026-11-05-GHL'
  ));

CREATE TABLE public.alpha_ghl_delivery (
  request_id uuid PRIMARY KEY
    REFERENCES public.alpha_public_intake_requests(id) ON DELETE CASCADE,
  state text NOT NULL DEFAULT 'PENDING'
    CHECK (state IN ('PENDING','SENDING','RETRY','DELIVERED','BLOCKED')),
  attempts integer NOT NULL DEFAULT 0 CHECK (attempts BETWEEN 0 AND 5),
  claim_token uuid,
  next_attempt_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  last_attempt_at timestamptz,
  last_http_status integer CHECK (last_http_status IS NULL OR last_http_status BETWEEN 100 AND 599),
  last_error_class text NOT NULL DEFAULT ''
    CHECK (last_error_class IN (
      '', 'invalid_record', 'auth_failure', 'rate_limited',
      'provider_failure', 'network_failure', 'invalid_response'
    )),
  remote_contact_id text CHECK (remote_contact_id IS NULL OR length(remote_contact_id) <= 128),
  delivered_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CONSTRAINT alpha_ghl_delivery_status_consistent
    CHECK (state <> 'DELIVERED' OR (remote_contact_id IS NOT NULL AND delivered_at IS NOT NULL))
);
CREATE INDEX alpha_ghl_delivery_pending ON public.alpha_ghl_delivery(next_attempt_at)
  WHERE state IN ('PENDING','SENDING','RETRY');
ALTER TABLE public.alpha_ghl_delivery ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_ghl_delivery FROM PUBLIC, anon, authenticated;
GRANT ALL ON public.alpha_ghl_delivery TO service_role;

CREATE FUNCTION public.alpha_ghl_delivery_on_intake()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
SET search_path = pg_catalog AS $$
BEGIN
  IF NEW.kind IN ('creator','advertiser')
     AND NEW.notice_version='AGM-INTEREST-2026-11-05-GHL'
  THEN
    INSERT INTO public.alpha_ghl_delivery(request_id) VALUES(NEW.id);
  END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER alpha_ghl_delivery_after_intake
  AFTER INSERT ON public.alpha_public_intake_requests
  FOR EACH ROW EXECUTE FUNCTION public.alpha_ghl_delivery_on_intake();

-- Service-only claim; one worker owns a request for five minutes. Stale claims
-- can be reclaimed and retried. Never include a customer's raw data in logs.
CREATE FUNCTION public.alpha_ghl_claim(p_request_id uuid)
RETURNS TABLE(request_id uuid, claim_token uuid)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
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
   AND (
     d.state='PENDING'
     OR (d.state IN ('RETRY','SENDING') AND d.next_attempt_at<=clock_timestamp())
   )
 RETURNING d.request_id,d.claim_token;
END;
$$;
CREATE FUNCTION public.alpha_ghl_finish(
  p_request_id uuid,
  p_claim_token uuid,
  p_new_state text,
  p_http_status integer,
  p_error_class text,
  p_contact_id text
) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE v_rows int;
BEGIN
  IF p_new_state NOT IN ('DELIVERED','RETRY','BLOCKED')
     OR (p_new_state='DELIVERED' AND (p_contact_id IS NULL OR length(p_contact_id)>128))
     OR p_error_class NOT IN (
       '', 'invalid_record','auth_failure','rate_limited',
       'provider_failure','network_failure','invalid_response'
     ) THEN
    RAISE EXCEPTION 'Invalid GoHighLevel delivery completion';
  END IF;
  UPDATE public.alpha_ghl_delivery d
  SET state=CASE WHEN p_new_state='RETRY' AND d.attempts>=5 THEN 'BLOCKED'
                 ELSE p_new_state END,
      last_http_status=p_http_status,
      last_error_class=p_error_class,
      remote_contact_id=CASE WHEN p_new_state='DELIVERED' THEN p_contact_id ELSE NULL END,
      delivered_at=CASE WHEN p_new_state='DELIVERED' THEN clock_timestamp() ELSE NULL END,
      next_attempt_at=clock_timestamp()+interval '10 minutes' * power(2,least(d.attempts-1,4)),
      claim_token=NULL
  WHERE d.request_id=p_request_id AND d.claim_token=p_claim_token AND d.state='SENDING';
  GET DIAGNOSTICS v_rows=ROW_COUNT;
  RETURN v_rows=1;
END;
$$;
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_delivery_on_intake() FROM PUBLIC,anon,authenticated;
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_claim(uuid) FROM PUBLIC,anon,authenticated;
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_finish(uuid,uuid,text,integer,text,text) FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_ghl_claim(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.alpha_ghl_finish(uuid,uuid,text,integer,text,text) TO service_role;
