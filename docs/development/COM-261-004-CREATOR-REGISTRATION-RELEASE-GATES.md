# COM-261-004 — Creator registration acceptance boundaries

**2026-11-05 | Task 4 of PRE-LAUNCH MASTER 20261105 | PREVIEW ONLY / PUBLIC PENDING**

## Existing proof, not duplicated

The existing ALPHA Factory staging acceptance in Shipping Yard Issue #93
(https://github.com/Alpha-Galaxy/alpha-shipping-yard-test/issues/93#issuecomment-6646491056)
documents a signed recent owner MFA synthetic **CREATOR HTTP 201**,
**ADVERTISER HTTP 201**, idempotent creator replay **HTTP 200** and
two database-linked intake/consent receipts independently read from staging.
The factory STOP remained engaged and the temporary privilege was revoked.
Those receipts are **not** evidence that a live Bliss Bot customer can register.

## New source work

- Existing Bliss Bot repository: `frontend/public/creator-registration-preview.html`.
- A separate, entirely **fictional** local-only preview of creator fields and
  required acknowledgement; marketing preference is optional and independent.
- JS validation denies missing acknowledgement, missing/real name, ordinary
  email, non-fictional channel URLs, ordinary markets and unsupported categories.
- No fetch, form POST, cookies/storage, token request or database writes.
- The existing Vercel portal builder copies an explicit allowlist; this file is
  **not** copied to `.vercel-portal` and is not a public registration entrypoint.
- Railway's `Runtime__RestrictedPreview` must stay enabled. Do not add
  this preview to the public landing page, change environment settings, merge
  or deploy on the strength of this work alone.
- Run static/unit checks:
  `node scripts/creator-registration-preview-smoke.cjs`.

## Production blockers and future integration contract

1. Approve and publish canonical privacy/terms notice, accountable controller
   contact, purpose, retention, deletion/withdrawal rights, processors and
   immutable consent version. A preview checkbox is **not** consent.
2. Confirm the **actual Bliss Bot Railway PostgreSQL** backup/isolated restore
   and migration history, rather than substituting Alpha Factory staging or
   the separate Bliss Supabase project. Confirm real OIDC roles, trusted
   HTTPS ingress, same-origin session, CSRF and tenant boundaries. Issue #302.
3. Design a dedicated **public creator intake route** guarded by server-side
   validation, request identity/idempotency, abuse limits, disclosure and
   processing acknowledgement. Never expose or proxy the factory's privileged
   `/internal/admin/staging-intake` endpoint or its recent-MFA JWT.
4. On the approved authoritative service, transactionally persist the creator
   and server-recorded consent purpose/version/timestamp, and return only an
   opaque receipt/record ID. Require independent readback and 1 canonical
   record on replay; no PII in public logs or receipt URLs.
5. Run anonymous/unauthorized denial, unchecked consent, malformed/oversized
   fields, duplicate/idempotency, concurrent retries, rate-limit, RLS,
   mobile/accessibility and outage tests in authorized isolated staging.
6. Compare exact deployed source, complete production backup/rollback rehearsals,
   receive required owner/legal/security approval, then conduct a controlled
   public release. HighLevel may be a separately authorized downstream CRM,
   **not** the Bliss Bot frontend replacement.

**Decision:** Task 4 remains **STAGING PASSED — PUBLIC PENDING**.
Do not mark task complete or solicit real customer data from this preview.
