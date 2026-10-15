# AGM-LAUNCH-010 R5 — inactive dispatch and recovery foundation

This milestone continues merged PR #167 (R4 head `4cec5b4c33c77d76c59f49954b1c4d22ccba7341`, merge `9571a9ea2349f8fa47a30ef1cfcfb84e1b166b99`). It is a development candidate, not activated Stripe processing. No worker, dispatch route, DI registration or provider credentials are configured. Existing public financial routes remain disabled by default and Mock-only. Tests use a synthetic test-secret label and an intercepted HTTP handler; no Stripe network requests are executed.

## Immutable dispatch evidence

Migration `20261014000200_FinancialDispatch` adds attempts and results without changing the EF public model. Attempts reference the existing immutable operation. Results reference the exact attempt/operation pair. Attempts and results reject UPDATE, DELETE and TRUNCATE; one operation and one provider PaymentIntent can each have at most one BOUND result. Privileged database owners can disable triggers; these controls do not replace least-privilege deployment or independent audit storage.

The store reads amount, USD currency, merchant/module/campaign, recipient, quote reference and provider key from persisted authority owned by the authenticated stable subject. A client cannot supply a replacement amount or payee. Runtime adapters must also enforce advertiser roles, trusted advertiser identity, current accepted-source authority and legal bindings. The dispatcher requires a trusted current-authorization callback before every provider call; tests demonstrate denial, but no production callback or authenticated dispatch endpoint is wired here.

## State and recovery

1. A short transaction locks an operation and appends a two-minute lease before external I/O. Concurrent callers receive IN_FLIGHT. No database transaction stays open during an HTTP call.
2. The inactive Stripe sandbox adapter receives the persisted snapshot and stable key. Network/response errors and cancellation produce UNKNOWN evidence using an independent bounded cleanup token; UNKNOWN never means that the provider did nothing.
3. Retrying an UNKNOWN outcome reuses the original operation and exact provider idempotency key. A restarted worker reloads durable state. If a worker dies before recording a result, a later worker can claim after lease expiry. Provider idempotency is required even when an expired worker still has an in-flight request.
4. Responses must match amount/currency/metadata, test mode and permitted PaymentIntent status. Completion rechecks the snapshot and fences expired or superseded attempts. A provider identity reused across operations raises a database conflict; it is not silently reassigned.
5. BOUND means only that a validated PaymentIntent identity was recorded. Even a `succeeded` creation response does not book cash or mark payment complete. Subsequent provider state changes, signed callbacks, disputes, actual refunds and fee reconciliation remain separate work.
6. An unresolved operation with an attempt aged 20 hours receives MANUAL_REVIEW and makes no automatic provider call. This conservative boundary precedes Stripe's documented minimum 24-hour idempotency retention. No fresh key is generated. A reviewed retrieve/reconcile procedure is still required; this milestone does not implement automatic recovery after that boundary.

Lease timing uses the database clock. Crash/expired-attempt tests seed old timestamps in the disposable fixture instead of waiting two minutes or twenty hours. Those inserts are test setup, not a public interface for changing audit timestamps. Creation-response persistence can fail or collide: evidence remains an unresolved attempt and must be reconciled; no successful payment is inferred.

## Validation and remaining acceptance

The PostgreSQL suite exercises all three modules, concurrent dispatch, replay, worker recreation after UNKNOWN, cancellation cleanup, owner isolation, current-authority rejection, forged completion snapshots, expiry fencing, manual-review cutoff, identity conflicts and audit mutation rejection. Upgrade tests start at financial foundation migration43, preserve an existing Economics quote and prepared funding operation, migrate to44, repeat safely, bind a fixture identity and refuse destructive Down. HTTP/auth tests from R4 remain part of the full suite; R5 dispatch tests are infrastructure tests with synthetic identities/authorization callbacks, not hosted OIDC acceptance.

Next implementation: authenticated sandbox dispatch adapter and approved worker configuration; authoritative signed provider callback-to-operation mapping; retrieval/reconciliation for unknown or stale outcomes; actual provider refunds/disputes/fees; legal payee and commission policy; full product UI and isolated hosted acceptance. Merchant/payee/fee decisions, exposed test-secret replacement with separate webhook secret and an owner-confirmed isolated target are required before actual sandbox activation. USD and the six target countries are recorded; no entities or fee defaults are invented. No production access, live transfers, merge, deployment, paid activation or credential changes are authorized by this milestone.
