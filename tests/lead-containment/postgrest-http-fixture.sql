-- ONLY a disposable localhost PostgreSQL 17 GitHub Actions service.
-- Synthetic schemas, no real lead data, credentials, or Supabase access.
CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
CREATE ROLE authenticator LOGIN NOINHERIT PASSWORD 'ci-postgrest-only-ephemeral';
GRANT anon TO authenticator;
GRANT USAGE ON SCHEMA public TO anon;

CREATE TABLE public.advertiser_inquiries (id integer PRIMARY KEY);
CREATE TABLE public.contact_inquiries (id integer PRIMARY KEY);
CREATE TABLE public.creator_applications (id integer PRIMARY KEY);
CREATE VIEW public.alpha_lead_summary
WITH (security_invoker=true)
AS SELECT id FROM public.advertiser_inquiries WHERE false;

ALTER TABLE public.advertiser_inquiries ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.contact_inquiries ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.creator_applications ENABLE ROW LEVEL SECURITY;

CREATE POLICY lead_read_block ON public.advertiser_inquiries
  AS RESTRICTIVE FOR SELECT TO anon, authenticated USING (false);
CREATE POLICY contact_read_block ON public.contact_inquiries
  AS RESTRICTIVE FOR SELECT TO anon, authenticated USING (false);
CREATE POLICY creator_read_block ON public.creator_applications
  AS RESTRICTIVE FOR SELECT TO anon, authenticated USING (false);

REVOKE ALL ON public.advertiser_inquiries, public.contact_inquiries,
  public.creator_applications, public.alpha_lead_summary
  FROM PUBLIC, anon, authenticated;

-- Positive-control route demonstrates PostgREST is live and the role works;
-- distinct from sensitive/revoked tables. No data is ever inserted.
CREATE TABLE public.ci_public_control (id integer PRIMARY KEY);
GRANT SELECT ON public.ci_public_control TO anon;

DO $$
DECLARE obj text;
BEGIN
  FOREACH obj IN ARRAY ARRAY[
    'advertiser_inquiries','contact_inquiries',
    'creator_applications','alpha_lead_summary'
  ] LOOP
    IF has_table_privilege('anon',format('public.%I',obj),'SELECT') THEN
      RAISE EXCEPTION 'Unsafe fixture: anon has SELECT on %',obj;
    END IF;
  END LOOP;
  IF NOT has_table_privilege('anon','public.ci_public_control','SELECT') THEN
    RAISE EXCEPTION 'Fixture positive control not readable';
  END IF;
END $$;
