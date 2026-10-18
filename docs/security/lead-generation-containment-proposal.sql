-- REVIEW ONLY. Not an applied migration. Target: yyesqcyjhqjryaskrqip.
-- Requires owner approval: direct anon/authenticated clients will stop working.
-- Existing service_role grants/bypass are preserved, not newly granted.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public.creator_applications ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.advertiser_inquiries ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.contact_inquiries ENABLE ROW LEVEL SECURITY;

-- RESTRICTIVE denial also prevents a future permissive policy from reopening
-- public access accidentally. These policies do not authorize an application user.
CREATE POLICY lead_incident_public_deny ON public.creator_applications
  AS RESTRICTIVE FOR ALL TO anon, authenticated USING (false) WITH CHECK (false);
CREATE POLICY lead_incident_public_deny ON public.advertiser_inquiries
  AS RESTRICTIVE FOR ALL TO anon, authenticated USING (false) WITH CHECK (false);
CREATE POLICY lead_incident_public_deny ON public.contact_inquiries
  AS RESTRICTIVE FOR ALL TO anon, authenticated USING (false) WITH CHECK (false);

REVOKE ALL PRIVILEGES ON TABLE public.creator_applications,
  public.advertiser_inquiries, public.contact_inquiries, public.alpha_lead_summary
  FROM PUBLIC, anon, authenticated;
-- No DROP, DELETE, TRUNCATE, new role, forced-RLS or service_role grant.
COMMIT;
