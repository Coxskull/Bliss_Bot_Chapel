# Task #04 — secure creator registration implementation

## Implemented on an isolated branch

- Reuses the Task #03 Supabase OIDC identity verification and creates a separate creator-registration-only marker. Applicants require a verified email, have no issuer-stamped role or tenant ID, and can reach only session/login/config/logout plus creator registration/status (and advertiser registration only when both registration features are explicitly enabled).
- Adds a CSRF-protected `POST /api/creators/register` that creates an `UNVERIFIED` Creator, a subject-bound `PENDING_REVIEW` membership, and an immutable, versioned consent receipt in one database transaction.
- Privacy processing and terms acceptance are required. Marketing permission is an independent optional field. The receipt records the applicant subject, creator ID, request digest, consent versions and choices, and creation time; it does not store email/password.
- A random request key is exact-payload idempotent. Changed-content retries, reused keys from other subjects and duplicate subject registrations return `409`. Database uniqueness constraints backstop concurrent requests.
- `GET /api/creators/registration` returns only the caller's own registration/receipt identifiers and status. Approved creator account access continues to use the existing issuer-bound, approved-subject path and only a matching `VERIFIED` Creator row. The pre-existing creator API boundary blocks shared-directory, cross-tenant and campaign access.
- Shared creator directory and detail reads filter out `UNVERIFIED` registrations, so applicants are not exposed to other authenticated marketplace users before review.
- The registration page is served from the Railway application origin so cookie, CSRF and API requests stay same-origin. The Vercel portal receives only a link to Railway; it does not publish the password form or registration JavaScript. Password signup goes directly from the browser to Supabase Auth with its publishable key.

## Verification and release boundary

- New focused tests cover issuer-bound registration claims, role/tenant stripping, dual registration scope, route denial, database uniqueness, consent persistence, append-only receipts, exact retries, duplicate/cross-subject denial and own-subject status readback against disposable PostgreSQL.
- Existing Task #04 creator inquiry, CRM and dashboard boundaries remain in the same branch; the creator inquiry is not treated as an account.
- No hosted Auth user or invite was created, no hosted DB migration or write was made, and no deployment, merge, campaign, outreach, payment or STOP change occurred.
- Feature flags default off. Exact-head CI, hosted email-confirmation/redirect, staged migration and RLS/DB-privilege readback, independent identity review, real account/tenant security acceptance, Task #1 recovery, Task #7 security, Task #8 ingress/rollback and legal/privacy approval remain release gates. The privacy/terms version identifiers in this implementation are draft identifiers until legal review accepts the text.
- Passing CI would prove source and disposable-fixture behavior only. It would not prove a hosted creator registration or creator login.
