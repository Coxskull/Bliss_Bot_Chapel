# Engineering Contract: Coverage week

**Status:** the contract after the hosted acceptance reading.
One calendar week stores the measured source-media coverage. The fuel
page still records that its own pass has no calendar week configured.

## Objective

Store one coverage week from the qualified slices already on file. Leave
a missing market unlisted. Leave the fuel gauge unchanged.

## AI capability

None. No model is called.

## Deterministic capability

`CoverageWeek` reads `SourceMediaCoverage` and stores that count. It
does not add a market and it does not change a slice.

## Existing reusable assets

The source-media library, the coverage reading, and PostgreSQL.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is purchased.

## Build-versus-buy review

Coverage already counts stored qualified slices. This contract stores
one week of that reading. It does not buy media or invent a market.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend.

## Inputs

The stored slices. A week key. A request to add a missing market is
refused.

## Outputs

`GET /api/operations/week` and `POST /api/operations/week`. The page is
`/operations#/week`.

## Cache policy

The week row is the record. The same week key returns that row and does
not write again. The stored qualified count stays as recorded.

## Failure handling

A missing slice list is refused. A blank market is refused. A qualified
count without its stored market is refused. Adding a missing market is
refused. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The week write uses the
existing operator write policy. The contract cannot add a market, send,
or claim a census.

## Persistence

New `CoverageWeeks` rows in the existing Bliss database. One week key is
one row. Slice files stay on disk.

## Tests

`CoverageWeekTests` and `CoverageWeekApiTests`.

## Evidence

`docs/architecture/COVERAGE-WEEK-EVIDENCE.md` and the PDF report beside
it.

## Acceptance criteria

- One week stores the measured qualified count, market count, and fuel status.
- A missing market is not added.
- The same key does not write a second row and does not raise the count.
- The fuel reading is unchanged after the week is stored.
- Census stays false. Slices are not changed.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Inventing a market for an empty week. A media download. A census. A
calendar of future weeks. Rewriting the coverage notice that its own
pass has no week configured. A live send. Hosted Bliss acceptance.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The week is the stored coverage
reading.

Can we reuse something? Yes. The coverage reading and the stored slices.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
