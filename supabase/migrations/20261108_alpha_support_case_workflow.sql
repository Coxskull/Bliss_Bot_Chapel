-- Task #21: additive support case / human escalation on existing ALPHA public intake.
-- SOURCE-ONLY until an approved isolated migration and hosted acceptance. Never
-- apply to the running Bliss Bot project solely because this file exists.
ALTER TABLE public.alpha_public_intake_requests DROP CONSTRAINT alpha_public_intake_requests_kind_check;
ALTER TABLE public.alpha_public_intake_requests ADD CONSTRAINT alpha_public_intake_requests_kind_check
  CHECK (kind IN ('creator','advertiser','privacy','support'));
ALTER TABLE public.alpha_public_intake_requests DROP CONSTRAINT alpha_public_intake_kind_data;
ALTER TABLE public.alpha_public_intake_requests ADD CONSTRAINT alpha_public_intake_kind_data CHECK (
 (kind='creator' AND notice_version<>'AGM-SUPPORT-2026-11-08' AND organization='' AND website<>'' AND market<>'' AND category<>'')
 OR (kind='advertiser' AND notice_version<>'AGM-SUPPORT-2026-11-08' AND organization<>'' AND market<>'')
 OR (kind='privacy' AND notice_version<>'AGM-SUPPORT-2026-11-08' AND description<>'')
 OR (kind='support' AND notice_version='AGM-SUPPORT-2026-11-08' AND category IN
     ('general','advertiser','creator','billing','refund','failed_campaign','privacy','security_incident')
     AND description<>'' AND organization='' AND website='' AND market='')
);
ALTER TABLE public.alpha_public_intake_requests DROP CONSTRAINT alpha_public_intake_requests_notice_version_check;
ALTER TABLE public.alpha_public_intake_requests ADD CONSTRAINT alpha_public_intake_requests_notice_version_check CHECK (
 notice_version IN ('AGM-INTEREST-2026-11-05','AGM-INTEREST-2026-11-05-GHL','AGM-SUPPORT-2026-11-08'));
ALTER TABLE public.alpha_public_intake_receipts DROP CONSTRAINT alpha_public_intake_receipts_kind_check;
ALTER TABLE public.alpha_public_intake_receipts ADD CONSTRAINT alpha_public_intake_receipts_kind_check
 CHECK (kind IN ('creator','advertiser','privacy','support'));
ALTER TABLE public.alpha_public_intake_receipts DROP CONSTRAINT alpha_public_intake_receipts_notice_version_check;
ALTER TABLE public.alpha_public_intake_receipts ADD CONSTRAINT alpha_public_intake_receipts_notice_version_check CHECK (
 notice_version IN ('AGM-INTEREST-2026-11-05','AGM-INTEREST-2026-11-05-GHL','AGM-SUPPORT-2026-11-08'));

CREATE TABLE public.alpha_support_cases (
  case_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  request_id uuid NOT NULL UNIQUE REFERENCES public.alpha_public_intake_requests(id) ON DELETE CASCADE,
  category text NOT NULL CHECK (category IN
    ('general','advertiser','creator','billing','refund','failed_campaign','privacy','security_incident')),
  severity text NOT NULL CHECK (severity IN ('NORMAL','HIGH','CRITICAL')),
  assigned_role text NOT NULL CHECK (assigned_role IN
    ('GENERAL_SUPPORT','ADVERTISER_SUPPORT','CREATOR_SUPPORT','BILLING_REVIEW','PRIVACY_REVIEW','SECURITY_REVIEW')),
  owner_user_id uuid, -- Verified by service-only transition; retain operator ID if Auth account is later removed.
  status text NOT NULL DEFAULT 'OPEN' CHECK
    (status IN ('OPEN','TRIAGED','IN_REVIEW','AWAITING_CUSTOMER','RESOLVED','CLOSED')),
  resolution_summary text NOT NULL DEFAULT '' CHECK (char_length(resolution_summary)<=1600),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  resolved_at timestamptz,
  CONSTRAINT alpha_support_resolved_consistent CHECK (
    status NOT IN ('RESOLVED','CLOSED') OR
    (owner_user_id IS NOT NULL AND char_length(btrim(resolution_summary))>0 AND resolved_at IS NOT NULL))
);
CREATE INDEX alpha_support_queue ON public.alpha_support_cases(status,severity,created_at)
 WHERE status NOT IN ('RESOLVED','CLOSED');
