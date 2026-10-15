# Phase 2 — Advertiser intake and versioned Brand DNA

Scope: authorized advertisers and chapel staff can save partial brand intake,
resume it from the database, inspect immutable history, and explicitly confirm
a complete brand direction. This is human intake; no assistant replies or
provider calls are introduced. Confirmation is brand direction only, not
creative approval, campaign readiness, or permission to send.

Required at confirmation: brand name, niche, market, language, campaign goal.
Drafts may be incomplete. Optional: colors, font, restrictions. All fields are
bounded plain text. No arbitrary URLs, uploads, secrets, or HTML are required.

Each save appends a DRAFT revision. Confirmation appends a CONFIRMED revision
copying the latest draft. Earlier confirmed versions remain intact; a subsequent
draft is unconfirmed. Revision number is unique per workspace. The client sends
its expected current version; stale saves and confirmations return 409.
Idempotency keys are scoped to the workspace; identical retries return the
existing revision and changed payloads return 409. Authorization precedes
replay lookup. All writes atomically append an audit event with server-resolved
actor identity. No update/delete endpoints exist; EF rejects modification or
deletion of stored revisions. Database constraints arbitrate concurrent writes.

API under `/api/wedding-planner/workspaces/{workspaceId}/brand-dna`:
- GET returns ordered revision history.
- POST appends a draft with fields, expectedVersion, and idempotencyKey.
- POST `/confirm` appends confirmed brand direction with expectedVersion and
  idempotencyKey; required fields must be present and latest must be DRAFT.

Public `/brand-intake.html` uses same-origin authentication and CSRF. Bound
advertisers open their own primary workspace; chapel staff select an existing
advertiser. Selection is not authorization. The page saves and resumes drafts,
displays history, and requires a separate explicit confirmation action.
Browser storage never becomes durable brand authority. Stale saves retain
entered text and require reload/review. The existing public AI preview remains.

Validation: drafts/reload, required fields, confirmation, immutable history,
retries/conflicts, concurrent PostgreSQL writes, audit atomicity, anonymous and
cross-tenant refusal, CSRF, browser save/resume/confirmation, and full regression.
Use local fictional data; production deployment is separate. Phase 3 starts
only after Phase 2 review and acceptance.
