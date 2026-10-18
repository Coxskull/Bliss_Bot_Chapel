# Alpha Lead Generation: P0 investigation and containment proposal

Evidence: ALPHA-EV-20261018-S7R2VN. Inspected 2026-10-18 05:19-05:23 UTC.
Target confirmed by connected project inventory: **yyesqcyjhqjryaskrqip**,
Alpha Lead Generation, ACTIVE_HEALTHY, organization nqugooctebekalqhbpek.
The supplied yyesqcvjhqirvaskrqip was different and returned permission errors.
This evidence concerns only the confirmed named project; not the Alpha/Bliss database.

## Live before state: FAIL / active exposure

Security Advisor confirms rls_disabled_in_public on public.creator_applications,
public.advertiser_inquiries and public.contact_inquiries. All three have RLS and
forced RLS false, owner postgres, zero policies. anon and authenticated have schema
USAGE and table SELECT, INSERT, UPDATE, DELETE, with no BYPASSRLS. service_role has
the same operations and BYPASSRLS. No explicit column ACLs were found on these
tables or the summary view.

A read-only transaction SET LOCAL ROLE anon successfully executed EXISTS against
all three tables; every result was true. Only row-existence booleans were returned,
not customer fields. No live INSERT/UPDATE/DELETE was executed. Those permissions
are confirmed by PostgreSQL effective privilege inspection, not a mutating test.

public.alpha_lead_summary is also SELECT-granted to anon/authenticated. It is already
security_invoker=true, but currently exposes the underlying unprotected tables'
email and display-name projections. Table schemas contain email, telephone, personal
names, messages, advertiser campaign information, creator details and JSON payloads.
No actual customer values were read/exported. Potential scope is all accessible rows;
there is no evidence establishing actual unauthorized acquisition or modification.

## Dependency and incident review: incomplete

No Edge Functions are deployed in this project. Local source and scoped Coxskull
GitHub searches for the actual project reference/table names returned no matching
application source. The table naming is not proof of GHL synchronization. Current
frontend/n8n/other API clients and their credentials/roles are not established.

Read-only log aggregates over the provider's available last-24-hour window showed
pooler, PostgreSQL and one PostgREST log event. Available aggregates do not provide
an attributable customer-data access history. pg_stat_statements contains anon
queries, including investigator-originated checks; it is not incident attribution.
No conclusion of compromise or absence of compromise is justified. Retain provider
logs and inspect the responsible integration's role/configuration without exporting
payloads, customer identifiers, raw query text, tokens or request headers.

## Proposed containment: REVIEW ONLY, not deployed

lead-generation-containment-proposal.sql enables RLS on the three specific tables,
adds restrictive deny policies for anon/authenticated, and revokes their/PUBLIC's
object privileges on those tables plus the summary view. No table/data deletion,
new users, service-role grant or ownership change. Existing service-role access
is preserved. This is backend-only emergency containment, not a tenant-access design.

**Impact requiring owner approval:** browser/public-role inquiry clients or dashboards
will be blocked. If such clients are needed, pause their intake during containment
and prepare a reviewed server-side validation/anti-abuse path. Do not restore public
SELECT, expose a service-role key to the browser, or invent auth.uid ownership fields
that the present schema lacks. Backend service-role integration use must be confirmed
before claiming continuity. Valid application user policies require the actual app
access model and identities, not guessed permissions.

## Isolated before/after tests: PASS, 70 checks

Executed scripts/test-lead-containment.mjs against @electric-sql/pglite **0.5.10**
(PostgreSQL/WASM) installed only in temporary engineering storage. The synthetic
fixture reproduces the three table names, a security-invoker UNION view, broad
public grants and a BYPASSRLS service role. It uses simplified synthetic columns;
it is not a full production-schema or hosted integration test.

- Before: both public roles read synthetic records from three tables and view (8 checks).
- After applying the exact proposal to the fixture: both roles denied table CRUD
  and view SELECT (26 checks).
- After a simulated accidental re-GRANT: RLS hides rows, denies insert, and update/
  delete return no records for both roles (24 checks); grants then revoked again.
- Existing service role synthetic SELECT/INSERT/UPDATE/DELETE succeeds (12 checks).

Production AFTER tests: **NOT RUN**; authorization pending. Hosted API and existing
application integration continuity: **BLOCKED** pending dependency identification
and approved containment. Latest live Advisor readback still reports all three RLS
errors because no production correction was made.

Reproduction in a disposable checkout: install @electric-sql/pglite@0.5.10 into an
isolated temporary npm prefix, make its node_modules available to the test's import,
then run `node scripts/test-lead-containment.mjs`. No production credentials are used.

## Additional Advisor notices, separate follow-up

set_updated_at has mutable search_path; its observed body is an ordinary invoker
trigger assigning now(). rls_auto_enable is SECURITY DEFINER and EXECUTE-granted to
public roles, but actually returns **event_trigger**. Do not infer from the generic
Advisor RPC wording that it is callable as an ordinary PostgREST function. Review
event-trigger wiring and revoke unnecessary grants separately without breaking it.
These notices were not silently bundled into the table-containment proposal.

## Approval and post-change acceptance

1. Owner/security reviewer approves the exact SQL and potential public-intake pause.
2. Confirm integration caller roles and preserve configuration/log evidence securely.
3. Convert proposal into the responsible project's reviewed migration/deployment path;
   apply only to confirmed project, with preflight drift review and lock limits.
4. Recheck effective table/column/view permissions, policies and RLS, test anonymous
   HTTP denial without customer payloads, and repeat approved backend synthetic CRUD.
5. Confirm GHL/n8n/other identified integrations with synthetic internal data, then
   rerun Security Advisor. Do not claim resolution until all three errors clear.

Advisor reference:
https://supabase.com/docs/guides/database/database-linter?lint=0013_rls_disabled_in_public
