# Engineering Contract: Source media coverage

**Status:** the priority contract after the player-served picture.
It does not open discovery, send, or a download. Bliss hosted
acceptance and Economics Phase 9 stay on their own paths.

## Objective

Read market coverage and the fuel gauge from stored source slices.
Count a market only when the qualified slice has a fingerprint and
provenance.

## AI capability

None. A fingerprint, a duplicate status, and a stored market name are
ordinary software.

## Deterministic capability

The coverage reading and the existing fuel gauge.

## Existing reusable assets

`SourceMediaRules`, the stored clip rows, and the operations console.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The library already stores the fingerprint, the duplicate status, the
quota credit, and the provenance. PostgreSQL and the demonstration
store already hold those rows. A media suite is not required.

## Estimated usage cost

Zero model calls. No download. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend. A missing market is not filled by a purchase.

## Inputs

Stored slices: market, country, status, quota credit, SHA-256, frame
hash, and provenance.

## Outputs

`GET /api/operations/fuel` and `/operations#/fuel`. Market rows are
counts of stored qualified slices. Withheld rows explain a missing
fingerprint, provenance, quota credit, or market.

## Cache policy

The reading uses the stored rows. It does not refetch a file or invent
a city.

## Failure handling

A missing clip list is refused. A qualified slice without a fingerprint
or provenance is withheld and is not coverage. A blank market is not
named. A duplicate is not coverage. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. It cannot upload a podcast,
qualify a slice by itself, or send.

## Persistence

No new table. The clip rows already stored by the library are the
evidence.

## Tests

`SourceMediaCoverageTests` and `SourceMediaCoverageApiTests`.

## Evidence

`docs/architecture/SOURCE-MEDIA-COVERAGE-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- A fingerprinted, provenanced, qualified slice covers only its stored
  market.
- A duplicate does not add coverage.
- A missing fingerprint, provenance, quota credit, or market is withheld.
- The fuel numbers are the stored clip counts.
- A calendar week is not claimed.
- The reading says it is not a census.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Copyrighted podcast downloads. Self-certified city counts. Crawlers.
Live send. A new subscription. Hosted Bliss acceptance. Economics
Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The count is a loop over stored rows.

Can we reuse something? Yes. The library fingerprints and the fuel gauge.

Do we already pay for this capability? The application is
`FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
