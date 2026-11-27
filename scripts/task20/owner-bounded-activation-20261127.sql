-- Owner-approved bounded activation, 2026-11-27.
-- No external billing verification is asserted or updated.
-- Expires automatically 2026-11-28 04:20:56 UTC. No automatic renewal.
DO $bounded_activation$
DECLARE
  f text;
  old_gate text := $gate$    cfg.verified_at IS NULL OR cfg.verified_at < now()-interval '24 hours' OR
    cfg.verified_at > now()+interval '5 minutes'$gate$;
  new_gate text := $gate$    cfg.verified_at IS NULL OR cfg.verified_at > now()+interval '5 minutes' OR
    (cfg.verified_at < now()-interval '24 hours' AND NOT (
      clock_timestamp() >= '2026-11-27 04:20:56+00'::timestamptz AND
      clock_timestamp() < '2026-11-28 04:20:56+00'::timestamptz
    ))$gate$;
  old_guard text := $guard$ IF EXISTS(SELECT 1 FROM public.alpha_ask_alpha_turns t WHERE t.state='UNKNOWN')$guard$;
  new_guard text := $guard$ -- Owner-approved bounded activation: the meter remains stale and truthful.
 IF cfg.verified_at < now()-interval '24 hours' THEN
   IF (SELECT coalesce(sum(greatest(0.010000,coalesce(t.reserved_usd,0),coalesce(t.actual_usd,0))),0)
       FROM public.alpha_ask_alpha_turns t
       WHERE t.created_at >= '2026-11-27 04:20:56+00'::timestamptz) + 0.010000 > 0.100000
   THEN RAISE EXCEPTION 'ASK_ALPHA_BUDGET_EXHAUSTED' USING ERRCODE='P0001'; END IF;
 END IF;
 IF EXISTS(SELECT 1 FROM public.alpha_ask_alpha_turns t WHERE t.state='UNKNOWN')$guard$;
BEGIN
  PERFORM pg_catalog.pg_advisory_xact_lock(2941,20);
  SELECT pg_get_functiondef('public.alpha_ask_alpha_reserve(uuid,text,text,text,text,boolean)'::regprocedure) INTO f;
  IF position(old_gate IN f)=0 OR position(old_guard IN f)=0 OR
     position('month_usd+cfg.externally_billed_usd+0.010000>9.000000' IN f)=0 OR
     position('today_usd+0.010000>1.000000' IN f)=0 OR
     position('Owner-approved bounded activation' IN f)>0
  THEN RAISE EXCEPTION 'UNEXPECTED_ASK_ALPHA_BASELINE'; END IF;
  IF clock_timestamp() >= '2026-11-28 04:20:56+00'::timestamptz
  THEN RAISE EXCEPTION 'ACTIVATION_WINDOW_EXPIRED'; END IF;
  IF NOT EXISTS (SELECT 1 FROM public.alpha_ask_alpha_settings WHERE id=1 AND enabled AND metered_month='2026-11-01'::date
      AND verified_at IS NOT NULL AND verified_at < now()-interval '24 hours')
      OR EXISTS(SELECT 1 FROM public.alpha_ask_alpha_turns WHERE state IN ('UNKNOWN','RESERVED'))
  THEN RAISE EXCEPTION 'ACTIVATION_PRECONDITION_FAILED'; END IF;
  EXECUTE replace(replace(f,old_gate,new_gate),old_guard,new_guard);
END
$bounded_activation$;
