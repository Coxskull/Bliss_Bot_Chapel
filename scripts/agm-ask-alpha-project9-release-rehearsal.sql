-- ALPHA Task #20: isolated / disposable PG17 final release and emergency STOP rehearsal.
-- NOT for live Supabase, not for confidential data. Run after base and $9 governor fixtures.
\set ON_ERROR_STOP on
DO $task20_rehearsal$
DECLARE
  v_legacy text := 'month_usd+cfg.externally_billed_usd+0.010000>10.000000';
  v_current text := 'month_usd+cfg.externally_billed_usd+0.010000>9.000000';
  v_proc regprocedure := 'public.alpha_ask_alpha_reserve(uuid,text,text,text,text,text,boolean)'::regprocedure;
  v_body text := pg_get_functiondef(v_proc);
  denied boolean := false;
BEGIN
  IF (SELECT enabled FROM public.alpha_ask_alpha_settings WHERE id=1) IS DISTINCT FROM false THEN
    RAISE EXCEPTION 'TASK20_RELEASE_REHEARSAL_NOT_DISABLED';
  END IF;
  IF position(v_current IN v_body)=0 OR position(v_legacy IN v_body)>0 THEN
    RAISE EXCEPTION 'TASK20_RELEASE_REHEARSAL_WRONG_BUDGET';
  END IF;
  IF has_function_privilege('anon',v_proc,'EXECUTE') OR
     has_function_privilege('authenticated',v_proc,'EXECUTE') OR
     NOT has_function_privilege('service_role',v_proc,'EXECUTE') THEN
    RAISE EXCEPTION 'TASK20_RELEASE_REHEARSAL_PRIVILEGE_REGRESSION';
  END IF;
  IF NOT (SELECT relrowsecurity AND relforcerowsecurity
          FROM pg_class WHERE oid='public.alpha_ask_alpha_turns'::regclass) THEN
    RAISE EXCEPTION 'TASK20_RELEASE_REHEARSAL_RLS_DISABLED';
  END IF;
  BEGIN
    PERFORM * FROM public.alpha_ask_alpha_reserve(
      '00000000-0000-4000-8000-000000000779',
      repeat('a',64),repeat('b',64),repeat('c',64),
      'general','AGM-ASK-ALPHA-AI-2026-11-08',false);
  EXCEPTION WHEN SQLSTATE 'P0001' THEN
    denied := true;
  END;
  IF NOT denied THEN
    RAISE EXCEPTION 'TASK20_RELEASE_REHEARSAL_DISABLED_GOVERNOR_ADMITTED_RESERVATION';
  END IF;
  RAISE NOTICE 'TASK20_PROJECT9_FAILCLOSED_REHEARSAL_PASS';
END;
$task20_rehearsal$;
