# ALPHA-503 independent repair — 2026-11-09

## Actual repair
Based on PR #367 exact head 5971c39b230e543987c39374bf2070e6f3deeb02, isolated branch codex/independent-repair-503.

Confirmed proposal evidence defect: changing amount, currency or quoteVersion left fingerprintSha256 identical. New regression failed against the original source (4 existing cases passed, fingerprint case failed). V2 fingerprints bind provider, service, price, currency, version, explicit approval states, receipt and deliverables in deterministic field order. Old fingerprints must be regenerated; this is a synthetic review identifier, not a signature or customer acceptance record.

Also fixed version validation accepting coerced values such as string "1", array [1] and unsafe integers. Versions now require positive safe integer numbers. No approval, payment or fulfillment authority is added.

Files repaired: scripts/alpha346-manual-proposal-gate.mjs and scripts/alpha346-manual-proposal-gate.test.mjs.

## Verification
- Offline public build: BLISS_APPLICATION_ORIGIN=https://example.org node scripts/build-vercel-portal.mjs — PASS.
- node --test --test-isolation=none scripts/alpha346-*.test.mjs — 32/32 PASS. Isolation disabled because sandbox child test processes failed; same assertions execute in-process.
- git diff --check — PASS.
- Local dotnet absent; no local C# or Postgres execution claimed. Existing GitHub exact-head evidence independently fetched: #356 9 successful workflows; #359 current aef1f16c 13 successful workflows; #367 run 42582308461 success; #371 run 42583848966 success. These do not verify this repair's future CI.

## Repository and session reconciliation
#358 f39c2508 is arithmetic/market validation, not a Stripe integration repair. #359 is the consolidated commercial candidate; avoid independently merging the overlapping #343/#356 predecessors. #367 and #371 carry sandbox payment receipt work. None is merged.

Git refs fetched from GitHub. No commercial/directive-502 branch exists in the fetched remote refs or connector branch search. Infrastructure Session 1 was not unambiguously identifiable. Available infrastructure PR #346 (706fd64e) changes five files; its main-relative changed-file set has NO intersection with #359's commercial set or these repair files. This establishes published-branch separation only, not absence of unpushed edits. Before integration, obtain exact session branch/head/file lists and repeat git diff --name-only comparisons. This repair changes no existing session branch.

## Payment and quotation inspection
Reviewed StripeSandboxClient, raw-body StripeSandboxVerifier, StripePlanningSandboxWebhookVerifier, PlanningSandboxStripeReceiptReview/store, offline reconciliation and #371 host source. Signed events use HMAC SHA-256, constant-time comparison and five-minute timestamp tolerance; live/Connect events are rejected. Receipt store verifies before DB access, takes event/session/intent advisory locks, compares persisted identities/hash/state and commits before returning. Host only ACKs allowed receipt decisions after persistence; conflicts and uncertain writes remain non-2xx. Pending-to-paid transitions deliberately require review and are not automatically reconciled into fulfillment.

Offline Stripe readback tests validate the existing test product/link/price, paid session, PaymentIntent, tax and decline event evidence. A committed readback JSON is evidence input, not a fresh authenticated provider query. No new Stripe API read, hosted webhook retry, live merchant eligibility or customer payment was verified here.

Quotation gate remains SYNTHETIC_ONLY, customerAccepted=false. There is no demonstrated production customer acceptance -> immutable accepted quote -> merchant-bound Checkout -> signed durable receipt -> authorized human fulfillment chain. Do not treat its fingerprint or browser redirect as acceptance/payment proof.

## Exact production release requirements
1. Independent review and passing exact-head CI for selected repairs; integrate into one commercial candidate, reconcile #358 and all child branches without duplicate predecessor merges. Preserve STOP and restricted preview.
2. Identify infrastructure session head; complete existing backup/restore, ingress/TLS, operator authentication and security release gates (#302/#335). No replacement infrastructure needed or authorized.
3. Owner/legal/finance approve planning scope, price/currency/tax, revisions/timeline, refund/privacy terms and monitored post-purchase support; verify live AGM merchant/account/payout eligibility. Sandbox readiness cannot establish this.
4. Persist real customer identity/authority, consent and explicit acceptance of a versioned immutable quote/terms. Bind server-authoritative merchant/order/quote/amount/currency to Checkout; use stable idempotency keys and durable receipt uniqueness. Reuse existing financial gateway components; current sandbox adapters must not be repurposed as live authorization.
5. Verify authorized isolated hosted test webhook delivery, retries, duplicates/concurrency, conflicts, pending/success/failure/expiry and reconciliation against provider state. Verify durable receipt readback and human-only fulfillment handoff. Synthetic/loopback green CI alone is insufficient.
6. Obtain a separate explicit production release decision. This directive authorizes no deployment, live charge, outreach, credential changes or spending.

No production state, factory STOP, credentials, Stripe configuration, customer records or infrastructure were changed.
