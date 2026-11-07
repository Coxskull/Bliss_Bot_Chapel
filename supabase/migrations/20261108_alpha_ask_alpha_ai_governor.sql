-- ALPHA GLOBAL MEDIA LLC — Task #20, OFFLINE CANDIDATE ONLY.
-- Extend the existing Bliss Bot Supabase public intake backend.
-- Do NOT apply until backup, RLS, billing, owner-approved notice and hosted review.
-- Does not touch factory STOP, existing intake, HighLevel, campaign or payment tables.
BEGIN;
CREATE TABLE IF NOT EXISTS public.alpha_ask_alpha_settings (
  id integer PRIMARY KEY DEFAULT 1 CHECK (id=1),
  enabled boolean NOT NULL DEFAULT false,
  metered_month date,
  externally_billed_usd numeric(12,6) NOT NULL DEFAULT 0 CHECK (externally_billed_usd>=0),
  verified_at timestamptz,
  updated_at timestamptz NOT NULL DEFAULT now()
);
INSERT INTO public.alpha_ask_alpha_settings(id,enabled)
 VALUES(1,false) ON CONFLICT(id) DO NOTHING;
CREATE TABLE IF NOT EXISTS public.alpha_ask_alpha_turns (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  request_key uuid NOT NULL UNIQUE,
  session_hash text NOT NULL CHECK(session_hash ~ '^[0-9a-f]{64}$'),
  ip_hash text NOT NULL CHECK(ip_hash ~ '^[0-9a-f]{64}$'),
  input_hash text NOT NULL CHECK(input_hash ~ '^[0-9a-f]{64}$'),
  audience text NOT NULL CHECK(audience IN ('advertiser','creator','support','general')),
  notice_version text NOT NULL CHECK(notice_version='AGM-ASK-ALPHA-AI-2026-11-08'),
  marketing_opt_in boolean NOT NULL DEFAULT false,
  state text NOT NULL DEFAULT 'RESERVED' CHECK(state IN ('RESERVED','SETTLED','UNKNOWN')),
  model text NOT NULL DEFAULT 'gpt-6-luna' CHECK(model='gpt-6-luna'),
  reserved_usd numeric(12,6) NOT NULL DEFAULT 0.010000 CHECK(reserved_usd=0.010000),
  actual_usd numeric(12,6) CHECK(actual_usd>=0),
  input_tokens integer CHECK(input_tokens>=0),
  output_tokens integer CHECK(output_tokens>=0),
  provider_request_id text CHECK(length(provider_request_id)<=180),
  output_hash text CHECK(output_hash IS NULL OR output_hash ~ '^[0-9a-f]{64}$'),
  created_at timestamptz NOT NULL DEFAULT now(),
  finished_at timestamptz,
  CONSTRAINT settled_has_usage CHECK (
    state <> 'SETTLED' OR (actual_usd IS NOT NULL AND input_tokens IS NOT NULL AND
    output_tokens IS NOT NULL AND provider_request_id IS NOT NULL AND output_hash IS NOT NULL)
  )
);
CREATE INDEX IF NOT EXISTS alpha_ask_alpha_time_idx ON public.alpha_ask_alpha_turns(created_at);
CREATE INDEX IF NOT EXISTS alpha_ask_alpha_ip_window_idx ON public.alpha_ask_alpha_turns(ip_hash,created_at);
CREATE INDEX IF NOT EXISTS alpha_ask_alpha_session_window_idx ON public.alpha_ask_alpha_turns(session_hash,created_at);
ALTER TABLE public.alpha_ask_alpha_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.alpha_ask_alpha_settings FORCE ROW LEVEL SECURITY;
ALTER TABLE public.alpha_ask_alpha_turns ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.alpha_ask_alpha_turns FORCE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_ask_alpha_settings, public.alpha_ask_alpha_turns
  FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.alpha_ask_alpha_settings TO service_role;
