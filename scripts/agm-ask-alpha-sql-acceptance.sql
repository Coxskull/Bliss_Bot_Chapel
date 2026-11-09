-- Task #20 synthetic SQL-only acceptance on existing CI PostgreSQL fixture.
-- Never run this against Supabase or a populated/business database.
\set ON_ERROR_STOP on
DO $$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='anon')
 THEN CREATE ROLE anon NOLOGIN; END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='authenticated')
 THEN CREATE ROLE authenticated NOLOGIN; END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='service_role')
 THEN CREATE ROLE service_role NOLOGIN; END IF;
END $$;
\ir ../supabase/migrations/20261108_alpha_ask_alpha_ai_governor.sql
DO $$
DECLARE
  candidate record;
  settled boolean;
  denied boolean := false;
  session_h text := repeat('a',64);
  ip_h text := repeat('b',64);
  input_h text := repeat('c',64);
  key_a uuid := '00000000-0000-4000-8000-000000000100';
  key_b uuid := '00000000-0000-4000-8000-000000000101';
  key_c uuid := '00000000-0000-4000-8000-000000000102';
BEGIN
 IF (SELECT enabled FROM public.alpha_ask_alpha_settings WHERE id=1) IS DISTINCT FROM false
 THEN RAISE EXCEPTION 'AI governor must default disabled'; END IF;
 IF NOT (SELECT relrowsecurity AND relforcerowsecurity FROM pg_catalog.pg_class
          WHERE oid='public.alpha_ask_alpha_turns'::regclass)
 THEN RAISE EXCEPTION 'AI receipt RLS/FORCE missing'; END IF;
 IF pg_catalog.has_table_privilege('anon','public.alpha_ask_alpha_turns','SELECT') OR
    pg_catalog.has_table_privilege('authenticated','public.alpha_ask_alpha_turns','SELECT') OR
    pg_catalog.has_function_privilege('anon',
      'public.alpha_ask_alpha_reserve(uuid,text,text,text,text,text,boolean)','EXECUTE') OR
    pg_catalog.has_function_privilege('authenticated',
      'public.alpha_ask_alpha_settle(uuid,text,text,text,integer,integer,numeric)','EXECUTE')
 THEN RAISE EXCEPTION 'AI receipt table/RPC inadvertently public'; END IF;

 BEGIN
  PERFORM * FROM public.alpha_ask_alpha_reserve(
    key_a,session_h,ip_h,input_h,'advertiser','AGM-ASK-ALPHA-AI-2026-11-08',false);
 EXCEPTION WHEN SQLSTATE 'P0001' THEN denied := true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Disabled AI governor admitted a reservation'; END IF;

 UPDATE public.alpha_ask_alpha_settings SET enabled=true,
  metered_month=date_trunc('month',now() AT TIME ZONE 'UTC')::date,
  verified_at=now(), externally_billed_usd=0 WHERE id=1;
 SELECT receipt_id,state,is_replay INTO candidate
 FROM public.alpha_ask_alpha_reserve(
    key_a,session_h,ip_h,input_h,'advertiser','AGM-ASK-ALPHA-AI-2026-11-08',false);
 IF candidate.receipt_id IS NULL OR candidate.state <> 'RESERVED' OR candidate.is_replay
 THEN RAISE EXCEPTION 'First reservation did not commit'; END IF;

 SELECT is_replay,state INTO candidate
 FROM public.alpha_ask_alpha_reserve(
    key_a,session_h,ip_h,input_h,'advertiser','AGM-ASK-ALPHA-AI-2026-11-08',false);
 IF candidate.is_replay IS DISTINCT FROM true OR candidate.state <> 'RESERVED'
 THEN RAISE EXCEPTION 'Idempotency replay was not read-only'; END IF;
 IF (SELECT count(*) FROM public.alpha_ask_alpha_turns) <> 1
 THEN RAISE EXCEPTION 'Replay created another reservation'; END IF;

 SELECT public.alpha_ask_alpha_settle(id,'SETTLED','resp_synthetic',
          repeat('d',64),100,40,0.000030) INTO settled
 FROM public.alpha_ask_alpha_turns WHERE request_key=key_a;
 IF settled IS DISTINCT FROM true
 THEN RAISE EXCEPTION 'Durable settlement failed'; END IF;
 IF (SELECT state FROM public.alpha_ask_alpha_turns WHERE request_key=key_a) <> 'SETTLED'
 THEN RAISE EXCEPTION 'Settlement not persisted'; END IF;

 UPDATE public.alpha_ask_alpha_settings SET externally_billed_usd=10 WHERE id=1;
 denied := false;
 BEGIN
  PERFORM * FROM public.alpha_ask_alpha_reserve(
   key_b,session_h,ip_h,input_h,'creator','AGM-ASK-ALPHA-AI-2026-11-08',false);
 EXCEPTION WHEN SQLSTATE 'P0001' THEN denied := true; END;
 IF NOT denied OR (SELECT count(*) FROM public.alpha_ask_alpha_turns) <> 1
 THEN RAISE EXCEPTION 'Monthly hard cap allowed an overrun'; END IF;

 UPDATE public.alpha_ask_alpha_settings SET externally_billed_usd=0,
  verified_at=now() - interval '25 hours' WHERE id=1;
 denied := false;
 BEGIN
  PERFORM * FROM public.alpha_ask_alpha_reserve(
   key_b,session_h,ip_h,input_h,'creator','AGM-ASK-ALPHA-AI-2026-11-08',false);
 EXCEPTION WHEN SQLSTATE 'P0001' THEN denied := true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Stale billing meter allowed a call'; END IF;

 UPDATE public.alpha_ask_alpha_settings SET verified_at=now() WHERE id=1;
 SELECT receipt_id,state,is_replay INTO candidate
 FROM public.alpha_ask_alpha_reserve(
   key_b,session_h,ip_h,input_h,'creator','AGM-ASK-ALPHA-AI-2026-11-08',false);
 IF candidate.state <> 'RESERVED' OR candidate.is_replay
 THEN RAISE EXCEPTION 'Second reservation failed'; END IF;
 SELECT public.alpha_ask_alpha_settle(candidate.receipt_id,'UNKNOWN',NULL,NULL,NULL,NULL,NULL)
  INTO settled;
 IF settled IS DISTINCT FROM true THEN RAISE EXCEPTION 'UNKNOWN not retained'; END IF;
 denied := false;
 BEGIN
  PERFORM * FROM public.alpha_ask_alpha_reserve(
    key_c,session_h,ip_h,input_h,'support','AGM-ASK-ALPHA-AI-2026-11-08',false);
 EXCEPTION WHEN SQLSTATE 'P0001' THEN denied := true; END;
 IF NOT denied THEN RAISE EXCEPTION 'Unreconciled usage did not fail closed'; END IF;
 RAISE NOTICE 'TASK20_SQL_ATOMIC_BUDGET_RLS_IDEMPOTENCY_FAILCLOSED_PASS';
END $$;
