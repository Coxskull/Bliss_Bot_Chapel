-- ALPHA Task #21 / Nov 12, 2026 / PRODUCTION additive locked operator backend.
-- Works only with existing private Gmail message metadata registry.
-- No public intake/CRM/advertiser/Ask Alpha/STOP changes.
-- No staff account or grants created; owner must authenticate to production
-- Supabase Auth with verified MFA and explicitly register ONE primary user.
DO $guard$
BEGIN
 IF to_regclass('alpha_support_private.manual_mail_cases') IS NULL
    OR to_regclass('alpha_support_private.manual_mail_case_events') IS NULL
    OR to_regclass('public.alpha_ask_alpha_settings') IS NULL
    OR to_regclass('alpha_support_private.manual_operator_grants') IS NOT NULL
    OR (SELECT count(*) FROM alpha_support_private.manual_mail_cases) <> 0
 THEN RAISE EXCEPTION 'TASK21_PRODUCTION_OPERATOR_PREREQUISITE_FAILED';
 END IF;
END $guard$;

CREATE TABLE alpha_support_private.manual_operator_grants(
 user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
 role text NOT NULL DEFAULT 'GENERAL_SUPPORT' CHECK(role='GENERAL_SUPPORT'),
 allowed_origin text NOT NULL DEFAULT 'https://alphacreativecontent.com'
   CHECK(allowed_origin='https://alphacreativecontent.com'),
 enabled boolean NOT NULL DEFAULT false,
 granted_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.manual_operator_grants ENABLE ROW LEVEL SECURITY;
CREATE TABLE alpha_support_private.manual_operator_claims(
 case_id uuid PRIMARY KEY REFERENCES alpha_support_private.manual_mail_cases(case_id) ON DELETE CASCADE,
 actor_user_id uuid NOT NULL,
 claimed_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.manual_operator_claims ENABLE ROW LEVEL SECURITY;

CREATE TABLE alpha_support_private.manual_operator_actions(
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 case_id uuid NOT NULL REFERENCES alpha_support_private.manual_mail_cases(case_id) ON DELETE CASCADE,
 actor_user_id uuid NOT NULL,
 action text NOT NULL CHECK(action IN ('CLAIM')),
 occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.manual_operator_actions ENABLE ROW LEVEL SECURITY;
CREATE FUNCTION alpha_support_private.reject_manual_operator_audit_changes()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $audit$
BEGIN RAISE EXCEPTION 'Operator actions are immutable'; END $audit$;
CREATE TRIGGER manual_operator_actions_immutable
 BEFORE UPDATE OR DELETE ON alpha_support_private.manual_operator_actions
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.reject_manual_operator_audit_changes();

-- These functions are callable by service_role only. The Edge Function
-- verifies live Auth JWT/session, MFA aal2, and the origin before RPC.
CREATE FUNCTION public.alpha_support_manual_operator_authorized(p_actor_user_id uuid)
RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog AS $authorized$
 SELECT p_actor_user_id IS NOT NULL AND EXISTS(
  SELECT 1 FROM alpha_support_private.manual_operator_grants g
   JOIN auth.users u ON u.id=g.user_id
  WHERE g.user_id=p_actor_user_id AND g.role='GENERAL_SUPPORT'
    AND g.allowed_origin='https://alphacreativecontent.com' AND g.enabled=true
 );
$authorized$;

CREATE FUNCTION public.alpha_support_manual_operator_queue(
 p_actor_user_id uuid,p_page integer DEFAULT 0
) RETURNS TABLE(case_id uuid,category text,status text,observed_at timestamptz,created_at timestamptz,claimed_by_me boolean)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path=pg_catalog AS $queue$
BEGIN
 IF p_page IS NULL OR p_page < 0 OR p_page > 100
 THEN RAISE EXCEPTION 'Invalid queue page'; END IF;
 IF NOT public.alpha_support_manual_operator_authorized(p_actor_user_id)
 THEN RAISE EXCEPTION 'Operator not authorized' USING ERRCODE='42501'; END IF;
 RETURN QUERY
 SELECT c.case_id,c.category,c.status,c.observed_at,c.created_at,
   coalesce(k.actor_user_id=p_actor_user_id,false) AS claimed_by_me
 FROM alpha_support_private.manual_mail_cases c
 LEFT JOIN alpha_support_private.manual_operator_claims k ON k.case_id=c.case_id
 WHERE c.status IN ('OPEN','IN_REVIEW')
 ORDER BY CASE c.category
   WHEN 'security' THEN 0 WHEN 'privacy' THEN 1
   WHEN 'billing' THEN 2 WHEN 'refund' THEN 2
   WHEN 'failed_campaign' THEN 3 ELSE 4 END,
   c.observed_at,c.case_id
 LIMIT 20 OFFSET p_page * 20;
END $queue$;

CREATE FUNCTION public.alpha_support_manual_operator_claim(
 p_actor_user_id uuid,p_case_id uuid
) RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $claim$
DECLARE v_changed uuid;
BEGIN
 IF p_case_id IS NULL OR
    NOT public.alpha_support_manual_operator_authorized(p_actor_user_id)
 THEN RETURN false; END IF;
 -- Atomic owner claim + status change; no refund, closure or customer mail.
 UPDATE alpha_support_private.manual_mail_cases c
 SET status='IN_REVIEW'
 WHERE c.case_id=p_case_id AND c.status='OPEN'
   AND NOT EXISTS(
     SELECT 1 FROM alpha_support_private.manual_operator_claims k
      WHERE k.case_id=c.case_id)
 RETURNING c.case_id INTO v_changed;
 IF v_changed IS NULL THEN RETURN false; END IF;
 INSERT INTO alpha_support_private.manual_operator_claims(case_id,actor_user_id)
 VALUES(v_changed,p_actor_user_id);
 INSERT INTO alpha_support_private.manual_operator_actions(case_id,actor_user_id,action)
 VALUES(v_changed,p_actor_user_id,'CLAIM');
 RETURN true;
END $claim$;

REVOKE ALL ON SCHEMA alpha_support_private FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON alpha_support_private.manual_operator_grants,
 alpha_support_private.manual_operator_claims,alpha_support_private.manual_operator_actions
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON SEQUENCE alpha_support_private.manual_operator_actions_event_id_seq
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION alpha_support_private.reject_manual_operator_audit_changes()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION public.alpha_support_manual_operator_authorized(uuid)
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION public.alpha_support_manual_operator_queue(uuid,integer)
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid)
 FROM PUBLIC,anon,authenticated,service_role;
GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_authorized(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_queue(uuid,integer) TO service_role;
GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid) TO service_role;

COMMENT ON TABLE alpha_support_private.manual_operator_grants IS
 'PRODUCTION DEFAULT OFF. Owner-approved single operator requires a real verified Supabase Auth user, aal2 MFA, and separate owner review before grant is enabled. No fallback staff actor.';
COMMENT ON TABLE alpha_support_private.manual_operator_actions IS
 'Append-only staff claim receipts without customer personal information. No refunds, arbitrary status changes or email content.';
