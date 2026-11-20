# Task #3B — current release disposition (2026-11-20)

## November 20 — Owner-directed account notice publication lane (supersedes historical draft status below)

The owner instructed **approved / proceed** for Task #3B launch work. A new, accurately limited advertiser account-specific public notice and application terms have been authored with effective date **November 20, 2026**, and new IDs advertiser-privacy-2026-11-20-v2 and advertiser-terms-2026-11-20-v2. The browser's required consent text and payload and the server's immutable registration receipt version checks now use exactly these IDs; no earlier draft consent is silently accepted as the new version.

The notice accurately describes Supabase Auth, Railway PostgreSQL, optional Vercel informational delivery, separate public inquiry/CRM handling, the ALPHA GLOBAL MEDIA LLC designated account privacy contact alpha7auto@gmail.com, human review of access/correction/deletion/marketing withdrawal, no uniform global response time, and **no claimed automated deletion of immutable receipts or backups**. The terms make clear a free PENDING_REVIEW application never grants marketplace access, creator placement or any payment. Publication is a disclosure/versioning change, **not** completed hosted account privacy handling, verified company-mailbox delivery, legal certification, or fully effective privacy-request fulfillment.

The two account notice pages become eligible for public **read-only** Vercel publication. Protected account-registration and account-rights form pages remain excluded from Vercel bundles; real login callbacks, sessions and CSRF belong on the Railway origin. Historical PR #423 draft documents remain dated evidence and are not silently rewritten.

**Separate launch gate not waived:** Owner's prior DigitalOcean recovery is the separate DR362 Factory/Supabase scope, not a verified restore receipt for the Bliss Railway PostgreSQL 18 application database. No actual application migration or customer registration was performed in this branch. Runtime:RestrictedPreview and registration remain OFF. Publishing notices and binding IDs do not automatically authorize live accounts.

---



**Scope:** The files alongside this note are original existing PR #423 **legal engineering candidates**, copied unchanged into the CURRENT Bliss source so reviewers do not need a divergent historical PR. They are NOT effective notices, contracts, a signature, or an authorization to enable account creation.

## Current source and operations

- Existing advertiser registration, identity binding, request-key idempotency, terms/consent receipt, optional-marketing choice and PENDING_REVIEW account protections are already implemented in current `main`. Do not duplicate those components or overwrite `frontend/public/advertiser-account-privacy.html`, which includes the newer account-rights link.
- Owner stated on 2026-11-20: **DigitalOcean recovery/backup work is complete**. Record as owner-reported completion; this update did not inspect custody, restore logs or source-to-target data parity. Do not require the owner to repeat completed DR tasks simply to continue source/isolated registration work.
- Current Railway Bliss deployment `6980ef76-64e3-46ca-9d10-c2af7a3a9116` is SUCCESS on current source `97105c370eae9403f68cc7978c2d6cd5c0a25dc8`. It remains `Runtime__RestrictedPreview=true`, advertiser registration OFF; live receipts and effective account notices are NOT_VERIFIED. The isolated `alpha-task3b-staging` service exists but its 2026-11-18 deployment is SLEEPING and it has no attached PostgreSQL volume. These facts must not be misread as a live signup acceptance.
- **Effective legal version:** not yet selected. Source UI/API currently binds `advertiser-privacy-2026-11-07-v1` and `advertiser-terms-2026-11-07-v1` as unapproved draft identifiers. PR #423 v2 candidates are NOT replacements until formally effective-dated, matched to browser/API/receipt and truly published. Owner's international-market and no paid outside-counsel decisions are already settled.
- Account-specific privacy requests and existing single-operator procedures are separate from the public inquiry form. Do not direct account erasure/marketing withdrawal through the lead-intake service or claim an automated final fulfillment that has not been performed.

## Next accepted engineering work, with no fabricated status

1. Reuse an owner-approved isolated Railway API + PostgreSQL 18 + effective Supabase OIDC acceptance target; do not apply a destructive or unreviewed migration to production or repurpose the unrelated Factory/DR362 recovery vault. The isolated target must have proven recoverability according to the **existing** owner-completed DigitalOcean recovery procedure.
2. Test the EXISTING registration implementation against a real verified hosted identity for: authenticated pending registration 201, exact-key/consent replay 200, different-payload conflict 409, unverified/forged JWT denial, CSRF protection, cross-tenant denied access, optional-marketing independence and disabled-by-default boundary. Only issue opaque case/receipt IDs in sanitized evidence.
3. Reconcile the actual legal effective date, retention/erasure/withdrawal handling and privacy contact's delivery/ACK with hosted behavior. Publish only accurate notices; choose one version and ensure browser/server/DB receipt parity.
4. Only after hosted receipts and effective notices, approve a bounded public registration release. Do not infer Factory STOP release, AI generation charges, customer payment activation or automated marketing from this source-document integration.

This note supersedes stale operational-status summaries in the historical 2026-11-15/16 owner packet; it does NOT rewrite the historical contemporaneous evidence.
