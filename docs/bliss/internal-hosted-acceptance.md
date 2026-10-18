# Internal hosted acceptance preparation

This preparation grants no users, changes no environment, and does not permit disabling
restricted preview. Run `python3 scripts/bliss-operational-preflight.py names.json` with
the Railway **variableNames** array only. Exit 2 means missing names or invalid input;
exit 0 means names present, never valid settings or operational acceptance. Do not export
environment values, passwords, tokens, cookies, or private user identifiers to evidence.

## Owner decisions

The owner has confirmed ownership of alpha7auto@gmail.com. This resolves email ownership
only; independent dashboard access, MFA/recovery and corporate control remain unverified.
Confirm organization ownership in Supabase Organization Settings > Team. The last team
readback identified alpha7auto@gmail.com as Owner and Erwin as Administrator, within
"Erwin Org's". An organization name or account label does not establish corporate control.
An ALPHA officer should independently sign in to the owner account, verify the project
and Team roles, and verify that its email recovery and MFA recovery are controlled by
ALPHA. Never put recovery codes in an engineering report. Do not transfer ownership or
invite administrators without explicit approval.

### Samsung phone: independent verification

1. Open Chrome on the owner's Samsung phone and visit https://supabase.com/dashboard.
   Use the existing sign-in method associated with the owner account. Do not create a
   replacement account if the organization is absent. If controls are hidden, Chrome's
   menu > Desktop site and landscape mode may help.
2. Select "Erwin Org's", then Organization Settings > Team. Confirm the signed-in
   alpha7auto@gmail.com membership says Owner. Open the existing Alpha project and
   confirm its reference is giujjtscdvtpyjpnceag. Viewing Auth settings is sufficient;
   do not reveal keys or change roles. If the organization/project is absent, record
   that discrepancy and stop; do not remove administrators or transfer projects.
3. Open Account Settings > Security and verify the existing MFA status. For GitHub
   sign-in also verify GitHub 2FA and its recovery arrangements; separately verify the
   owner's email recovery is under ALPHA control. Do not disable MFA or test recovery
   by removing a working factor.
4. Supabase platform MFA does not issue recovery codes: its official guidance recommends
   a backup TOTP factor independent of the primary device. Check whether such a factor
   exists; if absent, report recovery as incomplete and have the owner arrange it
   privately. Never send a QR code, setup secret, password or authentication code.
5. Return only: independent login PASS/FAIL; Owner designation PASS/FAIL; Alpha project
   access PASS/FAIL; MFA verified YES/NO; independent recovery arrangement YES/NO; and
   confirmation that the owner controls this access on behalf of ALPHA GLOBAL MEDIA LLC.
   The organization display name alone proves neither ownership nor lack of ownership.

References: https://supabase.com/docs/guides/platform/access-control and
https://supabase.com/docs/guides/platform/multi-factor-authentication.

Approve named ALPHA-controlled internal identities and the following synthetic scopes.
These are proposed tests, not created accounts or permission grants:

| Identity | Server-owned app_metadata | Acceptance purpose |
|---|---|---|
| Viewer | bliss_roles: [bliss.viewer] | Authenticated browsing; write/review denied |
| Operator | bliss_roles: [bliss.operator] | Controlled synthetic write; no reviewer/admin grant |
| Reviewer A | bliss_roles: [bliss.reviewer], bliss_review_workspace_ids: [synthetic workspace A UUID] | A allowed, B denied |
| Reviewer B | bliss_roles: [bliss.reviewer], bliss_review_workspace_ids: [synthetic workspace B UUID] | B allowed, A denied |
| Negative control | No Bliss roles; subject absent from ApprovedTestSubjects | Login rejected |

Use a signed-out browser for unauthenticated tests. No bliss.admin grant is needed for
this matrix. These application identities need no Supabase organization membership.
Approve each account first; after actual registration/verification, use its immutable
subject UUID in Authentication__ApprovedTestSubjects__N, never its email. A trusted
administrator grants app_metadata; user_metadata cannot grant permissions. Refresh
sessions after approved grants. Do not run signup or notification tests without approved
internal destinations.

