# Task20 conflict reconciliation (November 10, 2026)
This is a source-only forward port from historical Task20 PR #350 to the current
`codex/alpha-prelaunch-safe-integration-20261110` release candidate.
Preserve newer intake consent, CRM revocation and suppression, quote math and registration.
Preserved current, more recent versions of `index.ts`, `ghl-sync.mjs`,
`ask-alpha.mjs`, and `agm-ghl-unit-smoke.mjs`; those should NOT be replaced
by old PR #350 copies. Preserved newer scripted marketplace answers while
integrating AI checkbox UI behind default-off build flag.
Reused canonical mock-only `agm-ask-alpha-offline-guards.mjs` from merged PR #378,
rather than copying the older branch's obsolete fixture.
AI remains OFF. No OpenAI calls, live payments, production migrations, marketing
or STOP mutations authorized. SQL tests use disposable localhost PostgreSQL only.
The old hosted-negative workflow pinned to the historical Task20 feature branch
was not transferred to avoid unrequested live endpoint probes.
Hosted security, privacy, issuer, budget-meter and human support acceptance remain separate.
