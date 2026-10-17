# Vercel portal + Railway application

Deployment configuration only. No service has been deployed by these files.

Vercel serves a static workspace portal built from the existing
`frontend/public/global-media.html`, CSS and approved image. Railway runs the
existing Dockerfile, serving all application pages and APIs together. Portal
links open the complete Railway workspace rather than splitting browser cookies,
CSRF requests or OIDC callbacks across origins. GHL can link to the Vercel hub.

## Railway first

Use a dedicated Bliss service sourced from this repository's reviewed revision,
with repository root as build context. Do not change Alpha Auto services or use
their database. Target port is 8080, as declared by the existing Dockerfile.
The readiness endpoint is `/health/ready`; startup is fail-closed.

Before deployment, supply the existing production requirements described in
`PHASE-6-HOSTED-RUNBOOK.md`: a dedicated hosted database with VerifyFull TLS,
real OIDC authority/client and role/advertiser bindings, encrypted persistent
Data Protection key storage and certificate, an actual trusted ingress proxy
configuration, and verified backup declarations. Railway's volume placement,
non-root write permissions and ingress proxy behavior must be inspected before
claiming compatibility. Do not invent proxy IPs or disable production gates.
Keep controlled AI disabled until separately approved.

Run migrations as an authorized separate release operation after backup and
database identity checks. Do not add Development seed mode or automatic
production migration on every web process start. Register `/signin-oidc` and
`/signout-callback-oidc` on the final Railway application origin with the identity
provider. Verify login, logout, access denials and cookie persistence there.

## Vercel second

Import the same reviewed branch/revision, root directory `.`, with the static
build and output settings in `vercel.json`. Set the non-secret build variable
`BLISS_APPLICATION_ORIGIN` to the verified Railway HTTPS origin. The portal
builder rejects absent, non-HTTPS or credential-bearing origins; do not use a
placeholder for a real deployment. Do not put database, OIDC or API secrets in
Vercel frontend configuration.

Validate the portal and each app link before setting GHL's workspace origin or
hub URL. This does not make AI generation, campaigns, payments or payouts live.
Account access, hosting capacity/cost, secrets and identity configuration remain
deployment prerequisites. No new subscription or expenditure is authorized by
this configuration alone.
