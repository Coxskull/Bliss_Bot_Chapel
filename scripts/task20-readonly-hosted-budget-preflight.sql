-- Task20 Ask Alpha: read-only catalog and aggregate preflight.
-- Safe to run on the actual Bliss Bot project as a SELECT-only diagnostic.
-- Returns NO user/customer content, keys, tokens, IPs, provider requests or secrets.
-- This does NOT certify backup readiness, API key/project binding or human release.
WITH
  rpc AS (
    SELECT oid, prosecdef, pg_get_functiondef(oid) AS body
    FROM pg_proc
    WHERE oid = to_regprocedure('public.alpha_ask_alpha_reserve(uuid,text,text,text,text,text,boolean)')
  ),
  metering AS (
    SELECT enabled, externally_billed_usd, metered_month
    FROM public.alpha_ask_alpha_settings WHERE id=1
  ),
  private_tables AS (
    SELECT count(*) = 2
       AND bool_and(relrowsecurity AND relforcerowsecurity)
       AND bool_and(NOT has_table_privilege('anon',oid,'SELECT'))
       AND bool_and(NOT has_table_privilege('authenticated',oid,'SELECT')) AS guarded
    FROM pg_class
    WHERE oid IN ('public.alpha_ask_alpha_settings'::regclass,
                  'public.alpha_ask_alpha_turns'::regclass)
  )
SELECT jsonb_build_object(
  'ai_disabled',coalesce((SELECT enabled=false FROM metering),false),
  'sql_cap_9',coalesce((SELECT position(
     'month_usd+cfg.externally_billed_usd+0.010000>9.000000' IN body)>0 FROM rpc),false),
  'legacy_cap_10',coalesce((SELECT position(
     'month_usd+cfg.externally_billed_usd+0.010000>10.000000' IN body)>0 FROM rpc),false),
  'rpc_restricted',coalesce((SELECT prosecdef
    AND NOT has_function_privilege('anon',oid,'EXECUTE')
    AND NOT has_function_privilege('authenticated',oid,'EXECUTE')
    AND has_function_privilege('service_role',oid,'EXECUTE')
    FROM rpc),false),
  'private_rls',coalesce((SELECT guarded FROM private_tables),false),
  'receipt_count',(SELECT count(*) FROM public.alpha_ask_alpha_turns),
  'unresolved_count',(SELECT count(*) FROM public.alpha_ask_alpha_turns
     WHERE state='UNKNOWN'),
  'metered_month',(SELECT metered_month FROM metering),
  'externally_billed_usd',(SELECT externally_billed_usd FROM metering)
)::text AS task20_readonly_preflight;
