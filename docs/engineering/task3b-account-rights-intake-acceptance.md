# Task #3B — advertiser account-rights case intake acceptance

**Owner directive:** ALPHA-P0-TASK3B-FINAL-RESOLUTION-20261114
**Status:** source implementation candidate only. Do not deploy to production or treat as live account support.

## Existing systems reused
- Supabase OIDC and exact pending-registration marker, or separately verified approved advertiser role/tenant; no public inquiry email-to-identity mapping.
- Existing ASP.NET anti-forgery middleware and subject-partitioned rate limits.
- Existing Bliss EF Core/PostgreSQL model, migration and transactional receipt conventions.
- Existing global STOP and Restricted Preview remain unchanged.

## New endpoints and authorization
| Endpoint | Auth/condition | Result |
|---|---|---|
| POST /api/advertisers/privacy-requests | Verified pending applicant or approved advertiser with matching membership | 201 receipt; same key and identical scope 200; conflicting key 409 |
| GET /api/advertisers/privacy-requests | Same issuer+membership boundary | Own 25 latest cases only |
| GET /api/advertisers/privacy-requests/operator-queue | Issuer-bound staff role AND `Authentication:ApprovedPrivacyOperatorSubjects` allowlist | 50 oldest open case records for authorized operator only |
| POST /api/advertisers/privacy-requests/{id}/acknowledge | Same explicit operator role+allowlist, CSRF and rate limit | Transitions RECEIVED → VERIFYING and stores operator subject/time. No erasure or withdrawal. |

**Default-closed:** `Authentication:AdvertiserRegistrationRequested=false`, `Authentication:AdvertiserPrivacyRequestsRequested=false`, and `Authentication:ApprovedPrivacyOperatorSubjects=[]`. Account-case access is now independent of the signup switch. No operator access, live signups, payments, customer notifications or data-processor action is enabled by this source patch.

Request types are a fixed allowlist; no free text, password, email, IP or identification number is stored in the case. Request keys are unique; one subject cannot replay another subject's receipt. PostgreSQL public/anon/authenticated table access revoked.

## Source/isolated CI criteria

1. .NET 8 restore + build, targeted legal notice contract and source regression, isolated PostgreSQL 16 migration (including this new table), fake users A/B.
2. POST 201/200/409; altered-key and cross-tenant rejection; unauthorized 403; invalid types/empty keys 400.
3. GET own only; pending boundary denies any operator/internal route.
4. Operator queue default 403; forged role issuer, missing approved subject, or wrong role 403. Explicit fake owner/operator authenticated can view cases and ACK with recorded subject/time; repeat ACK conflicts.
5. Registration receipts remain immutable; submitting ERASURE/MARKETING_WITHDRAWAL does not auto-delete or change consent, and a case remains RECEIVED until authorized acknowledgment.
6. Migration rollback in disposable instance; verify no public grants and no production writes.

## Unproven hosted/operational acceptance

- Effective Railway API database target and independently recoverable backup are not verified; existing `bliss-bot` project has production only. Do not use it as test target.
- No authorized isolated Auth/OIDC/PG application setup, email verification, real consent version binding, deadlines or operator procedure is accepted.
- Human case handling beyond an ACK (identity verification, lawful fulfillment, processor deletions, consent-suppression propagation, notices and closure) is not implemented in this patch.
- UI is explicitly noindex and disabled unless authenticated/feature-enabled, and marked engineering preview; the account Privacy Notice remains a draft.
- Commercial activation, Global STOP, payment, outreach and registration flags remain untouched.

**Disposition:** mark only the exact independently verified source/CI milestones COMPLETE; keep Task #3B and documentation publication OPEN until full hosted and legal-policy acceptance.

## November 15 source correctness fixes (not hosted proof)

- Existing approved self-service advertiser middleware denied the exact new privacy-request route; it now permits GET/POST **only** on `/api/advertisers/privacy-requests` and **only** when the separate privacy feature switch is enabled. It continues to deny the queue, acknowledgment, nested tenant paths, all other methods and legacy marketplace endpoints. Controller subject/active membership checks remain mandatory.
- The client registration and privacy-case scripts incorrectly sent `RequestVerificationToken` while the existing ASP.NET antiforgery middleware configured `X-CSRF-TOKEN`. Both scripts now send the exact existing configured `X-CSRF-TOKEN`; the source contract test asserts parity.
- Preview config now separately exposes `advertiserPrivacyRequestsEnabled` only when authentication, Supabase and the privacy-request feature have been explicitly enabled; the rights page no longer relies on `advertiserRegistrationEnabled`.
- New .NET and Python regressions assert default-off and exact-route boundaries, including approved self-service advertiser sessions, and feature flag refusal. This remains *source/CI* verification; it is not hosted identity, human response or live erasure acceptance.

## Current hosting inventory — read-only November 15 verification

- Railway `bliss-bot` project `fb33c1b7-03fb-42c8-89b1-081951a738fd`: only environment `production` (`b67a9836-36bf-4083-93f7-00c867b13f39`), with the Bliss app on `main` and a production PostgreSQL service and volumes. Do NOT reuse this for isolated rights acceptance.
- Supabase `bkutbglzivfdnoerigyb` (Bliss Bot): only its default main PG17 branch, **not** an isolated PG18 staging backend or a verified active registration OIDC issuer. The owner has **already verified** the effective Railway app's `RAILWAY_INTERNAL/BLISS` target class and PG18 TLS connection. Its public application table count remains **zero**, with no EF migration history; no protected data may be copied or migrated. Do not repeat the owner's completed private host-classification and TLS/catalog probes.
- Railway Factory staging belongs to a different application and must not be repurposed; other Alpha backend projects are also unrelated. The DR362 recovery project is isolated for **disaster recovery**, not advertiser registration testing.
- **November 16 scoped improvement:** The existing ALPHA Neon **Free** organization now contains a dedicated new Task3B **PostgreSQL 18** staging project `alpha-task3b-pg18-staging` (`autumn-grass-58047756`), default `staging` branch (`br-noisy-forest-aofn7lez`), database `bliss_task3b`, fixed 0.25 CU, `block_public_connections=true`, no real customer data. Provider control plane reports the branch ready, but an attempted read-only SQL connection was rejected by the deliberate network block; therefore no hosted SQL/EF migration PASS. This does **not** supply an ASP.NET API instance, a proven Supabase OIDC issuer, independent current Railway production backup, or full hosted registration/rights acceptance. The existing private empty Railway project `alpha-task3b-staging` (`43062727-bca5-400a-b801-27828161324b`) is likewise still undeployed due to the absent isolated hard billing limit. No separate Task22/DR361/Factory/DR362 resource was repurposed.
- The source unit/integration tests are intentionally isolated, synthetic and disposable; they do not replace the required deployed OIDC, HTTPS ingress, case workflow, backup and rollback receipt.

**CI note:** The repository's existing .NET and PostgreSQL workflows run for pull requests to `main`. PR #424 is temporarily pointed to `main` only for exact-head CI after the source repairs; return it to its stacked parent PR #349 for engineering review when CI is accepted. No merge or deploy is authorized.
