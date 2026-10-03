# Engineering Contract: Economics Phase 9 acceptance

**Status:** owner acceptance of the implemented Economics Phase 9
contract, and the reading that follows it. Bliss hosted acceptance
stays on its own path.

## Objective

Record the owner's acceptance of Economics Phase 9 and cite the
historical actuals already stored. An empty history stays unrecorded.
A pricing rule is not changed. A settlement is not created.

## AI capability

None. No model is called.

## Deterministic capability

`EconomicsPhaseAcceptance` records one acceptance and cites the stored
placement or campaign actual. It does not calculate a new rate.

## Existing reusable assets

The Economics Phase 9 tables, the stored recommendations, and
PostgreSQL.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is purchased.

## Build-versus-buy review

Phase 9 already appends historical actuals. This contract records the
owner acceptance and reads that history. It does not buy a pricing
service or a settlement provider.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`. No amount is
invented when the history is empty.

## Maximum cost

No new spend.

## Inputs

Phase 9. The stored historical placement and campaign rows. A request
to reprice or to settle is refused.

## Outputs

`GET /api/operations/acceptance` and `POST /api/operations/acceptance`.
The page is `/operations#/acceptance`.

## Cache policy

The acceptance row is the record. The same phase key returns that row
and does not write again. The history line is read from the stored
actuals at the time of the request.

## Failure handling

A phase other than 9 is refused. Repricing is refused. Settlement is
refused. An amount supplied when no historical row is stored is
refused. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The acceptance write uses the
existing operator write policy. The contract cannot change a
recommendation, send, or create a settlement.

## Persistence

New `EconomicsPhaseAcceptances` rows in the existing Bliss database.
One phase key is one row. Historical placement rows, campaign
performance rows, recommendations, quotes, and placements stay where
they are.

## Tests

`EconomicsPhaseAcceptanceTests` and `EconomicsPhaseAcceptanceApiTests`.
The existing `EconomicsPhase9HistoricalTests` and
`EconomicsPhase9ApiTests` remain the Phase 9 proofs.

## Evidence

`docs/architecture/ECONOMICS-PHASE-ACCEPTANCE-EVIDENCE.md` and the PDF
report beside it. The original Phase 9 pack remains
`docs/economics/PHASE-9-EVIDENCE.md`.

## Acceptance criteria

- The owner acceptance is stored once for Economics Phase 9.
- The same key does not write a second acceptance.
- An empty history says no actual is on file.
- A stored placement actual is cited with its contracted amount and currency.
- The linked recommendation target stays as it was.
- A request to apply history to the price is refused.
- A request to create a settlement is refused.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Automatic repricing, model training, settlement, invoicing, payment,
and payout. Bliss hosted acceptance. A live send. Wedding Planner
Phases 2–9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The acceptance is one stored row and
the history line is a reading.

Can we reuse something? Yes. The Phase 9 tables and the stored
recommendations.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
