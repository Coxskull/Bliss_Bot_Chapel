-- Task #21 staging-only public-intake prerequisite for the existing
-- alpha-first-five-staging development branch. Do NOT run against production.
-- This faithfully provides only the base intake/receipt objects needed by
-- the existing 20261105 GHL and 20261108 support migrations; no new service,
-- email delivery, credentials, or permanent customer records are created here.
-- Apply as ONE atomic migration alongside the exact existing GHL + Task21 SQL.
DO $task21_staging_guard$
BEGIN
 IF EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n
   ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN('r','p'))
 OR to_regclass('public.alpha_public_intake_requests') IS NOT NULL
 OR to_regclass('public.alpha_public_intake_receipts') IS NOT NULL
 OR to_regclass('public.alpha_ghl_delivery') IS NOT NULL
 OR to_regclass('public.alpha_support_cases') IS NOT NULL
 OR EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname='alpha_support_private')
 THEN RAISE EXCEPTION 'TASK21 staging prerequisite rejected existing public/support objects'; END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='service_role')
 THEN RAISE EXCEPTION 'Service role unavailable'; END IF;
END $task21_staging_guard$;

CREATE TABLE public.alpha_public_intake_requests(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 request_key uuid NOT NULL UNIQUE,
 kind text NOT NULL CONSTRAINT alpha_public_intake_requests_kind_check
   CHECK (kind IN ('creator','advertiser','privacy')),
 full_name text NOT NULL CHECK (char_length(full_name) BETWEEN 1 AND 200),
 email text NOT NULL CHECK (char_length(email) BETWEEN 3 AND 320),
 organization text NOT NULL DEFAULT '',
 website text NOT NULL DEFAULT '',
 market text NOT NULL DEFAULT '',
 category text NOT NULL DEFAULT '',
 description text NOT NULL DEFAULT '',
 processing_acknowledged boolean NOT NULL DEFAULT true,
 adult_confirmed boolean NOT NULL DEFAULT true,
 marketing_opt_in boolean NOT NULL DEFAULT false,
 notice_version text NOT NULL CONSTRAINT alpha_public_intake_requests_notice_version_check
   CHECK (notice_version IN ('AGM-INTEREST-2026-11-05','AGM-INTEREST-2026-11-05-GHL')),
 intake_status text NOT NULL DEFAULT 'NOT_YET_VERIFIED',
 created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 CONSTRAINT alpha_public_intake_kind_data CHECK (
 (kind='creator' AND organization='' AND website<>'' AND market<>'' AND category<>'')
 OR (kind='advertiser' AND organization<>'' AND market<>'')
 OR (kind='privacy' AND description<>''))
);
CREATE UNIQUE INDEX alpha_public_intake_identity ON public.alpha_public_intake_requests
 USING btree (kind,email) WHERE kind IN('creator','advertiser');
ALTER TABLE public.alpha_public_intake_requests ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_public_intake_requests FROM PUBLIC,anon,authenticated;
GRANT SELECT,INSERT,UPDATE,DELETE ON public.alpha_public_intake_requests TO service_role;

CREATE TABLE public.alpha_public_intake_receipts(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 request_id uuid UNIQUE NOT NULL REFERENCES public.alpha_public_intake_requests(id)
   ON DELETE CASCADE,
 kind text NOT NULL CONSTRAINT alpha_public_intake_receipts_kind_check
   CHECK (kind IN ('creator','advertiser','privacy')),
 notice_version text NOT NULL CONSTRAINT alpha_public_intake_receipts_notice_version_check
   CHECK (notice_version IN ('AGM-INTEREST-2026-11-05','AGM-INTEREST-2026-11-05-GHL')),
 recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
ALTER TABLE public.alpha_public_intake_receipts ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.alpha_public_intake_receipts FROM PUBLIC,anon,authenticated;
GRANT SELECT,INSERT,UPDATE,DELETE ON public.alpha_public_intake_receipts TO service_role;

CREATE FUNCTION public.alpha_public_intake_make_receipt()
RETURNS trigger LANGUAGE plpgsql SECURITY INVOKER
SET search_path=pg_catalog AS $task21_receipt_body$
BEGIN
 INSERT INTO public.alpha_public_intake_receipts(request_id,kind,notice_version)
 VALUES(NEW.id,NEW.kind,NEW.notice_version);
 RETURN NEW;
END;
$task21_receipt_body$;
CREATE TRIGGER alpha_public_intake_receipt_on_insert
 AFTER INSERT ON public.alpha_public_intake_requests FOR EACH ROW
 EXECUTE FUNCTION public.alpha_public_intake_make_receipt();
REVOKE EXECUTE ON FUNCTION public.alpha_public_intake_make_receipt()
 FROM PUBLIC,anon,authenticated;
