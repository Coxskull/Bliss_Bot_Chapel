-- TASK21 STAGING-ONLY ROLLBACK PLAN. NOT EXECUTED.
-- Target strictly Supabase branch wdlmcriajgsretgyusmo.
-- Never run against Bliss Bot production or any branch holding real intake.
-- Intentionally RESTRICT (not CASCADE) so unexpected dependencies abort.
-- Requires an explicit new operational authorization before execution.
BEGIN;
SET LOCAL statement_timeout='45s';
SET LOCAL lock_timeout='3s';
DO $task21_rollback_guard$
BEGIN
 IF (SELECT count(*) FROM public.alpha_public_intake_requests)<>1
 OR NOT EXISTS (SELECT 1 FROM public.alpha_public_intake_requests
   WHERE request_key='5a6b3f11-1234-4a1f-9a44-58e1e0000021'::uuid
   AND email='task21-staging-acceptance@example.invalid'
   AND marketing_opt_in=false)
 OR (SELECT count(*) FROM public.alpha_support_cases)<>1
 OR (SELECT count(*) FROM public.alpha_support_case_events)<>1
 OR (SELECT count(*) FROM public.alpha_public_intake_receipts)<>1
 OR (SELECT count(*) FROM public.alpha_ghl_delivery WHERE state='BLOCKED')<>1
 THEN RAISE EXCEPTION 'Unexpected live/staging activity: refuse Task21 rollback'; END IF;
 IF (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='alpha_factory')<>37
 OR (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='alpha_prototype')<>5
 THEN RAISE EXCEPTION 'Shared factory schema changed: manual recovery review required'; END IF;
END $task21_rollback_guard$;
DROP TABLE public.alpha_support_case_events RESTRICT;
DROP TABLE public.alpha_support_cases RESTRICT;
DROP TABLE public.alpha_public_intake_receipts RESTRICT;
DROP TABLE public.alpha_ghl_delivery RESTRICT;
DROP TABLE public.alpha_public_intake_requests RESTRICT;
DROP FUNCTION public.alpha_support_claim(uuid,uuid) RESTRICT;
DROP FUNCTION public.alpha_support_transition(uuid,text,uuid,text,uuid) RESTRICT;
DROP FUNCTION public.alpha_support_immutable_event() RESTRICT;
DROP FUNCTION public.alpha_support_on_intake() RESTRICT;
DROP FUNCTION public.alpha_ghl_delivery_on_intake() RESTRICT;
DROP FUNCTION public.alpha_ghl_claim(uuid) RESTRICT;
DROP FUNCTION public.alpha_ghl_finish(uuid,uuid,text,integer,text,text) RESTRICT;
DROP FUNCTION public.alpha_public_intake_make_receipt() RESTRICT;
DROP FUNCTION alpha_support_private.auth_user_exists(uuid) RESTRICT;
DROP SCHEMA alpha_support_private RESTRICT;
COMMIT;
-- IMPORTANT: A rollback of schema objects does NOT undo the Supabase
-- schema_migrations ledger entry. Repair migration history separately only
-- after a reviewed plan; do not fake migration success or reset the branch.
