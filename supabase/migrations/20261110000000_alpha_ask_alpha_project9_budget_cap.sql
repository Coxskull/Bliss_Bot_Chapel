-- ALPHA Task #20: forward-only cap to $9. $10 prepaid, $9 project limit, $1 reserve.
-- Source-only until isolated hosted authorization; never change the AI enabled flag,
-- externally billed usage, prior receipts, intake, HighLevel, or the factory STOP.
-- Preserve the original 20261108 migration; this is a separate, newer migration.
BEGIN;
DO $task20_project9$
DECLARE
  v_signature regprocedure := to_regprocedure(
    'public.alpha_ask_alpha_reserve(uuid,text,text,text,text,text,boolean)');
  v_old text := 'month_usd+cfg.externally_billed_usd+0.010000>10.000000';
  v_new text := 'month_usd+cfg.externally_billed_usd+0.010000>9.000000';
  v_source text;
  v_count integer;
BEGIN
  IF v_signature IS NULL THEN
    RAISE EXCEPTION 'TASK20_PROJECT9_REQUIRED_GOVERNOR_MISSING';
  END IF;
  IF (SELECT enabled FROM public.alpha_ask_alpha_settings WHERE id=1) IS DISTINCT FROM false THEN
    RAISE EXCEPTION 'TASK20_PROJECT9_REQUIRES_AI_DISABLED';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE oid=v_signature AND prosecdef) THEN
    RAISE EXCEPTION 'TASK20_PROJECT9_SECURITY_DEFINER_MISSING';
  END IF;
  v_source := pg_get_functiondef(v_signature);
  v_count := (length(v_source)-length(replace(v_source,v_old,''))) / length(v_old);
  IF v_count = 1 THEN
    -- Preserve original identity, RLS, advisory locking, receipt validation,
    -- rate limits, metering-freshness tests, privileges and search_path.
    EXECUTE replace(v_source,v_old,v_new);
  ELSIF v_count = 0 AND
       (length(v_source)-length(replace(v_source,v_new,''))) / length(v_new) = 1 THEN
    NULL; -- already aligned; idempotent under controlled repeat
  ELSE
    RAISE EXCEPTION 'TASK20_PROJECT9_UNEXPECTED_GOVERNOR_BODY';
  END IF;
  IF position(v_new in pg_get_functiondef(v_signature))=0 OR
     position(v_old in pg_get_functiondef(v_signature))>0 THEN
    RAISE EXCEPTION 'TASK20_PROJECT9_POST_UPDATE_VALIDATION_FAILED';
  END IF;
  IF has_function_privilege('anon',v_signature,'EXECUTE') OR
     has_function_privilege('authenticated',v_signature,'EXECUTE') OR
     NOT has_function_privilege('service_role',v_signature,'EXECUTE') THEN
    RAISE EXCEPTION 'TASK20_PROJECT9_PRIVATE_RPC_PRIVILEGES_CHANGED';
  END IF;
  IF (SELECT enabled FROM public.alpha_ask_alpha_settings WHERE id=1) IS DISTINCT FROM false THEN
    RAISE EXCEPTION 'TASK20_PROJECT9_AI_ENABLED_UNEXPECTEDLY';
  END IF;
  RAISE NOTICE 'TASK20_PROJECT9_FORWARD_ONLY_GOVERNOR_CAP_PASS';
END;
$task20_project9$;
COMMIT;
