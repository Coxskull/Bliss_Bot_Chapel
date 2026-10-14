# AGM-LAUNCH-010 — cancellation review policy proposal (R9)

Status: SUBMITTED FOR REVIEW. This proposal is not owner-approved policy or executable release authority.
Stripe: UNVERIFIED / OWNER DECISION REQUIRED. Development remains mock-only.

## Proposed decisions

Require two distinct authenticated administrators, each with an explicit current merchant/module/campaign grant, to review the same frozen evidence version. An operator can annotate but cannot count toward approval. A reviewer cannot approve their own requested resolution. These are proposed Bliss rules; previous Alpha Auto approvals do not automatically approve this proposal.

A decision must bind operation, financial evidence fingerprint, current source quote/approval version, reason, proposed action, stable actor identity and idempotency key. Scope comes from server grants. Stale evidence invalidates the proposed decision; exact retries return the original record. Two approvals cannot be manufactured through two browser sessions with the same identity. Decisions and supersession records are append-only.

## Case handling

| Case | Proposed handling | Financial effect now |
| --- | --- | --- |
| Uncaptured canceled operation | Maintain hold; investigate identity, source approval and chronology. Do not recreate payment automatically. | None |
| Captured operation followed by cancellation | Preserve original capture/journal; review contradictory observations. Any refund uses the separately scoped refund workflow and original attribution. | None |
| Aged unresolved dispatch | Verify provider identity and existing retry key; do not create a second payment on uncertain evidence. | None |
| Changed evidence after review | Retain historical decisions; require review of the new version. | None |
| Duplicate/reordered provider observation | Preserve deduplication and captured truth; arrival order alone is not authoritative provider chronology. | None |

Review acknowledgment and money movement are separate. Even two approvals must not clear a hold, dispatch, capture, refund, allocate commissions or change original journals. A future release command needs a separately reviewed design, explicit allowed actions, fresh source/provider evidence and transactional fencing against concurrent financial writers. Its funds scope and owner acceptance remain undecided. No release endpoint is implemented in R9.

## Acceptance before decision implementation

Test distinct-identity quorum, self-review rejection, scoped admin isolation, stale evidence, source revocation, changed action under the same retry key, concurrent approvals, immutable supersession and zero financial mutations. Current private snapshots do not contain current public Economics approval or external provider state: those must be added/checked in a later reviewed decision workflow. Do not represent current notes as approvals.

## Operator screen and note history delivered in R9

A separate `bliss-frontend` local React application contains synthetic cases for Bliss Bot, Creator Academy and Wedding Planner. It has no backend/provider connection and stores local demonstration annotations in memory only. Reload clears them. No authentication or persistent operator acceptance is claimed. It does not accept credentials. Hosted publication and API integration remain pending.

The backend adds `GET /api/financials/mock-review/cases/{operationId}/notes?limit=25&after={noteId}`. Limits are 1–100. Current role and registered scope gates apply before cursor lookup. The server resolves a cursor only within that case, then uses `(recorded_at,id)` keyset ordering. Missing/cross-case cursors return validation errors; out-of-scope cases are forbidden. The final `nextAfter` is explicitly null. Notes are immutable, but pages do not constitute a multi-request database snapshot: refresh to see concurrent additions whose timestamps sort before a prior cursor. The case preview's 200-note bound remains; this route allows bounded traversal beyond it.

## Owner review requested

Review the proposed quorum, self-review restriction, evidence binding and separate release boundary. Decide legal merchant/payees/fee allocations independently; USD remains confirmed. Actual Stripe activation remains prohibited pending verified account/onboarding, replacement of the exposed test secret and explicit authorization.
