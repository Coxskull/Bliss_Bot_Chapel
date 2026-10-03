# Engineering Contract: Marketplace handoff

**Status:** the priority contract after the local batch measurement.
It hands one qualified advertiser and one stored creator to
`DeterministicRuleEvaluator`. It does not replace that evaluator.
Bliss hosted acceptance and Economics Phase 9 stay on their own paths.

## Objective

Record one repeatable handoff. The evaluator's status and score are
copied. A preserved advertiser stays out. The same pair is not a second
row. Another advertiser's row is not shown.

## AI capability

None. The accepted evaluator is ordinary software.

## Deterministic capability

`MarketplaceHandoff` calls `DeterministicRuleEvaluator.Evaluate`. It
does not multiply weights and it does not open a `BlissMatch`.

## Existing reusable assets

Stored prospects, stored creators, the active rule version, and the
operations console.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

Bliss already has the evaluator and the match-certificate writer. This
contract calls the evaluator and leaves certificate writing on that
existing path. A second compatibility service is not added.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend. A score is not a price.

## Inputs

A stored advertiser with a public source, a score of 100, and a state
other than preserved. A creator already stored in Bliss. The active
rule document.

## Outputs

`GET /api/operations/handoff` and `POST /api/operations/handoff`. The
page is `/operations#/handoff`.

## Cache policy

The handoff row is the record. The same tenant, creator, and source
return that row and do not call the evaluator again.

## Failure handling

A missing advertiser or creator is refused. A preserved advertiser, a
score below 100, or a missing public source does not call the evaluator.
A missing rule version is refused. A blank creator is refused. Delivery
remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The handoff write uses the
existing operator write policy. The contract cannot claim a win, send,
or invent a creator.

## Persistence

New `MarketplaceHandoffs` rows in the existing Bliss database. One
tenant, creator, and source is one row. Match certificates stay on
their existing table.

## Tests

`MarketplaceHandoffTests` and `MarketplaceHandoffApiTests`.

## Evidence

`docs/architecture/MARKETPLACE-HANDOFF-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- A qualified advertiser and a stored creator call
  `DeterministicRuleEvaluator` once.
- The stored status and score are the evaluator's result.
- A country name is not converted into a code.
- An approved result is still not a win, and a certificate is not opened.
- A preserved advertiser does not call the evaluator.
- The same pair does not write a second row.
- A reading for one advertiser does not include another advertiser's row.
- The public source URL is stored with the row.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

New matching arithmetic. Alpha Auto. A second compatibility service.
Opening a match certificate. A live send. An invented creator. Hosted
Bliss acceptance. Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The handoff is a stored pair and one
call to the accepted evaluator.

Can we reuse something? Yes. The evaluator, the stored creators, and
the prospect rows.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
