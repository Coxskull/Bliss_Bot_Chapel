# AGM-LAUNCH-010 R6 — configurable mock profiles and bound callbacks

Owner correction, 2026-10-14 Asia/Taipei:

- STRIPE ACCOUNT STATUS: **UNVERIFIED / OWNER DECISION REQUIRED**.
- AGM-LAUNCH-010: **IN PROGRESS — DEVELOPMENT CONTINUES**.
- No verified Stripe account is established for ALPHA GLOBAL MEDIA or Bliss Chapel. Previous test-key provision is not evidence of ownership, verification or onboarding.
- No account creation, real Stripe activation, production access, live transfers, paid services, merge or deployment is authorized. Continue mock development and disposable testing.

## Configurable authority without account assumptions

`FinancialPreparationOptions` retains `Enabled=false`, `Mode=Mock`, empty connection and empty bindings by default. Its account-status reading returns the owner-confirmed unverified status; it cannot be switched to verified by a payment DTO. `FundingBinding.MockProfileId` selects exactly one server-configured `MockAccountProfile` for bound fixture callbacks. Profiles are logical fixture labels, not actual Stripe account IDs or legal entities. Profile signing material must use the synthetic `whsec_fixture_` marker. There is no API for creating accounts or uploading real keys.

This registry gives a configurable selection point for future merchant account adapters; no business account or real connector is wired today. After owner verification/onboarding and approval, a separately reviewed adapter must resolve the legal merchant, real account, secrets and permitted funds flow, and replace the unverified policy with evidence-backed verification controls. Changing fixture profiles does not activate real processing. Sandbox/Live modes are refused by the public mock routes before database access.

## Bound mock callback

`POST /api/financials/mock-bound-webhooks` is authenticated, staff-role protected, CSRF protected and write-rate limited. It takes only operationId, rawJson and signature. Financial routes remain disabled by default and restricted to explicit loopback `financial_test`; this is not a publicly deployed Stripe webhook.

The workflow loads persisted operation authority, checks the currently approved/accepted source through the existing importer, selects the configured fixture profile, and resolves a BOUND dispatch identity. Only `pi_fixture_` identities qualify; ordinary provider IDs and unbound operations are refused. It verifies the exact raw-body signature, timestamp, test mode, event type/status, amount, USD currency, merchant/module/campaign, funding reference and mock profile metadata. Connect account events are refused by the existing verifier. No amount, payer, recipient, owner or profile is accepted from the request as financial authority.

In one transaction, the store locks the operation and event, freezes the profile/payer/recipient-owner binding, appends the simulated balanced TEST_CASH/TEST_RECIPIENT journal and receipt/capture, and records the raw-body fingerprint. Same-event concurrent retries return the original journal. A second valid event for the same bound identity appends callback evidence without creating a second journal or receipt. Re-signed changes to the original body conflict with immutable evidence. A callback cannot rebind a profile, payer, recipient owner, operation or PaymentIntent. Legacy `pi_mock_` capture and bound fixture capture cannot both fund the same operation. Refund reservation now follows the actually persisted simulated capture identity instead of assuming the legacy ID format.

Migration45 adds append-only mock bindings/events, rejects UPDATE/DELETE/TRUNCATE and uses deferred constraints to require matching dispatch evidence and capture attribution. Rollback on receipt/event conflicts removes the new binding and journal/capture together. Existing operation/quote persistence and public EF model metadata are retained. Automatic destructive Down is refused. Privileged database owners can disable triggers; least-privilege deployment and independent audit retention remain necessary before any hosted acceptance.

## Test interpretation and remaining development

New HTTP/PostgreSQL tests loop Bliss Bot, Creator Academy and Wedding Planner through bound fixture identity, signature/scope/source/profile rejection, staff-role isolation, source revocation, concurrent replay, distinct-event deduplication, immutable-body conflict, legacy-path collision and attributed partial refund. Direct PostgreSQL tests exercise binding rebinding, cross-operation event replay, raw forged journal attribution, mutation/truncate rejection and rollback after a legacy receipt conflict. A no-database HTTP test refuses disabled, Sandbox and Live configurations. These use synthetic identities/signing material and seeded fixture BOUND results, not a real Stripe account, hosted OIDC or actual provider execution.

Actual provider dispatch/worker configuration, genuine callback-to-account mapping, retrieval/reconciliation for long-age uncertainty, actual refunds/disputes/fees, multi-beneficiary/commission policy and full product UI/hosted acceptance remain. Account arrangements and legal payee decisions gate actual activation; they do not block further authorized mock software development. USD is confirmed; the target countries remain Colombia, Dominican Republic, Panama, Philippines, Malaysia and Thailand. No entities, commissions or fee defaults are invented.
