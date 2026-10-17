# AGM-COMM-015 — Trusted database-role tenant boundary (non-active candidate)

**Status:** Repository-only design and disposable PostgreSQL evidence. No approved IdP, application wiring, EF migration, production policy or commercial activation.

## Why previous GUC filtering is not authority

PR #263 demonstrated default-deny RLS using `agm.fixture_workspace_id`, `agm.fixture_issuer` and `agm.fixture_subject_sha256`. Those custom settings may be controlled by a SQL caller. They **must not** be treated as verified tenant identity, consent, or a basis for production RLS.

This candidate proves a different, narrower property: a PostgreSQL connection authenticated as a restricted role cannot read rows owned by another role's fictional tenant, even when it submits another tenant's workspace/subject through arbitrary SQL session variables.

## Trust boundary that MUST be approved before implementation

1. The provider's real signed tokens must be validated by a configured OIDC handler (issuer, audience, signature, expiration, nonce/callback and revocation/session policy), not by string-matching a claim's Issuer field.
2. The application server must resolve server-issued visitor-session ownership to a tenant/workspace using authoritative transactional storage, without honoring caller-chosen session IDs or tenant IDs.
3. A trusted application-side database access layer must select credentials/roles or another review-approved row context only after those checks. Untrusted clients must never connect directly with role-switching capability or an elevated bypass-RLS role.
4. DB credentials must be managed as secrets and rotated. The app role must not own the protected tables, possess BYPASSRLS, grant itself roles, or inherit a sibling tenant's role.
5. Exact-scope RLS, least-privilege operations, cross-tenant negative tests and rollback must pass with a **real staging** issuer and app authorization context.
6. Consent requires legally approved notice, separately authenticated, purpose/version scoped opt-in and withdrawal with CSRF, audit, retention and revocation propagation. No caller-provided "consent=true" is accepted as authority.

## Disposable proof (the test in this PR)

- Random local `agm_j_<uuid>` schema, two fictional advertiser/workspace/visitor-ownership records and matching synthetic consent entries.
- Two **distinct PostgreSQL LOGIN NOINHERIT roles** with random ephemeral test passwords, each granted USAGE on only the test schema and SELECT on the two staging tables.
- Four RLS SELECT policies scoped by the **database-authenticated role** (one ownership and one consent policy per tenant), not by mutable GUCs. No write privileges, no shared membership, no SELECT of Wedding Planner's advertiser/session tables.
- Verified login to PostgreSQL with each role, tenant-scoped and cross-tenant reads, forged `agm.fixture_*` variables not changing row visibility, rejected `SET ROLE` escalation (`42501`), and full test-policy rollback preserving fixture rows.
- Unique schema and roles disposed after each test, with no hosted DB, production connection, EF migration, API route, transcript read/export, or live consent.

### Limitations

A separate DB login per tenant is a *mechanical trust-boundary demonstration*, not a scalable reviewed production credential architecture. Production may instead require approved service-role segmentation, hardened connection management, trusted RLS session binding, or server-authorized data-access functions. The right choice is an explicit security/data-owner decision. No result here should be cited as hosted acceptance.

## Non-authority gates

`IDP-01`, `SESS-01`, `OWN-01`, `PRIV-01`, `CONS-01`, `HOST-01`, `COMM-01` remain BLOCKED. Public consent POST retains its prior HTTP 409 denial. `CampaignReady=false`, `Delivery=NOT_SENT`. No real visitor enrollment, payments, payout, transcript export, or advertising send.
