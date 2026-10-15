# AGM-LAUNCH-015 Stage J — isolated visitor ownership/consent database candidate

**Evidence ID:** ALPHA-EV-20261015-S4G9LR
**Tracker:** AGM-COMM-015-S10; parent AGM-COMM-015 approved for engineering, not legally complete.
**Scope:** Disposable PostgreSQL staging proof only, no hosted EF migration.

## Repository-first findings
The actual Wedding Planner application stores `WeddingPlannerWorkspaces` and `WeddingPlannerPlanningSessions`, each with `AdvertiserId`. There is currently no accepted `VisitorIssuer` or `VisitorSubject` ownership link and no server-authoritative consent ledger connected to Stage I's API. The preceding Stage I consent POSTs correctly return HTTP 409 to avoid pretending the signed visitor/ownership infrastructure exists.

## Implemented
1. `Bliss.Infrastructure/Migrations/AskAlphaVisitorOwnershipStagingV1.sql` is an **unapplied candidate SQL file**, never a production EF migration. It creates `AskAlphaVisitorOwnershipStaging` and `AskAlphaVisitorConsentStaging` in the caller's disposable schema only. Ownership is linked via foreign keys to actual EF workspace/session/advertiser rows, with one owner per session and an issuer/subject-digest/request-key uniqueness scope.
2. A database trigger checks that the stored workspace, advertiser and planning-session relationship is exact and ACTIVE/OPEN, and requires a **synthetic** creation source marker. Historic staff-created `FICTIONAL_ASK_ALPHA_WAKE` sessions cannot be adopted through a matching UUID.
3. Consent events enforce composite ownership/workspace/issuer/subject-scope FK, fixed **staging-only** `CREATIVE_PREVIEW_ONLY` purpose and `STAGING-NOT-LEGAL-V1` notice, and sequential OPT_IN → REVOKE with an advisory transaction lock. Re-opt-in after revocation and duplicate actions fail closed. SQL UPDATE/DELETE/TRUNCATE of either table is rejected.
4. `Bliss.Tests.Persistence.AskAlphaVisitorOwnershipStagingPostgresTests` creates a random temporary schema and materializes the real EF model with `CreateTablesAsync`. It seeds purely synthetic advertiser, workspace, new visitor-labeled and staff-labeled sessions, applies the candidate SQL from the checkout, and tests valid transitions and denied invalid foreign/adoption/mutation paths. In a `finally` it drops the entire test schema; readback proves absence.
5. The isolated Wedding Planner PostgreSQL workflow runs these tests twice in `127.0.0.1/planner_lineage_ci`. Nothing connects to Supabase production, Railway or a real identity provider.

## Critical limitation
A source marker and a subject digest are **not proof** that the visitor authenticated or personally consented. No real OIDC issuer/client, user-facing legal consent notice, CSRF-tested production write route, retention/export policies, row-level security, immutable credential audit or production migration has been provisioned. Stage I HTTP POST still returns 409. Staging schema is a design and constraint verification only.

## Remaining parent work
- Owner/security select a real OIDC provider; verify token signature/issuer/audience/cookie integrity and real session owner creation.
- Review privacy/retention and consent text/version/purpose/recipients, tenant/RLS rules, reversible migrations and access controls before a production model.
- Add authenticated, server-issued NEW session and owner record in one atomic application DB transaction, then purpose-bound opt-in, revocation, subject-access governance and regression suite.
- Keep transcript export, marketing contact, payment/payout and campaign delivery denied until separate reviews.

CI completion proves schema guard code only. It is NOT real visitor ownership, acceptance of consent or a commercial launch permit.