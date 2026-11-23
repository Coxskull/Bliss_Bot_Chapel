# Task21 — Production manual operator gateway release

**November 12, 2026 (Asia/Manila)**

Scope: existing Bliss Bot Supabase production project `bkutbglzivfdnoerigyb` and existing owner-designated ONE-human support operating policy. This work is additive to the live private Gmail metadata-only support registry; it does NOT alter the public website forms, sensitive staging support system, OpenAI paid AI, payment/refund logic or factory STOP.

## Deployed database work

Migration `task21_production_locked_single_operator_20261112` creates an explicitly empty `alpha_support_private.manual_operator_grants` table, an exclusive claims table, and immutable actor claim events. A newly created real authenticated human account does not automatically get a grant. Anonymous and authenticated roles have NO private-schema access and NO execute permission on privileged public procedures. Only a trusted, server-side service role may call the procedures, and every procedure verifies an existing, enabled grant for the exact actor. Supported actions are a metadata-only queue and `OPEN` → `IN_REVIEW` atomic claim; there is **no** function to approve/issue refunds, send email, respond to customers, close cases, or bypass human consent.

No synthetic or customer records were created; the linked production support registry was 0 cases and the production Auth provider had **0 users** at the time of deployment.

## Edge API, security, and operator scope

`alpha-support-manual-operator` must be deployed with `verify_jwt=true`. It requires a human Supabase Auth access token independently confirmed using `auth.getUser` and `auth.getClaims`, exact `aal2` MFA, exact production domain Origin, and an enabled DB-owned grant for that same user. It must fail closed with 401/403 until a verified owner-designated production operator account exists. An anonymous token, forged user metadata, valid JWT without roster, and a staging user do **not** authorize access. This endpoint never uses a browser-available service role token.

The operator can list 20 cases per page (category, status, timestamps and claim flag only) and claim one OPEN case once. Internal Gmail message IDs, email subject/body/sender, customer records, refund information and tokens never reach the browser. A separate owner-directed future release would be required for closure and refund policy actions.

## Operational activation still needed

The user has explicitly approved ONE operator, not a backup or 24/7 service. The existing production Supabase Auth project currently has zero accounts. To make the operator UI genuinely usable, the one intended operator must create/verify **their own** production Auth account, set up MFA with aal2, and receive one owner-approved grant `enabled=true` in the **private** roster; do not invent an Auth UUID, turn on unrestricted registration, or accept staff identity solely from client-supplied IDs. The account should not be granted any billing/refund approval by this project. No production staff-user is provisioned in this release. Separate support/incident and HighLevel CRM opt-out acceptance remains pending.

The public manual support route https://alphacreativecontent.com/support.html continues operating with a single human reviewing requests when available and no guaranteed response time.

## Acceptance

- PR focused offline secure-metadata contract must pass.
- Production private RLS & no-public-EXECUTE readback must pass.
- Edge JWT verification must remain ON and exact source readback verified.
- Reject unauthenticated requests on actual hosted URL (HTTP 401/403) without consuming tokens or issuing a case.
- No need to rerun prior Task21 Gmail ACK or Ask Alpha paid tests.

Task21 full customer support system remains OPEN until a real production staff login & protected operator read/claim acceptance, effective owner-approved support and dispute policy, and production incident/human process are verified.