ALTER TABLE public.alpha_support_cases ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_support_cases FROM PUBLIC,anon,authenticated;
GRANT SELECT,INSERT,UPDATE ON public.alpha_support_cases TO service_role;

CREATE TABLE public.alpha_support_case_events (
  event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  case_id uuid NOT NULL REFERENCES public.alpha_support_cases(case_id) ON DELETE CASCADE,
  action text NOT NULL CHECK (action IN ('CREATED','STATUS_CHANGED','ASSIGNED','RESOLUTION_RECORDED')),
  status text NOT NULL,
  owner_user_id uuid,
  actor_user_id uuid,
  summary text NOT NULL DEFAULT '' CHECK (char_length(summary)<=1600),
  occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX alpha_support_events_case_time
  ON public.alpha_support_case_events(case_id,event_id);
ALTER TABLE public.alpha_support_case_events ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_support_case_events FROM PUBLIC,anon,authenticated;
GRANT SELECT,INSERT ON public.alpha_support_case_events TO service_role;

CREATE FUNCTION public.alpha_support_immutable_event() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog AS $$
BEGIN RAISE EXCEPTION 'Support case events are immutable'; END;
$$;
CREATE TRIGGER alpha_support_events_immutable
  BEFORE UPDATE ON public.alpha_support_case_events
  FOR EACH ROW EXECUTE FUNCTION public.alpha_support_immutable_event();
REVOKE EXECUTE ON FUNCTION public.alpha_support_immutable_event() FROM PUBLIC,anon,authenticated;

CREATE FUNCTION public.alpha_support_on_intake() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
DECLARE v_case uuid; v_severity text; v_role text;
BEGIN
 IF NEW.kind <> 'support' THEN RETURN NEW; END IF;
 v_severity := CASE WHEN NEW.category='security_incident' THEN 'CRITICAL'
   WHEN NEW.category IN ('billing','refund','failed_campaign','privacy') THEN 'HIGH'
   ELSE 'NORMAL' END;
 v_role := CASE
   WHEN NEW.category='security_incident' THEN 'SECURITY_REVIEW'
   WHEN NEW.category='privacy' THEN 'PRIVACY_REVIEW'
   WHEN NEW.category IN ('billing','refund','failed_campaign') THEN 'BILLING_REVIEW'
   WHEN NEW.category='advertiser' THEN 'ADVERTISER_SUPPORT'
   WHEN NEW.category='creator' THEN 'CREATOR_SUPPORT'
   ELSE 'GENERAL_SUPPORT' END;
 INSERT INTO public.alpha_support_cases(request_id,category,severity,assigned_role)
 VALUES (NEW.id,NEW.category,v_severity,v_role)
 RETURNING case_id INTO v_case;
 INSERT INTO public.alpha_support_case_events(case_id,action,status,summary)
 VALUES (v_case,'CREATED','OPEN','Recorded; human owner not yet assigned');
 RETURN NEW;
END;
$$;
CREATE TRIGGER alpha_support_case_after_intake AFTER INSERT
  ON public.alpha_public_intake_requests FOR EACH ROW
  EXECUTE FUNCTION public.alpha_support_on_intake();

-- Supabase service_role bypasses RLS but has NO SELECT on auth.users in
-- a real hosted project. A tiny, non-exposed SECURITY DEFINER helper verifies
-- existence without widening direct auth.users grants to the service role.
-- This function returns ONLY a boolean; anon/authenticated/PUBLIC have no
-- schema usage or function EXECUTE. It must be owned by the trusted migration
-- owner (postgres), not by an untrusted or browser-accessible role.
CREATE SCHEMA alpha_support_private;
REVOKE ALL ON SCHEMA alpha_support_private FROM PUBLIC,anon,authenticated;
GRANT USAGE ON SCHEMA alpha_support_private TO service_role;

CREATE FUNCTION alpha_support_private.auth_user_exists(p_user_id uuid)
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER
SET search_path=pg_catalog AS $task21_identity_lookup$
  SELECT p_user_id IS NOT NULL
    AND EXISTS (SELECT 1 FROM auth.users WHERE id=p_user_id);
$task21_identity_lookup$;
REVOKE EXECUTE ON FUNCTION alpha_support_private.auth_user_exists(uuid)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION alpha_support_private.auth_user_exists(uuid)
 TO service_role;

-- Service-role-only, transactional ownership and resolution audit.
-- Caller must already have verified operator identity server-side.
CREATE FUNCTION public.alpha_support_transition(
 p_case_id uuid, p_status text, p_owner_user_id uuid,
 p_resolution text, p_actor_user_id uuid
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $$
DECLARE c public.alpha_support_cases%ROWTYPE; v_resolution text; v_action text;
BEGIN
 IF NOT alpha_support_private.auth_user_exists(p_actor_user_id)
 THEN RAISE EXCEPTION 'Verified operator identity required'; END IF;
 SELECT * INTO c FROM public.alpha_support_cases WHERE case_id=p_case_id FOR UPDATE;
 IF NOT FOUND THEN RETURN false; END IF;
 IF p_owner_user_id IS NOT NULL AND
   NOT alpha_support_private.auth_user_exists(p_owner_user_id)
 THEN RAISE EXCEPTION 'Assigned owner must exist'; END IF;
 -- Prevent silent takeover: only the current owner can change an owned case,
 -- and an unassigned case can only be claimed by the acting human.
 IF c.owner_user_id IS NOT NULL AND
    (c.owner_user_id IS DISTINCT FROM p_actor_user_id OR
     p_owner_user_id IS DISTINCT FROM c.owner_user_id)
 THEN RAISE EXCEPTION 'Only the assigned human may update this case'; END IF;
 IF c.owner_user_id IS NULL AND p_owner_user_id IS DISTINCT FROM p_actor_user_id
 THEN RAISE EXCEPTION 'Unassigned cases must be claimed by the acting operator'; END IF;
 IF p_status NOT IN ('OPEN','TRIAGED','IN_REVIEW','AWAITING_CUSTOMER','RESOLVED','CLOSED')
 OR NOT (p_status = c.status
   OR (c.status='OPEN' AND p_status IN ('TRIAGED','IN_REVIEW'))
   OR (c.status='TRIAGED' AND p_status='IN_REVIEW')
   OR (c.status='IN_REVIEW' AND p_status IN ('AWAITING_CUSTOMER','RESOLVED'))
   OR (c.status='AWAITING_CUSTOMER' AND p_status IN ('IN_REVIEW','RESOLVED'))
   OR (c.status='RESOLVED' AND p_status IN ('CLOSED','IN_REVIEW'))
   OR (c.status='CLOSED' AND p_status='IN_REVIEW'))
 THEN RAISE EXCEPTION 'Invalid support status transition'; END IF;
 v_resolution := coalesce(p_resolution,'');
 IF char_length(v_resolution)>1600
 THEN RAISE EXCEPTION 'Resolution too long'; END IF;
 IF p_status IN ('RESOLVED','CLOSED') AND
     (p_owner_user_id IS NULL OR btrim(v_resolution)='')
 THEN RAISE EXCEPTION 'Human owner and resolution required'; END IF;
 IF p_status=c.status AND p_owner_user_id IS NOT DISTINCT FROM c.owner_user_id
   AND v_resolution=c.resolution_summary THEN RETURN true; END IF;
 v_action := CASE WHEN p_status IN ('RESOLVED','CLOSED') AND p_status<>c.status
                  THEN 'RESOLUTION_RECORDED'
                  WHEN p_owner_user_id IS DISTINCT FROM c.owner_user_id THEN 'ASSIGNED'
                  ELSE 'STATUS_CHANGED' END;
 UPDATE public.alpha_support_cases
 SET status=p_status,owner_user_id=p_owner_user_id,resolution_summary=v_resolution,
     resolved_at=CASE WHEN p_status IN ('RESOLVED','CLOSED') THEN coalesce(c.resolved_at,clock_timestamp()) ELSE NULL END,
     updated_at=clock_timestamp()
 WHERE case_id=p_case_id;
 INSERT INTO public.alpha_support_case_events
 (case_id,action,status,owner_user_id,actor_user_id,summary)
 VALUES (p_case_id,v_action,p_status,p_owner_user_id,p_actor_user_id,v_resolution);
 RETURN true;
END;
$$;
REVOKE EXECUTE ON FUNCTION public.alpha_support_transition(uuid,text,uuid,text,uuid) FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_transition(uuid,text,uuid,text,uuid) TO service_role;
-- Explicit self-claim is serialized by a case-level row lock, returns false
-- when occupied, and is idempotent for a repeat by the same owner.
-- The private Edge handler must first verify JWT, MFA level and exact roster.
CREATE FUNCTION public.alpha_support_claim(
 p_case_id uuid, p_actor_user_id uuid
) RETURNS boolean LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $support_claim_body$
DECLARE c public.alpha_support_cases%ROWTYPE;
BEGIN
 IF NOT alpha_support_private.auth_user_exists(p_actor_user_id) THEN RAISE EXCEPTION 'Verified human operator required'; END IF;
 SELECT * INTO c FROM public.alpha_support_cases WHERE case_id=p_case_id FOR UPDATE;
 IF NOT FOUND THEN RETURN false; END IF;
 IF c.owner_user_id=p_actor_user_id AND c.status='TRIAGED'
 THEN RETURN true; END IF;
 IF c.owner_user_id IS NOT NULL OR c.status<>'OPEN'
 THEN RETURN false; END IF;
 UPDATE public.alpha_support_cases
 SET owner_user_id=p_actor_user_id,status='TRIAGED',updated_at=clock_timestamp()
 WHERE case_id=p_case_id;
 INSERT INTO public.alpha_support_case_events
 (case_id,action,status,owner_user_id,actor_user_id,summary)
 VALUES (p_case_id,'ASSIGNED','TRIAGED',p_actor_user_id,p_actor_user_id,
         'Human operator acknowledged case; no external notification');
 RETURN true;
END;
$support_claim_body$;
REVOKE EXECUTE ON FUNCTION public.alpha_support_claim(uuid,uuid)
 FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_claim(uuid,uuid) TO service_role;

REVOKE EXECUTE ON FUNCTION public.alpha_support_on_intake() FROM PUBLIC,anon,authenticated;

-- Reuse the existing durable HighLevel outbox for NON-SENSITIVE support only.
-- Actual sending separately requires ALPHA_GHL_SUPPORT_SYNC_ENABLED=true,
-- validated service-side consent and the existing GHL global enable switch.
CREATE OR REPLACE FUNCTION public.alpha_ghl_delivery_on_intake()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 IF (NEW.kind IN ('creator','advertiser') AND NEW.notice_version='AGM-INTEREST-2026-11-05-GHL')
 OR (NEW.kind='support' AND NEW.notice_version='AGM-SUPPORT-2026-11-08'
     AND NEW.category IN ('general','advertiser','creator'))
 THEN INSERT INTO public.alpha_ghl_delivery(request_id) VALUES(NEW.id);
 END IF;
 RETURN NEW;
END;
$$;
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_delivery_on_intake() FROM PUBLIC,anon,authenticated;
