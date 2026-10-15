# AGM-LAUNCH-011 — one fictional campaign: stored handoff + mock funding correlation

**Status:** implementation pending exact-head CI and independent review. Sandbox/local fixture only.
**Dependency:** merged PR #216 (AGM-LAUNCH-010) mock capture / Mission Control ledger evidence.

## Read-only implementation

New staff-only `GET /api/financials/mock-review/cases/{operationId}/campaign-dossier/{handoffId}`.
The existing operator/admin permission and exact registered MockReviewerScope are checked by the financial evidence reader **before** reading any Academy or campaign record. The mock financial connection must be `127.0.0.1/financial_test`. Application campaign reads additionally require a relational PostgreSQL connection to loopback and refuse the generic `postgres` database. No production query or cross-account search is intended.

From previously **persisted** records, the dossier correlates:

- One accepted, current USD quote/version with one quote line and the exact integer-minor-unit amount frozen in a captured mock payment operation.
- Exactly one `TEST_CASH` debit and `TEST_RECIPIENT` credit from the previous mock capture; no inferred ALPHA commission or creator payable.
- Exact server-bound merchant/campaign/advertiser identity, quote version and approved fictional handoff UUID.
- A draft campaign, PLANNED/NOT_SENT handoff and unsent placement; matching placement run, content, match and slot.
- Stored Academy candidate, brief, historical inventory-fit decisions, actual bounded PNG bytes, SHA-256 and pixel dimensions, all matched to the frozen handoff snapshot.

Mission Control's existing financial review UI can inspect by exact operation UUID and then submit one handoff UUID for a read-only dossier. A client rejects any dossier claiming payment completion, creative acceptance, transcript connection, approved commission, creator payout, campaign readiness or delivery. The stored snapshot fingerprint and existing financial evidence fingerprint are **not signatures**.

## Evidence boundaries

This demonstrates **stored local correlation** across the planned creative handoff and one fictitious mock journal. PostgreSQL application and financial fixtures are read in **separate** snapshots; the response is not an atomic multi-database settlement certificate. Passing historical review references does not prove independently accepted artwork or creator/advertiser rights. Existing reference art and 32 × 16 synthetic PNG are not a finished advertising creative or placement preview.

Explicit response states:
`AskAlphaTranscriptStatus=NOT_LINKED`, `PlacementPreviewStatus=NOT_INDEPENDENTLY_VERIFIED`, `CreativeQualityStatus=HISTORICAL_FIXTURE_REVIEW_ONLY`, `AlphaCommissionStatus=NO_APPROVED_ALLOCATION`, `CreatorCompensationStatus=NO_APPROVED_ALLOCATION`, `PaymentCompleted=false`, `ProviderCalled=false`, `CampaignReady=false`, `CampaignDeliveryAuthorized=false`, `Delivery=NOT_SENT`.

## Acceptance checks

- The existing handoff service's synthetic Manila quote is PHP. Its attempted USD mock correlation is explicitly rejected; no FX conversion is assumed. A separate, fully synthetic USD-only object projection unit test exercises the positive verifier contract plus corrupt snapshot/image denial, attempted send, campaign/amount mismatch and multi-line quote rejection. **This is not yet a persisted USD end-to-end demonstration.**
- Disposable PostgreSQL bound-mock HTTP tests exercise operator scope, denial to advertiser, refusal of unknown handoff and unregistered operation.
- Frontend tests refuse forged/live authority and malformed amounts; the existing CI workflows must run on the **exact** new head: .NET 8/Docker, isolated financial/gateway PostgreSQL, Wedding Planner PostgreSQL and three-system browser.

## Still required for commercial demonstration

Persist and bind one consented Ask Alpha fictional transcript, generate a new Medellín automotive-parts advertisement, independently assess visual quality and true inventory dimensions, obtain collecting merchant/payee identity and explicit fee split approvals, securely connect a rotated Stripe **test** key and separate signed webhook, run actual test-mode receipt/refund/reconciliation checks, and receive independent Executive AI acceptance. No live checkout, campaign send, creator payout, or production release is authorized by this work.

**Newly verified integration gap (2026-10-15):** the service-produced six-market Economics fixtures use local market currencies (e.g., Manila PHP), while AGM-LAUNCH-010's accepted financial importer strictly requires an accepted USD quote. Do not relabel a PHP quote as USD or fabricate an FX rate. A separately approved USD quote path or approved conversion/settlement design is required before this new endpoint can return a genuine cross-system dossier for that fixture.
