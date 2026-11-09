-- ALPHA Task21 — STAGING-ONLY fail-closed alert INTENTS, not notifications.
-- Prerequisite: approved existing alpha-first-five-staging Task21 cases.
-- Do not run in production or outside an isolated synthetic staging project.
-- An intent exists so a future approved human channel can be configured;
-- this migration does NOT notify, acknowledge, page, email, text or schedule.
DO $alert_stage_guard$
BEGIN
 IF to_regclass('public.alpha_support_cases') IS NULL
 OR to_regclass('public.alpha_support_case_events') IS NULL
 OR to_regclass('public.alpha_support_alert_intents') IS NOT NULL
 OR EXISTS (SELECT 1 FROM pg_catalog.pg_proc p
   JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
   WHERE n.nspname='public' AND p.proname='alpha_support_alert_on_case')
 THEN RAISE EXCEPTION 'Task21 alert intent migration prerequisites/collision failed'; END IF;
END $alert_stage_guard$;

-- PII-free evidence of HIGH/CRITICAL escalation waiting for staffing.
-- Intentionally NO deliver-to address, provider token, email, message,
-- free-text description, or ability to mark SENT/ACKED. One per case.
CREATE TABLE public.alpha_support_alert_intents (
  alert_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  case_id uuid NOT NULL UNIQUE
    REFERENCES public.alpha_support_cases(case_id) ON DELETE CASCADE,
  severity text NOT NULL CHECK (severity IN ('HIGH','CRITICAL')),
  assigned_role text NOT NULL CHECK (assigned_role IN
    ('BILLING_REVIEW','PRIVACY_REVIEW','SECURITY_REVIEW')),
  delivery_state text NOT NULL DEFAULT 'UNROUTED'
    CHECK (delivery_state='UNROUTED'),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX alpha_support_alert_intents_unrouted
  ON public.alpha_support_alert_intents(severity,created_at)
  WHERE delivery_state='UNROUTED';
ALTER TABLE public.alpha_support_alert_intents ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_support_alert_intents
  FROM PUBLIC,anon,authenticated,service_role;
GRANT SELECT,INSERT ON public.alpha_support_alert_intents TO service_role;

-- Only HIGH and CRITICAL cases enter this *unrouted* audit queue.
CREATE FUNCTION public.alpha_support_alert_on_case()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $task21_alert_intent$
BEGIN
 IF NEW.severity IN ('HIGH','CRITICAL') THEN
   INSERT INTO public.alpha_support_alert_intents(
     case_id,severity,assigned_role
   ) VALUES(NEW.case_id,NEW.severity,NEW.assigned_role)
   ON CONFLICT (case_id) DO NOTHING;
 END IF;
 RETURN NEW;
END;
$task21_alert_intent$;
REVOKE EXECUTE ON FUNCTION public.alpha_support_alert_on_case()
  FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_support_alert_on_case()
  TO service_role;
CREATE TRIGGER alpha_support_alert_after_case
  AFTER INSERT ON public.alpha_support_cases
  FOR EACH ROW EXECUTE FUNCTION public.alpha_support_alert_on_case();
