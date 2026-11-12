-- ALPHA Task #21 — restricted PRODUCTION MANUAL SUPPORT MAILBOX registry.
-- This is not the unmerged PR353 public support form migration and does NOT
-- alter existing intake, CRM, OpenAI, Auth, public API, payment, or STOP routes.
-- Retains only an opaque Gmail message ID and coarse support category.
DO $task21_production_gate$
BEGIN
 IF to_regclass('public.alpha_public_intake_requests') IS NULL
   OR to_regclass('public.alpha_ask_alpha_settings') IS NULL
   OR to_regclass('public.alpha_support_cases') IS NOT NULL
   OR to_regclass('alpha_support_private.manual_mail_cases') IS NOT NULL
 THEN RAISE EXCEPTION 'TASK21_MANUAL_MAIL_PRODUCTION_PREREQUISITE_FAILED';
 END IF;
END $task21_production_gate$;

CREATE SCHEMA IF NOT EXISTS alpha_support_private;
REVOKE ALL ON SCHEMA alpha_support_private FROM PUBLIC, anon, authenticated, service_role;

CREATE TABLE alpha_support_private.manual_mail_cases (
 case_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 channel text NOT NULL DEFAULT 'GMAIL_MANUAL_SUPPORT' CHECK (channel='GMAIL_MANUAL_SUPPORT'),
 provider_message_id text NOT NULL UNIQUE
   CHECK (provider_message_id ~ '^[0-9a-f]{12,40}$'),
 category text NOT NULL CHECK (category IN
   ('general','advertiser','creator','billing','refund','failed_campaign','privacy','security')),
 status text NOT NULL DEFAULT 'OPEN' CHECK (status IN
   ('OPEN','IN_REVIEW','RESOLVED','CLOSED')),
 observed_at timestamptz NOT NULL,
 created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 CHECK (observed_at <= created_at + interval '1 day'),
 CHECK (observed_at >= created_at - interval '90 days')
);
CREATE INDEX manual_mail_cases_open
 ON alpha_support_private.manual_mail_cases(status,observed_at)
 WHERE status IN ('OPEN','IN_REVIEW');

CREATE TABLE alpha_support_private.manual_mail_case_events (
 event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 case_id uuid NOT NULL REFERENCES alpha_support_private.manual_mail_cases(case_id) ON DELETE CASCADE,
 action text NOT NULL CHECK (action IN ('CREATED','STATUS_CHANGED')),
 case_status text NOT NULL CHECK (case_status IN ('OPEN','IN_REVIEW','RESOLVED','CLOSED')),
 occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX manual_mail_case_events_case_idx
 ON alpha_support_private.manual_mail_case_events(case_id,event_id);
ALTER TABLE alpha_support_private.manual_mail_cases ENABLE ROW LEVEL SECURITY;
ALTER TABLE alpha_support_private.manual_mail_case_events ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON ALL TABLES IN SCHEMA alpha_support_private
 FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA alpha_support_private
 FROM PUBLIC, anon, authenticated, service_role;

CREATE FUNCTION alpha_support_private.manual_mail_capture_audit()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $task21_mail_audit$
BEGIN
 IF TG_OP='INSERT' THEN
  INSERT INTO alpha_support_private.manual_mail_case_events(case_id,action,case_status)
  VALUES(NEW.case_id,'CREATED',NEW.status);
  RETURN NEW;
 END IF;
 IF (NEW.case_id,NEW.channel,NEW.provider_message_id,NEW.category,NEW.observed_at,NEW.created_at)
    IS DISTINCT FROM
    (OLD.case_id,OLD.channel,OLD.provider_message_id,OLD.category,OLD.observed_at,OLD.created_at)
 THEN RAISE EXCEPTION 'Support mail receipt identity is immutable'; END IF;
 IF OLD.status='CLOSED' OR
    (OLD.status='OPEN' AND NEW.status NOT IN ('OPEN','IN_REVIEW')) OR
    (OLD.status='IN_REVIEW' AND NEW.status NOT IN ('IN_REVIEW','RESOLVED')) OR
    (OLD.status='RESOLVED' AND NEW.status NOT IN ('RESOLVED','CLOSED'))
 THEN RAISE EXCEPTION 'Invalid manual support status transition'; END IF;
 IF NEW.status IS DISTINCT FROM OLD.status THEN
  NEW.updated_at=clock_timestamp();
  INSERT INTO alpha_support_private.manual_mail_case_events(case_id,action,case_status)
  VALUES(NEW.case_id,'STATUS_CHANGED',NEW.status);
 END IF;
 RETURN NEW;
END
$task21_mail_audit$;
CREATE TRIGGER manual_mail_audit_insert
 AFTER INSERT ON alpha_support_private.manual_mail_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.manual_mail_capture_audit();
CREATE TRIGGER manual_mail_audit_update
 BEFORE UPDATE ON alpha_support_private.manual_mail_cases
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.manual_mail_capture_audit();

CREATE FUNCTION alpha_support_private.manual_mail_events_immutable()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $task21_mail_immutable$
BEGIN RAISE EXCEPTION 'Support mail audit events are immutable'; END
$task21_mail_immutable$;
CREATE TRIGGER manual_mail_events_no_changes
 BEFORE UPDATE OR DELETE ON alpha_support_private.manual_mail_case_events
 FOR EACH ROW EXECUTE FUNCTION alpha_support_private.manual_mail_events_immutable();
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA alpha_support_private
 FROM PUBLIC, anon, authenticated, service_role;

COMMENT ON TABLE alpha_support_private.manual_mail_cases IS
 'P0 Task21: production internal mailbox metadata ONLY; no customer emails, subjects, bodies, marketing or customer-visible ticket IDs. No Data API grant. App support case system remains separately gated.';
COMMENT ON TABLE alpha_support_private.manual_mail_case_events IS
 'Append-only case audit: no customer content. Manual mailbox records have a separate 90-day cleanup.';