For each account obtain owner approval of its exact internal email, responsible person,
role, synthetic workspace assignment (reviewers), invitation/reset destination and test
expiry/cleanup decision. Do not request a password. Four approved subjects receive the
listed application scopes; the fifth negative-control identity deliberately stays off
the subject allowlist. Approving the matrix alone does not authorize provisioning.

## Existing prerequisites and execution order

1. Keep Runtime__RestrictedPreview=true while preparing. Confirm Production, existing
   HTTPS authority/callback, authentication enabled, persistent encrypted session keys,
   real trusted-ingress addresses, and real backup schedule/provider/retention. Do not
   invent proxy IPs or backup declarations to satisfy the startup guard.
2. Application PostgreSQL test must run in the application network using its existing
   Npgsql configuration, VerifyFull CA/hostname checks and a read-only query. A psql
   query from the database container is not this test. Check pending EF migrations
   before proposing any migration; do not apply migrations as preparation.
3. After owner gates and hosting prerequisites are satisfied, prepare a concrete
   deployment for approval. Normal startup must pass ProductionPostureEvaluator.
4. Execute real OIDC code+S256 PKCE and secure-cookie login for each approved identity.
   Record sanitized status and role outcomes. Check signed-out access, unapproved
   subject rejection, viewer/operator boundaries and both reviewer workspace directions.
5. Use the actual Vercel portal entry and normal protected browser session to exercise
   the intended Railway API requests and controlled synthetic workspace operation.
   Current portal hyperlinks alone do not demonstrate cross-origin API communication;
   verify the actual request origin, endpoint, cookie/CSRF behavior and response. Do not
   add permissive CORS, share cookies across unrelated domains, or weaken deployment
   protection to make a test pass.
6. Record hosted login, database, API and workspace results separately from CI. Keep
   CampaignReady=false and Delivery=NOT_SENT. Do not activate payments, campaigns,
   customer outreach, or AI delivery jobs.

## Hosted checkpoints (not executed by this document or offline CI)

| Order | Real test | Required evidence |
|---|---|---|
| 1 | Approved identity completes OIDC login | Correct subject/role, secure session, callback success; no tokens exported |
| 2 | Signed-out and unapproved-subject requests | Protected data denied; no login session for unapproved subject; record redirect/401/403 as endpoint contract specifies |
| 3 | Viewer write/review, operator review, reviewer cross-workspace | Disallowed actions denied with no data mutation; A/B allowed scopes and both cross-scope denials |
| 4 | Npgsql from application runtime | Certificate/hostname verified connection, read-only query, migration status; no raw connection string |
| 5 | Approved synthetic CRUD through actual application API | Unique synthetic record created/read/updated and cleaned up or clearly labeled; audit evidence and unauthorized mutation denial |
| 6 | Vercel entry through intended backend/API flow | Actual browser request origin, correct Railway endpoint, authenticated response and denied unauthorized request; links alone insufficient |
| 7 | Each intended workspace | Synthetic task completes and persists correctly, reload confirms state, role boundary preserved; no AI delivery or commercial action |

The implementation currently presents a static Vercel portal; do not invent a same-origin
API route. If a separate cross-origin API client is required, review the intended existing
flow first and prepare a focused change rather than weakening CORS/session protections.
Only after evidence for all required checkpoints can operational acceptance be considered.

## Retained website audit evidence (secondary workstream)

ALPHA-EV-20261018-R6W2KC: TinyFish run
https://agent.tinyfish.ai/runs/69a62460-9011-4869-b522-b03047c483f9 completed 16 steps
on 2026-10-18, 04:45:27-04:47:11 UTC, without an execution error. Homepage plus
/privacy-policy-review and /terms-of-service-review were accessed; both legal pages were
explicit drafts. Advertiser/creator links and a separate inquiry-form structure were
observed. No submissions or CRM records were tested. Mobile sizing was unsupported.
Missing button hrefs do not independently prove broken JavaScript behavior. Preserve
contact-button, advertiser/creator submission, CRM creation and mobile acceptance as
outstanding. Result mentions screenshots but exposes no separate download artifact.
No repeat audit, publication, payments, campaigns or customer outreach is authorized here.
