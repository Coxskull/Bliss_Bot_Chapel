# Engineering Contract: Marketplace metrics

**Status:** the contract after the coverage week.
One marketplace reading stores the measured advertiser count, the
measured creator count, and the stored slot count. A revenue amount is
not on file. The balance page still records that revenue and inventory
are not recorded there.

## Objective

Store one marketplace reading from the rows already on file. Leave a
revenue amount unrecorded. Leave the stored slots unchanged.

## AI capability

None. No model is called.

## Deterministic capability

`MarketplaceMetrics` reads the stored advertiser names, creator names,
slot count, and Economics revenue-row count. It does not add a slot and
it does not copy an amount.

## Existing reusable assets

The advertiser rows, the creator rows, the inventory slots, the
Economics history tables, and PostgreSQL.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row is purchased.

## Build-versus-buy review

The balance page already counts stored advertisers and creators and
leaves revenue and inventory unrecorded. This contract stores one
reading of those measured counts plus the stored slot count. It does
not buy a marketplace report.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend.

## Inputs

The stored advertisers, creators, and inventory slots. The count of
Economics historical rows. A reading key. A request to record a revenue
amount is refused. A request to add a slot is refused.

## Outputs

`GET /api/operations/metrics` and `POST /api/operations/metrics`. The
page is `/operations#/metrics`.

## Cache policy

The reading row is the record. The same reading key returns that row
and does not write again. The stored counts stay as recorded.

## Failure handling

A missing advertiser list, creator list, or history is refused. A
negative slot count is refused. A supplied revenue amount is refused.
Adding a slot is refused. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The write uses the existing
operator write policy. The contract cannot record a revenue amount,
add a slot, send, or claim a census.

## Persistence

New `MarketplaceMetricReadings` rows in the existing Bliss database.
One reading key is one row. Economics remains the only price authority.

## Tests

`MarketplaceMetricsTests` and `MarketplaceMetricsApiTests`.

## Evidence

`docs/architecture/MARKETPLACE-METRICS-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- One reading stores the measured advertiser count, creator count, and slot count.
- A revenue amount is not recorded. A stored Economics row is counted and its amount is not copied.
- The same key does not write a second row and does not raise the counts.
- The balance page still says revenue is not recorded and inventory is not recorded.
- The stored slot count is unchanged.
- Census stays false. Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

An invented revenue amount. Filling a theoretical slot. A marketplace
census. Rewriting the balance notice. Automatic repricing. Settlement.
A live send. Hosted Bliss acceptance.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The reading is the stored row counts.

Can we reuse something? Yes. The advertiser, creator, slot, and
Economics tables.

Do we already pay for this capability? The application and the local
database are `FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
