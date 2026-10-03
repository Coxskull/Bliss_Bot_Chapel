# Engineering Contract: Local batch measurement

**Status:** the priority contract after the contact route audit.
It does not claim hosted acceptance, the 15-minute factory target, or
a stored census. Bliss hosted acceptance and Economics Phase 9 stay on
their own paths.

## Objective

Measure one read of the local prospect rows. Store the clock, the
process working set, an unrecorded cost, a retry, a partial failure,
and whether one prospect's message names another.

## AI capability

None. A clock, a process reading, and a name comparison are ordinary
software.

## Deterministic capability

`BatchMeasurement` classifies the stored rows. The service times the
existing library read and stores one `BatchMeasurements` row.

## Existing reusable assets

The prospect rows in PostgreSQL, the factory cost sentence that records
no dollar amount, and the operations console.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The local database already stores the prospects and their messages. A
hosted capacity suite and an invoice feed are not required for this
reading. A missing invoice stays unrecorded.

## Estimated usage cost

Zero model calls. No invoice is on file. Compute cost stays
`UNRECORDED`.

## Maximum cost

No new spend. A missing invoice is not filled by a guessed amount.

## Inputs

Stored prospects and their message texts. The process working set. The
idempotency key and the attempt, which is 1 or 2.

## Outputs

`GET /api/operations/measure` and `POST /api/operations/measure`. The
page is `/operations#/measure`.

## Cache policy

The measurement row is the record. The same idempotency key returns
that row and does not read the clock again. Message texts are not
copied into the measurement row.

## Failure handling

A missing prospect list is refused. A negative clock or working set is
refused. A third attempt is not configured. A blank name is a partial
failure and the other prospects remain. A message that names another
stored prospect is leakage. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The measurement write uses
the existing operator write policy. The contract cannot claim hosted
acceptance, send, or invent a price.

## Persistence

New `BatchMeasurements` rows in the existing Bliss database. One
idempotency key is one row. Prospect messages stay on the prospect row.

## Tests

`BatchMeasurementTests` and `BatchMeasurementApiTests`.

## Evidence

`docs/architecture/LOCAL-BATCH-EVIDENCE.md` and the PDF report beside
it.

## Acceptance criteria

- Elapsed time is the clock reading. The 15-minute target is not claimed.
- A positive working set is stored as bytes and is not a hosted
  capacity claim. A missing working set stays unrecorded.
- No invoice is on file. Cost stays unrecorded. Economics remains the
  only price authority.
- A blank name is a partial failure. The other prospect remains.
- Attempt 2 records one retry. Attempt 3 is refused.
- A message that names another prospect is leakage.
- The same idempotency key does not write a second row.
- Hosted acceptance, the factory target, and a stored census stay
  unclaimed.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Hosted Bliss acceptance. A claim that 100, 1,000, or 10,000 in-memory
checks are stored prospects. The 15-minute factory target. An invented
dollar amount. A live send. A new subscription. Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The measurement is a timed read and a
string comparison.

Can we reuse something? Yes. The prospect rows and the operations
console.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
