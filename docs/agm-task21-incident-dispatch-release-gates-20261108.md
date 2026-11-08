# ALPHA GLOBAL MEDIA LLC — Task #21 internal incident dispatch release gates

**Date:** 2026-11-08 (Asia/Manila)  
**Status:** Source-only engineering accepted only after exact-head CI. **NOT a running notification system.**  
**Repo:** Coxskull/Bliss_Bot_Chapel, draft PR #353  
**Staging:** Supabase development branch `wdlmcriajgsretgyusmo`; **Production:** no change  
**Global STOP:** engaged. **Incremental spend:** $0.

## Implemented this stage

- `scripts/task21/incident-escalation-contract.mjs` validates the five sensitive categories and strips private fields before generating a fictional, metadata-only incident envelope.
- `scripts/task21/incident-escalation-dispatcher.mjs` adds a *source-only* internal-email dispatch boundary. Default: **DISABLED_OR_UNAPPROVED**.
- Requires positive, server-only `environment=staging`, explicit synthetic-alert enablement, exact approved internal recipient, confirmed monitoring, **durable atomic lease/receipt ledger** and an injected authenticated transport. Neither ledger nor transport is configured in the deployed API. No new provider credentials are stored in source.
- A duplicate, stale/unverifiable lease, invalid record, sender exception, missing provider receipt or failed receipt persistence fails closed. A provider-accepted response is **not** treated as mailbox delivery or human acknowledgment; uncertain persistence is **DO NOT AUTOMATICALLY RETRY**.
- The source and tests never call Gmail, HighLevel, customer contacts, payments or other remote providers.

## Separate proofs mandatory before enabling any notification

1. **Approved address and consent.** Owner confirms one monitored **internal** mailbox/channel and its designated primary; no client addresses or private case details in notifications. Confirm that the selected provider's API is authorized, rate-limited and within $0 incremental spending.
2. **Backup and coverage.** Nominate a distinct reachable backup operator. Verify both actual identities, secure access, availability window, out-of-office and escalation paths. A solo operator is not 24/7 coverage.
3. **Durable ledger.** Implement an atomic single-claim-per-alert mechanism in approved isolated staging, with TTL/retry semantics, authenticated provider receipts and immutable attempts. Prevent duplicate delivery; test concurrency and crash recovery without leaking PII.
4. **Provider transport.** Implement approved server-side only provider adapter and scoped secret, ensure no public/browser tokens. Only internal synthetic channel is permitted initially. A provider acceptance ID must be persisted before marking accepted.
5. **Independent human ACK.** Receive actual delivery evidence and independently timestamp a real responding human. Do **not** mark the original `alpha_support_alert_intents.delivery_state` as sent/acknowledged; it is intentionally immutable at `UNROUTED` until a separate reviewed state model is approved.
6. **Deny / rollback.** Prove disabled path (zero egress), duplicate claim, revoked operator, unknown channel, wrong origin, missing MFA for security, send failures, uncertain receipts, expired lease and rollback. Test escalation to backup **without** customer messaging.
7. **Policies and sandbox.** Obtain owner/finance/legal approval for refund, failed campaigns, privacy/security incident response, SLA and retention; verify HighLevel opt-out and claim/finish in an explicitly isolated sandbox with outbound automation disabled.
8. **Release review.** All exact-head workflows green; private config, staging isolation and redacted receipts witnessed. Formal acceptance and separate production deployment approval required. Do not merge or release Global STOP automatically.

## Actual operational status at this checkpoint

The single browser-case trial was **CLOSED** and its temporary permission **revoked** in the previous staging acceptance. One fictional critical incident alert is still **UNROUTED**. An earlier internal Gmail self-test had human ACK, but it was **manually sent** and cannot count as application-generated delivery. Primary GENERAL_SUPPORT staging access exists; no independently verified sensitive-case coverage or distinct backup has been nominated.

**No-go until all evidence above exists.** Run the new source-only test:

```bash
node scripts/task21/incident-escalation-dispatcher.test.mjs
```
