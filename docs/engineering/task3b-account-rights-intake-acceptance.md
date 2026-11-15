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

**Default-closed:** `Authentication:AdvertiserRegistrationRequested=false` and `Authentication:ApprovedPrivacyOperatorSubjects=[]`. No operator access, live signups, payments, customer notifications or data-processor action is enabled by this source patch.

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