GRANT SELECT, INSERT, UPDATE ON public.alpha_ask_alpha_turns TO service_role;
-- Backend-only RPC. All reservations serialize on a single transaction lock;
-- competing instances see both RESERVED requests and settled billed usage.
CREATE OR REPLACE FUNCTION public.alpha_ask_alpha_reserve(
 p_request_key uuid,p_session_hash text,p_ip_hash text,p_input_hash text,
 p_audience text,p_notice_version text,p_marketing_opt_in boolean
) RETURNS TABLE(receipt_id uuid,state text,is_replay boolean)
LANGUAGE plpgsql SECURITY DEFINER SET search_path='' AS $$
DECLARE
 existing public.alpha_ask_alpha_turns%ROWTYPE;
 cfg public.alpha_ask_alpha_settings%ROWTYPE;
 utc_day timestamptz := date_trunc('day',now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC';
 utc_month timestamptz := date_trunc('month',now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC';
 today_usd numeric(12,6);
 month_usd numeric(12,6);
BEGIN
 PERFORM pg_catalog.pg_advisory_xact_lock(2941,20);
 IF p_request_key IS NULL OR p_session_hash !~ '^[0-9a-f]{64}$' OR
    p_ip_hash !~ '^[0-9a-f]{64}$' OR p_input_hash !~ '^[0-9a-f]{64}$' OR
    p_audience NOT IN ('advertiser','creator','support','general') OR
    p_notice_version <> 'AGM-ASK-ALPHA-AI-2026-11-08' OR p_marketing_opt_in IS NULL
 THEN RAISE EXCEPTION 'INVALID_ASK_ALPHA_REQUEST' USING ERRCODE='22023'; END IF;
 SELECT * INTO existing FROM public.alpha_ask_alpha_turns WHERE request_key=p_request_key;
 IF FOUND THEN
   IF existing.session_hash <> p_session_hash OR existing.input_hash <> p_input_hash
   THEN RAISE EXCEPTION 'IDEMPOTENCY_CONFLICT' USING ERRCODE='23505'; END IF;
   RETURN QUERY SELECT existing.id, existing.state, true;
   RETURN;
 END IF;
 SELECT * INTO cfg FROM public.alpha_ask_alpha_settings WHERE id=1 FOR UPDATE;
 IF NOT FOUND OR cfg.enabled IS DISTINCT FROM true OR
    cfg.metered_month IS DISTINCT FROM (utc_month AT TIME ZONE 'UTC')::date OR
    cfg.verified_at IS NULL OR cfg.verified_at < now()-interval '24 hours' OR
    cfg.verified_at > now()+interval '5 minutes'
 THEN RAISE EXCEPTION 'ASK_ALPHA_METERING_NOT_VERIFIED' USING ERRCODE='P0001'; END IF;
 IF EXISTS(SELECT 1 FROM public.alpha_ask_alpha_turns WHERE state='UNKNOWN')
 THEN RAISE EXCEPTION 'ASK_ALPHA_UNRECONCILED_USAGE' USING ERRCODE='P0001'; END IF;
 IF (SELECT count(*) FROM public.alpha_ask_alpha_turns WHERE ip_hash=p_ip_hash AND
      created_at >= now()-interval '1 hour') >= 6 OR
    (SELECT count(*) FROM public.alpha_ask_alpha_turns WHERE session_hash=p_session_hash AND
      created_at >= utc_day) >= 12 OR
    (SELECT count(*) FROM public.alpha_ask_alpha_turns WHERE created_at >= utc_day) >= 100 OR
    (SELECT count(*) FROM public.alpha_ask_alpha_turns WHERE session_hash=p_session_hash AND
      state='RESERVED') >= 1
 THEN RAISE EXCEPTION 'ASK_ALPHA_RATE_LIMITED' USING ERRCODE='P0001'; END IF;
 SELECT coalesce(sum(CASE WHEN state='SETTLED' THEN greatest(actual_usd,0)
                   ELSE reserved_usd END),0)
 INTO today_usd FROM public.alpha_ask_alpha_turns WHERE created_at>=utc_day;
 SELECT coalesce(sum(CASE WHEN state='SETTLED' THEN greatest(actual_usd,0)
                   ELSE reserved_usd END),0)
 INTO month_usd FROM public.alpha_ask_alpha_turns WHERE created_at>=utc_month;
 IF today_usd+0.010000>1.000000 OR
    month_usd+cfg.externally_billed_usd+0.010000>10.000000
 THEN RAISE EXCEPTION 'ASK_ALPHA_BUDGET_EXHAUSTED' USING ERRCODE='P0001'; END IF;
 INSERT INTO public.alpha_ask_alpha_turns
  (request_key,session_hash,ip_hash,input_hash,audience,notice_version,marketing_opt_in)
 VALUES(p_request_key,p_session_hash,p_ip_hash,p_input_hash,p_audience,
        p_notice_version,p_marketing_opt_in)
 RETURNING id INTO receipt_id;
 state := 'RESERVED'; is_replay := false;
 RETURN NEXT;
END; $$;
CREATE OR REPLACE FUNCTION public.alpha_ask_alpha_settle(
 p_receipt_id uuid,p_state text,p_provider_request_id text,p_output_hash text,
 p_input_tokens integer,p_output_tokens integer,p_actual_usd numeric
) RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path='' AS $$
DECLARE r public.alpha_ask_alpha_turns%ROWTYPE;
BEGIN
 PERFORM pg_catalog.pg_advisory_xact_lock(2941,20);
 SELECT * INTO r FROM public.alpha_ask_alpha_turns WHERE id=p_receipt_id FOR UPDATE;
 IF NOT FOUND OR r.state<>'RESERVED' OR p_state NOT IN ('SETTLED','UNKNOWN')
 THEN RETURN false; END IF;
 IF p_state='SETTLED' AND
    (p_actual_usd IS NULL OR p_actual_usd<0 OR p_actual_usd>r.reserved_usd OR
     p_input_tokens IS NULL OR p_output_tokens IS NULL OR
     p_input_tokens<0 OR p_output_tokens<0 OR
     p_provider_request_id IS NULL OR length(p_provider_request_id)>180 OR
     p_output_hash !~ '^[0-9a-f]{64}$')
 THEN RETURN false; END IF;
 UPDATE public.alpha_ask_alpha_turns SET
  state=p_state,
  provider_request_id=CASE WHEN p_state='SETTLED' THEN p_provider_request_id ELSE NULL END,
  output_hash=CASE WHEN p_state='SETTLED' THEN p_output_hash ELSE NULL END,
  input_tokens=CASE WHEN p_state='SETTLED' THEN p_input_tokens ELSE NULL END,
  output_tokens=CASE WHEN p_state='SETTLED' THEN p_output_tokens ELSE NULL END,
  actual_usd=CASE WHEN p_state='SETTLED' THEN p_actual_usd ELSE NULL END,
  finished_at=now()
 WHERE id=p_receipt_id;
 RETURN true;
END; $$;
REVOKE ALL ON FUNCTION public.alpha_ask_alpha_reserve(uuid,text,text,text,text,text,boolean)
 FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.alpha_ask_alpha_settle(uuid,text,text,text,integer,integer,numeric)
 FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_ask_alpha_reserve(uuid,text,text,text,text,text,boolean) TO service_role;
GRANT EXECUTE ON FUNCTION public.alpha_ask_alpha_settle(uuid,text,text,text,integer,integer,numeric) TO service_role;
COMMIT;
