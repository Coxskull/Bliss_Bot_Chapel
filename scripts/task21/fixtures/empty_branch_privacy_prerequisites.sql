-- Disposable localhost PostgreSQL CI fixture ONLY; not a Supabase/production migration.
-- No staff identity, Auth account, operator grant, customer content or mail sender.
DO $fixture$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN
  CREATE ROLE anon NOLOGIN;
 END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN
  CREATE ROLE authenticated NOLOGIN;
 END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='service_role') THEN
  CREATE ROLE service_role NOLOGIN;
 END IF;
END $fixture$;
CREATE SCHEMA auth;
CREATE TABLE auth.users(id uuid PRIMARY KEY);
CREATE TABLE public.alpha_ask_alpha_settings(id integer PRIMARY KEY);
CREATE TABLE public.alpha_public_intake_requests(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 kind text NOT NULL CHECK(kind IN ('privacy','creator','advertiser')),
 processing_acknowledged boolean NOT NULL DEFAULT false,
 adult_confirmed boolean NOT NULL DEFAULT false,
 created_at timestamptz NOT NULL DEFAULT now()
);
-- The migrations must install all private support objects with this EMPTY
-- user/grant starting point, exactly like a Supabase branch with_data=false.
