# Internal hosted acceptance preparation

This preparation grants no users, changes no environment, and does not permit disabling
restricted preview. Run `python3 scripts/bliss-operational-preflight.py names.json` with
the Railway **variableNames** array only. Exit 2 means missing names or invalid input;
exit 0 means names present, never valid settings or operational acceptance. Do not export
environment values, passwords, tokens, cookies, or private user identifiers to evidence.

## Owner decisions

Confirm organization ownership in Supabase Organization Settings > Team. The last team
readback identified alpha7auto@gmail.com as Owner and Erwin as Administrator, within
"Erwin Org's". An organization name or account label does not establish corporate control.
An ALPHA officer should independently sign in to the owner account, verify the project
and Team roles, and verify that its email recovery and MFA recovery are controlled by
ALPHA. Never put recovery codes in an engineering report. Do not transfer ownership or
invite administrators without explicit approval.

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
