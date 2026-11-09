-- Task21 alert intents: acceptance in isolated PostgreSQL 17 ONLY.
-- This runs after the existing support case/role tests, never against live data.
DO $task21_alert_check$
DECLARE v_privacy uuid; v_request uuid; v_fail boolean;
BEGIN
 IF (SELECT count(*) FROM public.alpha_support_alert_intents) <> 5
 THEN RAISE EXCEPTION 'Expected precisely 4 HIGH and 1 CRITICAL unrouted intents'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_alert_intents WHERE severity='HIGH')<>4
 OR (SELECT count(*) FROM public.alpha_support_alert_intents WHERE severity='CRITICAL')<>1
 THEN RAISE EXCEPTION 'Incorrect alert severity distribution'; END IF;
 IF EXISTS(SELECT 1 FROM public.alpha_support_alert_intents i
   JOIN public.alpha_support_cases c ON c.case_id=i.case_id
   WHERE c.severity NOT IN ('HIGH','CRITICAL')
      OR c.severity IS DISTINCT FROM i.severity
      OR c.assigned_role IS DISTINCT FROM i.assigned_role)
 THEN RAISE EXCEPTION 'Normal case routed to alert or role mismatched'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public.alpha_support_alert_intents i
   JOIN public.alpha_support_cases c ON c.case_id=i.case_id
   WHERE c.category='security_incident'
      AND i.assigned_role='SECURITY_REVIEW'
      AND i.delivery_state='UNROUTED')
 THEN RAISE EXCEPTION 'Critical security incident missing unrouted receipt'; END IF;
 IF (SELECT count(*) FROM public.alpha_support_alert_intents
       WHERE delivery_state='UNROUTED')<>5
 THEN RAISE EXCEPTION 'False delivered/acknowledged state'; END IF;
 IF EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a
   JOIN pg_catalog.pg_class c ON a.attrelid=c.oid
   WHERE c.oid='public.alpha_support_alert_intents'::regclass
     AND a.attnum>0 AND NOT a.attisdropped
     AND a.attname ~* '(email|contact|phone|token|message|payload|person|name|address|recipient|secret|body)')
 THEN RAISE EXCEPTION 'Alert intent contains sensitive/outbound delivery column'; END IF;
 IF has_table_privilege('anon','public.alpha_support_alert_intents','SELECT')
 OR has_table_privilege('authenticated','public.alpha_support_alert_intents','SELECT')
 OR has_table_privilege('authenticated','public.alpha_support_alert_intents','INSERT')
 OR has_table_privilege('service_role','public.alpha_support_alert_intents','UPDATE')
 OR has_table_privilege('service_role','public.alpha_support_alert_intents','DELETE')
 OR has_function_privilege('authenticated',
   'public.alpha_support_alert_on_case()','EXECUTE')
 THEN RAISE EXCEPTION 'Alert receipt privilege is not fail-closed'; END IF;
 -- The retention path must cascade only a matching case/intent, never
 -- directly grant service_role deletion of an alert receipt.
 SELECT c.case_id,c.request_id INTO v_privacy,v_request
 FROM public.alpha_support_cases c WHERE c.category='privacy';
 DELETE FROM public.alpha_public_intake_requests WHERE id=v_request;
 IF EXISTS(SELECT 1 FROM public.alpha_support_alert_intents
   WHERE case_id=v_privacy) OR
 (SELECT count(*) FROM public.alpha_support_alert_intents)<>4
 THEN RAISE EXCEPTION 'Retention cascade left an orphan alert'; END IF;
END;
$task21_alert_check$;

-- CI's minimal public-intake fixture intentionally does not grant
-- service_role intake INSERT (unlike the real Supabase staging baseline).
-- Create the fictional intake as the CI fixture owner, THEN prove the alert
-- is visible to restricted service_role and cannot be falsified by it.
INSERT INTO public.alpha_public_intake_requests
  (request_key,kind,full_name,email,description,category,notice_version,
   marketing_opt_in)
VALUES (gen_random_uuid(),'support','SYNTHETIC ALERT RECEIPT ONLY',
 'task21-alert-ci@example.invalid','Fictional verification - no contact',
 'security_incident','AGM-SUPPORT-2026-11-08',false);
SET ROLE service_role;
DO $task21_alert_role$
DECLARE aid uuid; denied boolean:=false;
BEGIN
 SELECT a.alert_id INTO aid FROM public.alpha_support_alert_intents a
 JOIN public.alpha_support_cases c ON c.case_id=a.case_id
 WHERE c.category='security_incident'
   AND a.delivery_state='UNROUTED'
 ORDER BY a.created_at DESC LIMIT 1;
 IF aid IS NULL THEN RAISE EXCEPTION 'Service role incident did not record intent'; END IF;
 BEGIN
   UPDATE public.alpha_support_alert_intents
   SET delivery_state='DELIVERED' WHERE alert_id=aid;
 EXCEPTION WHEN insufficient_privilege OR check_violation THEN denied:=true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Service role faked outbound delivery'; END IF;
END;
$task21_alert_role$;
RESET ROLE;
SELECT 'TASK21_ALERT_INTENT_OFFLINE_ACCEPTANCE_PASS' AS acceptance_status;
