# ALPHA Launch-Value Slice #330 — Private intake operator review queue

**Status:** Source-only candidate; NOT DEPLOYED; NOT PUBLICLY AUTHORIZED; NO ACCOUNTS OR INVITATIONS CREATED.

## What was actually observed

On November 7, 2026 PHT, a read-only aggregate inspection of the existing **Bliss Bot Supabase** project `bkutbglzivfdnoerigyb` reported 4 `public.alpha_public_intake_requests` (2 creator, 2 advertiser), 4 related `public.alpha_public_intake_receipts`, and 4 `public.alpha_ghl_delivery` rows marked DELIVERED; all 4 inquiries remain NOT_YET_VERIFIED. These counts demonstrate accepted lead intake, not 4 approved customers, invitations, campaign offers, legal rights or revenue. No names, emails, remote CRM contact IDs, private notes or record contents were retrieved.

Public customer inquiries originate at `frontend/public/join-creator.html` or `join-advertiser.html`, POST to the existing `alpha-public-intake` Edge Function, create a receipt and may be delivered to GoHighLevel server-side. The protected marketplace is separately hosted on Railway and remains in Restricted Preview. The ALPHA Factory uses a separate persistent staging project and its STOP is **not** an authorization shortcut for the Bliss lead queue.

## New, smallest safe code slice

- `supabase/functions/alpha-intake-operator-review/review-core.mjs`: a **pure** privacy-preserving queue projection. Each source request is linked to exactly one same-kind/same-notice receipt with chronological validity, current consent/age acknowledgement, unverified status and at most one current CRM-delivery status. Missing/mismatched/duplicated receipts are held. No marketing opt-in is needed to qualify for *manual* review. Every item always has `invitation_authorized=false`, `campaign_authorized=false`, `payment_authorized=false`.
- `supabase/functions/alpha-intake-operator-review/index.ts`: separate **GET-only** server-side operator endpoint candidate; it selects no name/email/contact message or CRM ID; no writes, contact sync, account creation, verification update or invitation. Request caps at 50. The endpoint is denied unless `ALPHA_INTAKE_REVIEW_ENABLED=true` and an exact allowed Origin, then validates the bearer with fresh Supabase Auth `getUser`, requires a server-owned operator `app_metadata` flag, an exact explicitly allowed user ID, signed JWT `aal2` and an independent live-session RPC readback. Absent config, unauthorized identity or missing RPC => fail closed. Origin/CORS alone never grants access.
- `docs/launch/candidates/NOT_FOR_APPLY-alpha-intake-operator-live-session.sql`: **unapplied security-sensitive candidate** for an exact, AAL2/verified-factor and non-revoked session check. It grants function execution to the service role only, never to anon/authenticated. This SQL requires separate security review, backup and isolated restore before it may be applied.
- `tests/alpha-intake-operator-review.test.mjs`: offline fictional-contract tests of provenance, duplicates, receipt mismatch, privacy, consent, CRM states, zero auto-approval, missing/expired session, mismatched ID, missing operator and AAL1 denial, and read-only source guard.
- `.github/workflows/alpha-intake-operator-review-ci.yml`: Node 20 source-only test on the changed files.

The stored request status remains NOT_YET_VERIFIED; the review status `REVIEW_READY` **only means ready for a human to examine**. It does not establish creator channel rights, advertiser legal identity, invite eligibility, identity verification, campaign approval or funds authorization.

## Strict deployment / future enablement gate

This draft PR **must not** auto-apply migrations, deploy Edge Functions, change protected Railway login, or create GoHighLevel contacts. Before any rollout, an independent reviewer must:

1. Verify that the physical backup for the **Bliss Bot Supabase** project (not merely separate ALPHA Factory staging) covers Auth and current intake schema, with zero-extra-cost isolated restoration proof.
2. Review the SQL candidate and test actual Auth session revocation, expiry, AAL1/AAL2, TOTP factor status, cross-user and concurrent revoke behavior, anonymous denial and absence of public grants. Supabase `auth.sessions` and `auth.mfa_factors` are not exposed to the browser.
3. Confirm the operating user actually belongs to Bliss Auth (not a different Supabase project). Admin must separately authorize verified operator `app_metadata.alpha_intake_operator=true`; neither an interest receipt nor marketing consent can create this flag. Record owner approval.
4. If all tests pass, deploy server-side with `ALPHA_INTAKE_REVIEW_ENABLED` **initially false**, `ALPHA_INTAKE_REVIEW_USER_IDS` exactly the approved Bliss Auth UUID(s), `ALPHA_INTAKE_REVIEW_ORIGINS` only the trusted operator origin, and Supabase credentials configured as private environment secrets. Never put `SUPABASE_SERVICE_ROLE_KEY` or the JWT in public code, logs or the issue.
5. Run supervised positive and negative tests from the exact deployed version (live current session, nonoperator, revoked session, AAL1, wrong Origin, unknown/expired account, duplicate source and malformed link). Observe only redacted counts and opaque IDs.
6. Maintain `Runtime__RestrictedPreview` and Factory STOP. Do not issue invitations or contact leads until the separate manual verification, legal/privacy, backup/restore and production Auth signoffs are accepted.

## How to run reproducible offline tests

```sh
node --test tests/alpha-intake-operator-review.test.mjs
```

**Hard separation of acceptance:**
- Source/test PASS, if CI succeeds: only the pure queue and static authorization contract is accepted.
- Hosted secure review: NOT TESTED/NOT DEPLOYED until explicit gates above.
- Full Task #3/#4 registration, Task #7 live security, Task #8 recovery and Task #23 paid sandbox campaign: still OPEN.
- Formal launch remains **NO-GO**, with a measurable new customer-onboarding engineering increment.

**Issue:** https://github.com/Coxskull/Bliss_Bot_Chapel/issues/330  
**Backup/readiness dependency:** https://github.com/Coxskull/Bliss_Bot_Chapel/issues/302  
**ALPHA separate actor-rights/security dependency:** https://github.com/Alpha-Galaxy/alpha-shipping-yard-test/issues/216
