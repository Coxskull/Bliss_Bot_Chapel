# Alpha Lead Generation — public REST denial acceptance (owner-triggered)

**Safety classification:** READ-ONLY, ZERO ROWS, NO AUTHENTICATION SESSION, NO WRITES. This probe is *not* run against production by GitHub CI.

Use only the connected **Alpha Lead Generation** project, reference `yyesqcyjhqjryaskrqip`. The earlier user-supplied string differs and must not be used. **Do not use Alpha Auto/Bliss Supabase projects.**

## Why this exists

PostgreSQL metadata acceptance confirms four existing objects have RLS and revoked `anon` / `authenticated` SELECT grants, but does not demonstrate how the external Data API behaves with a valid *public publishable* key. Supabase distinguishes grants from RLS ([official Data API security docs](https://supabase.com/docs/guides/api/securing-your-api)). A missing table grant should yield SQLSTATE `42501` at the REST boundary. Merely getting `401 Missing API key` does not prove object-level denial.

## Owner-approved run

Only run when an ALPHA owner explicitly approves a small number of external public GET requests. Do not add this command to an automatic scheduled workflow. Do not paste keys, JWTs, raw response bodies, customer data or tokens into a chat, issue, CI log, or PDF.

From a trusted local shell with Node 22+ and the environment set privately:

```bash
export ALPHA_LEADS_SUPABASE_ORIGIN='https://yyesqcyjhqjryaskrqip.supabase.co'
# Enter an existing sb_publishable_ value privately, never echo it:
read -r -s -p 'Existing public publishable key: ' ALPHA_LEADS_PUBLISHABLE_KEY
export ALPHA_LEADS_PUBLISHABLE_KEY
printf '\n'
node scripts/lead-public-rest-denial-probe.mjs
unset ALPHA_LEADS_PUBLISHABLE_KEY
```

The probe refuses secret/service-role keys, JWTs and any alternate origin. It issues exactly four `GET /rest/v1/<object>?select=*&limit=0` requests: `advertiser_inquiries`, `contact_inquiries`, `creator_applications`, and `alpha_lead_summary`. The query returns zero rows **when the endpoint behaves as expected**; successful 2xx is always marked FAIL, without reading its body. No form submission or database modification occurs. Redirects, unexpected response codes, invalid keys or non-PostgREST errors are not counted as a pass.

**Acceptance:** all four objects must return HTTP 401/403 **with PostgREST PostgreSQL error code `42501`**, indicating the request reached permission evaluation and was denied. The script logs only object identifiers, HTTP status and SQLSTATE. A generic gateway `401` (invalid or absent key) is **INCONCLUSIVE**, not PASS. Any `2xx` is **FAIL / investigate exposure immediately**; do not dump the response.

## Offline tests (automatically in PR CI)

```bash
node --test scripts/lead-public-rest-denial-probe.test.mjs
```

Synthetic mocked HTTP responses test exact origin / routes, four GET requests, `limit=0`, no bearer authorization, no redirects, 401/403 SQLSTATE handling, fail-closed 2xx and non-42501, no reading success bodies, and key restrictions. **No network and no Supabase keys are used in these tests.**

## What a PASS does *not* close

- Signed-in `authenticated` role permissions (requires explicit owner-approved nonprivileged test user/JWT).
- Backend/service-role insert/update paths and GHL/n8n lead-intake continuity.
- RLS policy and column-level grant regression (use the existing four-row SQL catalog verifier separately).
- Historical exposure, log-retention completeness, or previous unauthorized access.
- Separate function-security warnings on `public.rls_auto_enable()` and `public.set_updated_at()`; those remain pending owner-approved migration.

If any result fails, preserve only the sanitized status and error code, pause further tests, and request an incident-impact/security review. Never relax RLS, issue `GRANT`, or create users just to make a test pass.
