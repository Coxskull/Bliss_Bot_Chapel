-- Run ONLY after actual Task21 mailbox, operator and privacy queue source
-- migrations on disposable PostgreSQL 16/18 with no Auth or grant records.
DO $assert$
DECLARE
 v_actor uuid := gen_random_uuid();
 v_request uuid := gen_random_uuid();
 v_denied boolean := false;
BEGIN
 IF (SELECT count(*) FROM auth.users)<>0
 OR (SELECT count(*) FROM alpha_support_private.manual_operator_grants)<>0
 THEN RAISE EXCEPTION 'TASK21_FIXTURE_MUST_HAVE_ZERO_AUTH_AND_GRANTS'; END IF;
 IF to_regclass('alpha_support_private.manual_privacy_form_cases') IS NULL
 OR to_regclass('alpha_support_private.manual_privacy_form_events') IS NULL
 OR to_regclass('alpha_support_private.manual_privacy_operator_claims') IS NULL
 OR to_regclass('alpha_support_private.manual_privacy_operator_actions') IS NULL
 THEN RAISE EXCEPTION 'TASK21_PRIVATE_SCHEMA_NOT_CREATED'; END IF;

 IF public.alpha_support_manual_operator_authorized(v_actor)
 THEN RAISE EXCEPTION 'TASK21_EMPTY_BRANCH_GRANTED_OPERATOR_ACCESS'; END IF;

 BEGIN
  PERFORM 1 FROM public.alpha_support_manual_operator_queue(v_actor,0);
 EXCEPTION WHEN insufficient_privilege THEN v_denied:=true;
 END;
 IF NOT v_denied
 THEN RAISE EXCEPTION 'TASK21_UNAUTHORIZED_QUEUE_NOT_DENIED'; END IF;

 IF public.alpha_support_manual_operator_claim(v_actor,gen_random_uuid())
 THEN RAISE EXCEPTION 'TASK21_UNAUTHORIZED_CASE_CLAIM'; END IF;
 IF pg_catalog.has_function_privilege('anon',
   'public.alpha_support_manual_operator_queue(uuid,integer)','EXECUTE')
 OR pg_catalog.has_function_privilege('authenticated',
   'public.alpha_support_manual_operator_queue(uuid,integer)','EXECUTE')
 OR NOT pg_catalog.has_function_privilege('service_role',
   'public.alpha_support_manual_operator_queue(uuid,integer)','EXECUTE')
 THEN RAISE EXCEPTION 'TASK21_QUEUE_RPC_GRANTS_UNSAFE'; END IF;

 -- Synthetic test-only privacy-form event, with no customer name/email/body.
 INSERT INTO public.alpha_public_intake_requests(
  id,kind,processing_acknowledged,adult_confirmed)
 VALUES(v_request,'privacy',true,true);

 IF (SELECT count(*) FROM alpha_support_private.manual_privacy_form_cases
  WHERE source_request_id=v_request)<>1
 OR (SELECT count(*) FROM alpha_support_private.manual_privacy_form_events e
  JOIN alpha_support_private.manual_privacy_form_cases f ON f.case_id=e.case_id
  WHERE f.source_request_id=v_request AND e.event_type='CREATED')<>1
 THEN RAISE EXCEPTION 'TASK21_SYNTHETIC_PRIVACY_RECEIPT_NOT_DURABLE'; END IF;

 IF EXISTS (
  SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
  WHERE n.nspname='alpha_support_private'
   AND c.relname IN ('manual_privacy_form_cases','manual_privacy_form_events',
       'manual_privacy_operator_claims','manual_privacy_operator_actions')
   AND NOT c.relrowsecurity)
 THEN RAISE EXCEPTION 'TASK21_PRIVATE_TABLE_RLS_DISABLED'; END IF;
END $assert$;
SELECT 'TASK21_EMPTY_BRANCH_REPLAY_AND_DENY_PASS' AS result;
