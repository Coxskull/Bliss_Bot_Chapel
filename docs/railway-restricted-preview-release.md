# Bliss Bot — Restricted Interface Preview Release Procedure

**Owner evidence:** `ALPHA-EV-20261017-C8M4RS`  
**Classification:** APPROVAL-GATED, INTERFACE-ONLY, NON-OPERATIONAL  
**Last verified:** 2026-10-17 (Asia/Manila)

This procedure is for a **separate Railway preview environment** after explicit owner
release authorization, not the current production environment. It is never a
substitute for identity, session, consent, privacy, or commercial acceptance.

## Current blockers (verified before this PR)

- PR #274 is merged; PR #275 is READY FOR REVIEW / UNMERGED at
  `9e7edf83b629b81e864c42c1274983d5e8cc1e3e`.
  PR #275 is the fail-closed static-file allowlist. **Never deploy an
  interface preview without that security review and merge.**
- The existing Railway `bliss-bot` production service is OFFLINE. Its source
  remains `codex/bliss-railway-build-20261016`, latest deployment
  `d9ff20be-234b-4636-a429-263ed61c59df` FAILED on OIDC startup.
- Its configured health check is `/health/ready`, which is intentionally
  HTTP 503 in restricted preview. The service's variable names do NOT yet
  include `Runtime__RestrictedPreview` (values were not read or copied).
- The service has a persistent volume at `/data` and a Data Protection keys
  variable name already configured. No assumption is made about value
  correctness or writable filesystem state.
- The PostgreSQL service is online, but this is *not* application readiness.

## Stage 0 — approvals (STOP until fulfilled)

1. Independent security and operations reviewer approves #275; inspect all
   changed files, especially public asset allowlist and deny-by-default.
2. Confirm four exact-head GitHub workflows on the final merged code;
   reject failed/in-progress/skipped required checks.
3. Owner explicitly approves merge and an **isolated preview only**. Do not
   use the live production service or copy authentication secrets into the
   preview without a separately reviewed need.
4. Confirm costs/approval before adding a Railway environment, service,
   database or paid resource. The existing production environment is not
   a disposable test environment.

## Stage 1 — prepare approved isolated Railway configuration

- Use a separately scoped environment named `restricted-preview`; inspect
  its variables, source and changes before any deploy. Do not duplicate live
  production variables or data indiscriminately.
- Point the PREVIEW source only at a reviewed branch/commit including #275.
- Set `Runtime__RestrictedPreview=true` **only in the preview environment**.
  Keep default `Runtime:RestrictedPreview=false` elsewhere. Do not set
  `ASPNETCORE_ENVIRONMENT=Development` to bypass security.
- Use the existing `Dockerfile.railway` Docker image build and ensure the
  required `Runtime__DataProtectionKeysPath` is set to a writable secure
  directory, with safe ownership. Check config *names* only; never print
  secrets to reports.
- Configure preview startup healthcheck **`/health/live`**.
  Ordinary production must continue to use **`/health/ready`**.
- The optional `railway.preview.json` in this repo contains these build/
  healthcheck differences. For a legacy Railway service that already uses
  Config as Code, explicitly select the custom config file path
  `/railway.preview.json` **in the preview service's settings only**.
  Merely adding the file does not select it. The normal root
  `railway.json` keeps the production `/health/ready` gate.
- Railway documentation states legacy Config as Code is deprecated and ends
  2026-12-01. New Railway services cannot opt into legacy Config as Code.
  For new services or post-cutoff deployments, use the supported Railway
  Infrastructure as Code workflow; do not assume `railway.preview.json`
  will apply. Review the effective deployment config before activation.
  Docs: https://docs.railway.com/config-as-code and
  https://docs.railway.com/infrastructure-as-code
- Inspect pending changes and require a second human approval before
  accepting the deployment. Do not mutate production, Supabase OAuth,
  payments, data or live domains.

## Stage 2 — independent hosted smoke test (read-only)

With an explicitly approved **HTTPS PREVIEW** URL and after the new
deployment reports healthy, execute:

```bash
bash scripts/restricted-preview-smoke.sh https://YOUR-APPROVED-PREVIEW-HOST
```

The script makes only HTTP GET requests. First it requires
`/preview-status` HTTP 200, response header
`X-Bliss-Mode: restricted-preview`, and JSON
`restrictedPreview=true, operational=false`. If that proof is missing, it
**stops before any further probes**. It then expects HTTP 200 on the reviewed
landing page, CSS/JS/image and `/health/live`, and HTTP 503 on readiness,
API, OIDC callback, internal HTML, operations pages, generated media and
non-reviewed assets. No redirects or successful auth responses count as pass.

The script deliberately does **not** POST to business endpoints: HTTP
mutation denial remains covered by local/CI integration tests. Never test
against a commercial production endpoint or issue a write that could trigger
business activity.

## Stage 3 — record result and maintain blockers

Capture exact GitHub SHA, reviewer, CI run IDs, preview deployment ID,
environment ID, healthcheck response, sanitized runtime errors, and smoke-test
output. Retain `CampaignReady=false`, `Delivery=NOT_SENT`, and all hosted
gates `IDP-01`, `SESS-01`, `OWN-01`, `PRIV-01`, `CONS-01`,
`HOST-01`, `COMM-01` **BLOCKED** until separately verified.

A passing `/health/live` proves a process is alive, NOT database, OIDC,
privacy, payment, marketplace operation or commercial readiness.
