# AGM-LAUNCH-015 Stage F — application-database visitor enrollment readiness

**Date:** 2026-10-15
**Tracker:** AGM-COMM-015-S06
**Scope:** Read-only repository engineering / EF integration checks. No real login or consent.

## Why Stage F is required
Merged Stages A–E proved synthetic claim validation and fixture-only atomic enrollment. The actual Wedding Planner session table, `WeddingPlannerPlanningSessions`, is owned by an advertiser (`AdvertiserId`) and contains **no verified visitor issuer/subject ownership record**. Existing staff-created Ask Alpha demonstration sessions and matching retry keys must not be promoted to real visitor ownership.

## Implemented
- `AskAlphaAuthoritativeEnrollmentPlanner` reads the actual app EF `BlissDbContext` workspace and session models with `AsNoTracking()`. It does not query text, export transcripts, add rows, save, commit, modify migrations or register hosted services.
- Stage D default-off issuer/role/subject claims readiness is mandatory before any datastore lookup. Invalid foreign/untrusted principals receive no workspace or session existence information.
- Validates bounded source slug, idempotency key, nonempty workspace ID, existing **ACTIVE** workspace, nonempty advertiser binding.
- Any pre-existing application session sharing an idempotency key yields `UNOWNED_SESSION_REPLAY_DENIED` regardless of whether it sits in the requested workspace or another tenant; it is never reclassified as a visitor-owned session.
- Otherwise returns `REAL_VISITOR_OWNERSHIP_SCHEMA_AND_SIGNED_IDP_MISSING` to expose the actual next blocker. No supported path returns an “authorized to create” decision or a session ID.

## Boundary guarantees
All responses have `VisitorOwnershipSchemaAvailable=false`, `NewVisitorSessionCreated=false`, `RealVisitorIdentityVerified=false`, `HistoricalSessionOwnershipVerified=false`, `VisitorConsentRecorded=false`, `TranscriptLinked=false`, `TranscriptExportAuthorized=false`, `PaymentAuthorized=false`, `CreatorPayoutAuthorized=false`, `CampaignDeliveryAuthorized=false`, and `Delivery=NOT_SENT`.

Unit/integration tests use the existing EF InMemory model with seeded fictional active/inactive workspaces and advertiser-created sessions. They verify positive read-only gap classification, denied anonymous/privileged principals, malformed scopes, foreign/missing/closed workspace, reused same-tenant and cross-tenant retry keys, unchanged advertiser ownership, no added/deleted/modified EF records and no new session.

## Proposed authoritative model for a future owner-approved production PR
*Not implemented in Stage F.* A future `AskAlphaVisitorSessionOwnership` entity should include distinct `WorkspaceId`, `SessionId`, `AdvertiserId`, `Issuer`, protected visitor subject binding, `ConsentStatus`, `CreatedAt`, `RevokedAt`, idempotency/scope version and unique immutable session ownership constraint. The exact data/privacy representation, RLS, archival and retention must be approved before code or migration. Session and ownership row must be inserted atomically in the authoritative PostgreSQL application database under a **cryptographically verified** hosted OIDC principal. Existing advertiser/staff sessions may not be adopted by matching UUID, slugs or retry keys.

## Remaining blockers
1. Real approved visitor identity provider with issuer/audience/signature/session validation — not done.
2. Approved visitor ownership schema and EF migration, tenant security policy and safe staging rollback — not done.
3. A real visitor-owned NEW session creation route with atomic insert, repeatable readbacks and rejection of historic conversation adoption — not done.
4. Informed, purpose/version-specific consent UI, withdrawal, privacy retention, audit and secure content storage — not done.
5. USD quote/FX, merchant and payees, Stripe test-mode evidence, creator rights, creative QA and Executive commercial acceptance — not done.

No production write, payment, payout, external contact, transcript export or delivery is authorized by this work. Independent review and four-workflow exact-head CI required before engineering merge.
