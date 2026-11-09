-- Disposable PostgreSQL17 CI fixture only. DO NOT apply to any ALPHA project.
CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
CREATE ROLE service_role NOLOGIN BYPASSRLS; -- real Supabase hosted service_role has rolbypassrls=true
CREATE SCHEMA auth;
CREATE TABLE auth.users(id uuid PRIMARY KEY);
CREATE TABLE public.alpha_public_intake_requests(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 request_key uuid NOT NULL UNIQUE,
 kind text NOT NULL CONSTRAINT alpha_public_intake_requests_kind_check
   CHECK (kind IN ('creator','advertiser','privacy')),
 full_name text NOT NULL, email text NOT NULL,
 organization text NOT NULL DEFAULT '', website text NOT NULL DEFAULT '',
 market text NOT NULL DEFAULT '',category text NOT NULL DEFAULT '',
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
CREATE TABLE public.alpha_public_intake_receipts(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 request_id uuid UNIQUE NOT NULL REFERENCES public.alpha_public_intake_requests(id) ON DELETE CASCADE,
 kind text NOT NULL CONSTRAINT alpha_public_intake_receipts_kind_check
   CHECK (kind IN ('creator','advertiser','privacy')),
 notice_version text NOT NULL CONSTRAINT alpha_public_intake_receipts_notice_version_check
   CHECK (notice_version IN ('AGM-INTEREST-2026-11-05','AGM-INTEREST-2026-11-05-GHL')),
 recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE public.alpha_ghl_delivery(
 request_id uuid PRIMARY KEY REFERENCES public.alpha_public_intake_requests(id) ON DELETE CASCADE,
 state text NOT NULL DEFAULT 'PENDING'
);
CREATE FUNCTION public.alpha_public_intake_make_receipt() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
 INSERT INTO public.alpha_public_intake_receipts(request_id,kind,notice_version)
 VALUES(NEW.id,NEW.kind,NEW.notice_version);
 RETURN NEW;
END;$$;
CREATE TRIGGER alpha_public_intake_receipt_on_insert AFTER INSERT ON public.alpha_public_intake_requests
 FOR EACH ROW EXECUTE FUNCTION public.alpha_public_intake_make_receipt();
CREATE FUNCTION public.alpha_ghl_delivery_on_intake() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.kind IN ('creator','advertiser') AND NEW.notice_version='AGM-INTEREST-2026-11-05-GHL'
 THEN INSERT INTO public.alpha_ghl_delivery(request_id) VALUES(NEW.id); END IF;
 RETURN NEW;
END;$$;
CREATE TRIGGER alpha_ghl_delivery_after_intake AFTER INSERT ON public.alpha_public_intake_requests
 FOR EACH ROW EXECUTE FUNCTION public.alpha_ghl_delivery_on_intake();
