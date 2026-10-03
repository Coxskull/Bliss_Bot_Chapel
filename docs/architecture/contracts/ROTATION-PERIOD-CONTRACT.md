# Engineering Contract: Later rotation period

**Status:** the priority contract after the research ledger.
One later period stores the measured rotation pass. Bliss hosted
acceptance and Economics Phase 9 stay on their own paths. Phase 29
still records that its own pass has no later period configured.

## Objective

Store one later rotation period from the advertisers and slots already
on file. Keep the creator decision. Leave open slots empty. Record no
revenue amount and claim no census.

## AI capability

None. No model is called.

## Deterministic capability

`RotationPeriod` reads `RotationAbundance` for one balanced pair and
stores that reading. It does not place a new advertiser and it does not
rewrite a stored slot.

## Existing reusable assets

The rotation pass, the stored advertisers, the stored inventory slots,
and PostgreSQL.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is purchased.

## Build-versus-buy review

Phase 29 already measures one pass and leaves a later period
unconfigured. This contract stores that later period. It does not buy
inventory, invent advertisers, or add a price engine.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`. No revenue amount
is invented.

## Maximum cost

No new spend.

## Inputs

A balanced pair of 2, 4, or 6. The stored advertiser names. A creator
approval decision. The stored slot count. An idempotency key. A request
to fill an open slot or to supply a revenue amount is refused.

## Outputs

`GET /api/operations/rotation` and `POST /api/operations/rotation`. The
page is `/operations#/period`.

## Cache policy

The period row is the record. The same period key returns that row and
does not write again. The open count and the creator decision stay as
stored.

## Failure handling

A missing advertiser list is refused. A pair other than 2, 4, or 6 is
refused. More stored advertisers than the pair is refused. Filling an
open slot is refused. A revenue amount is refused. Delivery remains
`NOT_SENT`.

## Authorization

The reading is on the operations console. The period write uses the
existing operator write policy. The contract cannot send, invent a
census, or invent a price.

## Persistence

New `RotationPeriods` rows in the existing Bliss database. One period
key is one row. Inventory slots and advertiser rows stay where they
are.

## Tests

`RotationPeriodTests` and `RotationPeriodApiTests`.

## Evidence

`docs/architecture/ROTATION-PERIOD-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- One later period stores the measured theoretical, placed, and open counts.
- Creator approval is stored. A withheld decision is a separate row.
- The same key does not write a second row and does not fill the open slots.
- A request to fill a theoretical slot is refused.
- A revenue amount is refused. The revenue line says none is on file.
- Census stays false. Stored slots are not rewritten.
- Economics remains the only price authority.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Filling theoretical slots with advertisers that are not already stored.
A marketplace census. An invented revenue row. Rewriting the Phase 29
notice that its own pass has no later period. A live send. Hosted Bliss
acceptance. Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The period is the stored rotation
reading and the creator decision.

Can we reuse something? Yes. The rotation pass, the stored advertisers,
and the stored slots.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
