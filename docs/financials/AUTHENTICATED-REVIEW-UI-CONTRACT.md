# AGM-LAUNCH-010 R10 — authenticated mock review UI

Status: development milestone submitted for review; parent IN PROGRESS.
Stripe: UNVERIFIED / OWNER DECISION REQUIRED. No activation, account creation or actual provider processing.

## Reuse and delivery

The separate `bliss-frontend` now consumes the existing same-origin `/api/auth/session` cookie/CSRF contract and mock financial queue/case/history APIs. It requires authentication enabled, authenticated staff `canWrite` and a CSRF token; the backend additionally checks explicit current subject/merchant/module/campaign funding authority on every request. Session claims do not grant financial scope. Authentication-disabled Development does not expose financial UI actions.

Build with Node24 (`npm ci`, `npm test`, `npm run build`) before .NET packaging. Vite's base and output align with the existing static host: `/operations/financial-review/index.html`, generated assets in ignored `frontend/operations/financial-review`. The existing .NET project copies the entire frontend tree to output/publish. Generated assets are not committed; an automated production build/publish pipeline for this new package is not delivered or activated here. Existing operations frontend, cookie settings and production configuration are not changed.

## UI behavior

Reads a server-scoped queue, module filters, current case snapshot, bounded note pages and original immutable note evidence. Displays minor-unit USD strings using BigInt; recognizes existing numeric financial module enums. Request/result identity checks guard asynchronous case changes. Strict mock envelope flags and exact monetary types reject live/approval/release/malformed responses.

A new annotation posts only operationId, evidenceFingerprint, note and a random UUID idempotency key, plus the server CSRF token in a header. Actor, role, scope, amounts and recipients are never posted. CSRF/session tokens are only in memory. React escapes note/evidence text; no raw HTML execution. No payment, approval, cancellation resolution or release control exists.

401/403 clears visible financial evidence and actions; stale/busy409 removes the stale current case and requires refresh. Writes are not automatically retried. Uncertain outcomes disable new notes, filters and case switching; exact retry lookup can resolve a known stored annotation without a second POST. Unknown write correlation is in memory: reloading loses it, and operators must inspect persisted history before an intentional new observation. A matching text from a different actor/version is never used to resolve an uncertain write.

`GET /api/financials/mock-review/cases/{operationId}/note-retries/{retryKey:guid}` resolves only the authenticated actor's retry key inside the authorized case. Returns the immutable note or explicit `result:null`; absence is not proof the write failed. Same registered scope, staff, disabled/Mock-only/loopback guards apply. No schema or financial mutation. Historical non-UUID keys remain accessible via existing note history; this new lookup covers UUID frontend retries.

## Local acceptance and limits

Browser rehearsal uses a test-only loopback bridge to the real .NET TestServer, existing synthetic identity handler, real antiforgery validation and disposable financial PostgreSQL. The bridge is outside production code and never activates actual OIDC/provider/network services. This proves local browser/API/DB behavior with synthetic identity, not hosted identity onboarding or account acceptance. Verify scoped queue and all three modules, persisted note after reload, historical evidence, CSRF denial, stale evidence refresh, exact retry resolution, session/role denial and desktop/mobile layout. Additional intercepted browser tests cover malformed responses and uncertain network outcomes. Preserve raw logs and exact commits.

Cancellation-policy proposal remains pending owner review. No distinct-admin decision/quorum or release implementation. Public Economics approval and external provider state are outside current private snapshots. Account/legal merchant/payee/fee decisions, exposed-secret replacement, real sandbox acceptance and hosted deployment remain pending, while mock development continues.
