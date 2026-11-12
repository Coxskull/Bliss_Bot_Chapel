-- ALPHA Task #21 — production real-privacy-form to private staff queue.
-- Additive only: no change to public intake constraints, existing advertiser
-- or creator flow, existing mail cases, CRM, Ask Alpha, payments or STOP.
-- Source of truth is existing consented privacy form; never copy its personal
-- name, email, description, country, or marketing preference into staff queue.
DO $task21_pre$
BEGIN
 IF to_regclass('public.alpha_public_intake_requests') IS NULL
    OR to_regclass('alpha_support_private.manual_mail_cases') IS NULL
    OR to_regclass('alpha_support_private.manual_operator_grants') IS NULL
    OR to_regclass('alpha_support_private.manual_operator_claims') IS NULL
    OR to_regclass('alpha_support_private.manual_privacy_form_cases') IS NOT NULL
    OR (SELECT count(*) FROM public.alpha_public_intake_requests WHERE kind='privacy')<>0
    OR (SELECT count(*) FROM alpha_support_private.manual_operator_grants WHERE enabled=true)<>1
 THEN RAISE EXCEPTION 'TASK21_PRIVACY_FORM_PRIVATE_QUEUE_GATE_FAILED'; END IF;
END $task21_pre$;

CREATE TABLE alpha_support_private.manual_privacy_form_cases(
 case_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 source_request_id uuid NOT NULL UNIQUE
   REFERENCES public.alpha_public_intake_requests(id) ON DELETE CASCADE,
 category text NOT NULL DEFAULT 'privacy' CHECK(category='privacy'),
 status text NOT NULL DEFAULT 'OPEN' CHECK(status IN ('OPEN','IN_REVIEW','RESOLVED','CLOSED')),
 observed_at timestamptz NOT NULL,
 created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX manual_privacy_open_priority
 ON alpha_support_private.manual_privacy_form_cases(status,observed_at)
 WHERE status IN ('OPEN','IN_REVIEW');
ALTER TABLE alpha_support_private.manual_privacy_form_cases ENABLE ROW LEVEL SECURITY;

CREATE TABLE alpha_support_private.manual_privacy_form_events(
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 case_id uuid NOT NULL REFERENCES alpha_support_private.manual_privacy_form_cases(case_id) ON DELETE CASCADE,
 event_type text NOT NULL CHECK(event_type IN ('CREATED','STATUS_CHANGED')),
 case_status text NOT NULL CHECK(case_status IN ('OPEN','IN_REVIEW','RESOLVED','CLOSED')),
 happened_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.manual_privacy_form_events ENABLE ROW LEVEL SECURITY;
CREATE INDEX manual_privacy_events_case ON alpha_support_private.manual_privacy_form_events(case_id,event_id);

CREATE TABLE alpha_support_private.manual_privacy_operator_claims(
 case_id uuid PRIMARY KEY REFERENCES alpha_support_private.manual_privacy_form_cases(case_id) ON DELETE CASCADE,
 actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
 claimed_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.manual_privacy_operator_claims ENABLE ROW LEVEL SECURITY;

CREATE TABLE alpha_support_private.manual_privacy_operator_actions(
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 case_id uuid NOT NULL REFERENCES alpha_support_private.manual_privacy_form_cases(case_id) ON DELETE CASCADE,
 actor_user_id uuid NOT NULL REFERENCES auth.users(id) ON DELETE RESTRICT,
 action text NOT NULL CHECK(action='CLAIM'),
 occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE alpha_support_private.manual_privacy_operator_actions ENABLE ROW LEVEL SECURITY;
CREATE INDEX manual_privacy_operator_actions_case ON alpha_support_private.manual_privacy_operator_actions(case_id,event_id);

-- Only the trusted, existing public intake INSERT can create a privacy case.
-- Trigger executes as database owner, because service_role has zero direct
-- permissions in the private support schema.
CREATE FUNCTION alpha_support_private.enqueue_real_privacy_form()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog AS $privacy_enqueue$
BEGIN
 IF NEW.kind='privacy' AND NEW.processing_acknowledged IS TRUE
    AND NEW.adult_confirmed IS TRUE THEN
   INSERT INTO alpha_support_private.manual_privacy_form_cases(
    source_request_id,category,observed_at)
   VALUES(NEW.id,'privacy',NEW.created_at)
   ON CONFLICT(source_request_id) DO NOTHING;
 END IF;
 RETURN NEW;
END $privacy_enqueue$;
CREATE TRIGGER task21_real_privacy_form_private_queue
 AFTER INSERT ON public.alpha_public_intake_requests
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.enqueue_real_privacy_form();

CREATE FUNCTION alpha_support_private.audit_manual_privacy_form()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog AS $privacy_audit$
BEGIN
 IF TG_OP='INSERT' THEN
  INSERT INTO alpha_support_private.manual_privacy_form_events(case_id,event_type,case_status)
  VALUES(NEW.case_id,'CREATED',NEW.status);
  RETURN NEW;
 END IF;
 IF (NEW.case_id,NEW.source_request_id,NEW.category,NEW.observed_at,NEW.created_at)
   IS DISTINCT FROM
    (OLD.case_id,OLD.source_request_id,OLD.category,OLD.observed_at,OLD.created_at)
 THEN RAISE EXCEPTION 'Task21 privacy case identity is immutable'; END IF;
 IF OLD.status='CLOSED' OR
    (OLD.status='OPEN' AND NEW.status NOT IN ('OPEN','IN_REVIEW')) OR
    (OLD.status='IN_REVIEW' AND NEW.status NOT IN ('IN_REVIEW','RESOLVED')) OR
    (OLD.status='RESOLVED' AND NEW.status NOT IN ('RESOLVED','CLOSED'))
 THEN RAISE EXCEPTION 'Invalid Task21 privacy case transition'; END IF;
 IF NEW.status IS DISTINCT FROM OLD.status THEN
   NEW.updated_at=clock_timestamp();
   INSERT INTO alpha_support_private.manual_privacy_form_events(case_id,event_type,case_status)
   VALUES(NEW.case_id,'STATUS_CHANGED',NEW.status);
 END IF;
 RETURN NEW;
END $privacy_audit$;
CREATE TRIGGER task21_privacy_form_create_audit
 AFTER INSERT ON alpha_support_private.manual_privacy_form_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.audit_manual_privacy_form();
CREATE TRIGGER task21_privacy_form_status_audit
 BEFORE UPDATE ON alpha_support_private.manual_privacy_form_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.audit_manual_privacy_form();

-- Unauthorized user updates are denied by RLS and revoked privileges.
-- On DELETE CASCADE, the existing 90-day public intake retention job must
-- be able to remove the linked case and its events, so no DELETE blocker.
CREATE FUNCTION alpha_support_private.reject_manual_privacy_event_updates()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $privacy_events$
BEGIN RAISE EXCEPTION 'Privacy case audit events cannot be updated'; END $privacy_events$;
CREATE TRIGGER task21_privacy_event_no_update
 BEFORE UPDATE ON alpha_support_private.manual_privacy_form_events
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.reject_manual_privacy_event_updates();
CREATE TRIGGER task21_privacy_actor_event_no_update
 BEFORE UPDATE ON alpha_support_private.manual_privacy_operator_actions
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.reject_manual_privacy_event_updates();

-- Existing customer support mailbox queue unchanged; join two genuinely
-- sourced streams for the one owner-authorized human operator.
CREATE OR REPLACE FUNCTION public.alpha_support_manual_operator_queue(
 p_actor_user_id uuid,p_page integer DEFAULT 0
) RETURNS TABLE(case_id uuid,category text,status text,observed_at timestamptz,
 created_at timestamptz,claimed_by_me boolean)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path=pg_catalog AS $operator_queue$
BEGIN
 IF p_page IS NULL OR p_page < 0 OR p_page > 100
 THEN RAISE EXCEPTION 'Invalid queue page'; END IF;
 IF NOT public.alpha_support_manual_operator_authorized(p_actor_user_id)
 THEN RAISE EXCEPTION 'Operator not authorized' USING ERRCODE='42501'; END IF;
 RETURN QUERY
 WITH combined AS (
  SELECT m.case_id,m.category,m.status,m.observed_at,m.created_at,
    COALESCE(k.actor_user_id=p_actor_user_id,false) AS claimed_by_me
  FROM alpha_support_private.manual_mail_cases m
  LEFT JOIN alpha_support_private.manual_operator_claims k ON k.case_id=m.case_id
  WHERE m.status IN ('OPEN','IN_REVIEW')
  UNION ALL
  SELECT f.case_id,f.category,f.status,f.observed_at,f.created_at,
    COALESCE(k.actor_user_id=p_actor_user_id,false) AS claimed_by_me
  FROM alpha_support_private.manual_privacy_form_cases f
  LEFT JOIN alpha_support_private.manual_privacy_operator_claims k ON k.case_id=f.case_id
  WHERE f.status IN ('OPEN','IN_REVIEW')
 )
 SELECT c.case_id,c.category,c.status,c.observed_at,c.created_at,c.claimed_by_me
 FROM combined c
 ORDER BY CASE c.category
  WHEN 'security' THEN 0 WHEN 'privacy' THEN 1
  WHEN 'billing' THEN 2 WHEN 'refund' THEN 2
  WHEN 'failed_campaign' THEN 3 ELSE 4 END,
  c.observed_at,c.case_id
 LIMIT 20 OFFSET p_page*20;
END $operator_queue$;

CREATE OR REPLACE FUNCTION public.alpha_support_manual_operator_claim(
 p_actor_user_id uuid,p_case_id uuid
) RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog AS $operator_claim$
DECLARE v_changed uuid;
BEGIN
 IF p_case_id IS NULL OR NOT public.alpha_support_manual_operator_authorized(p_actor_user_id)
 THEN RETURN false; END IF;
 -- Preserve existing Gmail-origin cases exactly.
 UPDATE alpha_support_private.manual_mail_cases m
 SET status='IN_REVIEW'
 WHERE m.case_id=p_case_id AND m.status='OPEN'
  AND NOT EXISTS (
   SELECT 1 FROM alpha_support_private.manual_operator_claims k WHERE k.case_id=m.case_id)
 RETURNING m.case_id INTO v_changed;
 IF v_changed IS NOT NULL THEN
  INSERT INTO alpha_support_private.manual_operator_claims(case_id,actor_user_id)
  VALUES(v_changed,p_actor_user_id);
  INSERT INTO alpha_support_private.manual_operator_actions(case_id,actor_user_id,action)
  VALUES(v_changed,p_actor_user_id,'CLAIM');
  RETURN true;
 END IF;
 -- Also allow one and only one human claim of a genuine privacy-form case.
 UPDATE alpha_support_private.manual_privacy_form_cases f
 SET status='IN_REVIEW'
 WHERE f.case_id=p_case_id AND f.status='OPEN'
  AND NOT EXISTS(
   SELECT 1 FROM alpha_support_private.manual_privacy_operator_claims k WHERE k.case_id=f.case_id)
 RETURNING f.case_id INTO v_changed;
 IF v_changed IS NULL THEN RETURN false; END IF;
 INSERT INTO alpha_support_private.manual_privacy_operator_claims(case_id,actor_user_id)
 VALUES(v_changed,p_actor_user_id);
 INSERT INTO alpha_support_private.manual_privacy_operator_actions(case_id,actor_user_id,action)
 VALUES(v_changed,p_actor_user_id,'CLAIM');
 RETURN true;
END $operator_claim$;

REVOKE ALL ON SCHEMA alpha_support_private FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON alpha_support_private.manual_privacy_form_cases,
 alpha_support_private.manual_privacy_form_events,
 alpha_support_private.manual_privacy_operator_claims,
 alpha_support_private.manual_privacy_operator_actions
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON SEQUENCE alpha_support_private.manual_privacy_form_events_event_id_seq,
 alpha_support_private.manual_privacy_operator_actions_event_id_seq
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.enqueue_real_privacy_form()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.audit_manual_privacy_form()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE EXECUTE ON FUNCTION alpha_support_private.reject_manual_privacy_event_updates()
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION public.alpha_support_manual_operator_queue(uuid,integer)
 FROM PUBLIC,anon,authenticated,service_role;
REVOKE ALL ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid)
 FROM PUBLIC,anon,authenticated,service_role;
GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_queue(uuid,integer) TO service_role;
GRANT EXECUTE ON FUNCTION public.alpha_support_manual_operator_claim(uuid,uuid) TO service_role;

COMMENT ON TABLE alpha_support_private.manual_privacy_form_cases IS
 'Task21 only: private metadata link to the existing consented privacy-form record. No customer PII is copied. No marketing or customer email. Deletion cascades under existing 90-day public intake retention.';
COMMENT ON TABLE alpha_support_private.manual_privacy_form_events IS
 'Task21 private status-only audit, no PII. Existing 90-day intake deletion cascades to this record.';
