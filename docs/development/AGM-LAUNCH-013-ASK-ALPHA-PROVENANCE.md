# AGM-LAUNCH-013 — Ask Alpha reference provenance preflight

**Scope:** Repository-only implementation after merged PR #218. Read-only, no schema changes.
**Status:** Pending exact-head CI and independent review.

## Purpose
The Ask Alpha demonstration store already maintains an optional Wedding Planner workspace UUID, Wedding Planner session UUID, and Economics quote UUID. Those are mutable demonstration metadata, not a signed conversation transcript or customer consent. AGM-LAUNCH-013 introduces a narrow provenance check that can identify when these *existing source references* agree with a separate, authorized campaign handoff. It does not link/export raw conversation text or infer commercial permission.

### Endpoint
`GET /api/wedding-planner/workspaces/{workspaceId}/handoff/plans/{handoffId}/ask-alpha-preflight?slug={boundedSlug}`

Access control: the existing Wedding Planner workspace access check is performed before querying the handoff or reading the demonstration source. A foreign advertiser receives 404. The existing handoff must be PLANNED/NOT_SENT and its campaign DRAFT, with matched placement, run, Economics quote, version, quote line and immutable handoff snapshot. An accepted Economics amount, currency, payment, or settlement policy is **not** inferred from this check.

The requested demonstration slug is strict lowercase/dash syntax, max 96 characters. The source reference is considered a **metadata match** only when:
- Illustrative, non-suppressed demonstration, with at least one VISITOR turn and one ASSISTANT turn;
- Demonstration's existing `WeddingPlannerWorkspaceId` is the same authorized workspace;
- Demonstration's existing `WeddingPlannerSessionId` resolves to an actual stored session with the exact same workspace and advertiser;
- Demonstration's existing `EconomicsQuoteId` equals the quote already bound to the handoff.

Only counts of visitor/assistant turns are returned, **not body text, names, contact roads, message timestamps or transcript hashes**.

### States
A match returns `SOURCE_REFERENCES_MATCH_CONSENT_REQUIRED`. Any missing/mismatched source returns `NOT_LINKED` with zero counts. In **all** cases:

`ConsentStatus=UNRECORDED`, `TranscriptLinked=false`, `TranscriptExportAuthorized=false`, `ProspectOutreachAuthorized=false`, `PaymentAuthorized=false`, `CreatorPayoutAuthorized=false`, `CampaignReady=false`, `CampaignDeliveryAuthorized=false`, `Delivery=NOT_SENT`.

The original source is mutable; the status is not a stable signed evidence package and cannot be used for production acceptance. The UI provides a read-only source-slug check alongside the existing funding-currency diagnostic.

### Tests and controls
- Persist a synthetic Ask Alpha demonstration and a same-tenant Wedding Planner session; verify exact source metadata matching through scoped HTTP.
- Verify foreign advertiser denial, unknown handoff/slug handling and invalid-slug refusal.
- Verify mismatch of workspace, session advertiser, quote ID, suppression and missing conversation turns returns NOT_LINKED with no cross-tenant counts.
- Verify the match never changes transcript consent, financial authority, payout or delivery claims.
- Require exact-head .NET 8/Docker, financial/gateway PostgreSQL, Wedding Planner PostgreSQL and browser journeys success.

### Remaining work
A separately designed, explicit opt-in and revocation-aware consent workflow, immutable/scoped transcript attachment with signed provenance, creator and advertiser rights review, actual visual acceptance, approved USD quote or documented FX policy, test-mode payment evidence and independently approved fees/settlement are **not implemented by this PR**. Do not send campaigns or create real payment/payout operations.
