# AGM-LAUNCH-010-SUB-004 — isolated sandbox acceptance gate

Prepared acceptance plan; NOT EXECUTED against Stripe. Local mocked tests are separate evidence.

Required before actual provider activation: owner-reviewed R4 scope, legal collecting merchant and authorized payees/fees, six-market capability review, USD settlement confirmation, revocation/replacement of exposed test secret through secret store, separate webhook signing secret, explicit Stripe test account identity, isolated approved database/schema/least-privilege role, real test-mode adapter/receipt mapping and approved test-only network ingress. No live keys, transfers or production target.

Current public routes are explicitly MOCK and disabled by default; they accept pi_mock identities and cannot be used as real Stripe webhook endpoints. Do not change the Mode flag to pretend real sandbox integration exists. Extend/review an actual test-mode callback mapping separately. Existing StripeSandboxClient is inactive and mocked only.

Acceptance evidence sequence:
1. Record exact commit, approved environment/account identifiers and provider capability/API version. Never put secret values in logs or PDFs.
2. Create accepted synthetic USD source quote and confirmed test recipient binding. Authenticated initiation must reject amount/recipient overrides, another advertiser, unapproved/revoked versions and repeated conflicting keys.
3. Create actual Stripe test PaymentIntent using persisted operation key. Record real test intent/event IDs only after API execution; no invented placeholders. Verify amount/currency/account metadata and no live mode.
4. Test signed raw-body callback, stale/forged signatures, duplicate events, different event same intent, changed replay and out-of-order states. Assert one transactionally committed journal/capture and no client redirect-based payment completion.
5. Request two concurrent partial test refunds; total cannot exceed original captured amount. PENDING/failed responses cannot become completed refund totals. Verify original beneficiary attribution, signed completed state, provider refund-ID dedup and reversal journal.
6. Reconcile actual provider test balance/fee/refund facts against local journals and module reporting. Unknown fee/commission rules must stay unknown, not default to zero or route to a guessed payee.
7. Crash before/after provider acknowledgment, restore/resume and reconcile unknown outcomes without repeating a charge/refund. Preserve immutable histories, reviewed migration/upgrade/rollback evidence and denied privilege tests.
8. Independent reviewer compares exact-head logs/TRX/schema/real test IDs and records scope. Owner acceptance/deployment/live-money authorization remain separate.

No real provider fee policy, payouts, Connect capability, hosted TLS/OIDC or production acceptance is claimed by the mocked lifecycle.
