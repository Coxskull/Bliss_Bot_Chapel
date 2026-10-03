# Engineering Contract: Bounded advertiser discovery

**Status:** the priority contract after source-media coverage.
It does not open a crawler, contact enrichment, or a send. Bliss hosted
acceptance and Economics Phase 9 stay on their own paths.

## Objective

Count stored prospects that have a public source URL, and refuse a
second write of the same source or the same business. The operator
supplies the name and the page.

## AI capability

None. A name, a public URL, a duplicate, and a stored count are
ordinary software.

## Deterministic capability

`OpportunityScreen` scores the road. `AdvertiserDiscovery` accepts,
preserves, or refuses the write and reads the stored rows.

## Existing reusable assets

The discovery screen, `ProspectMemories`, and the operations console.
No new table.

## Required provider

None.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The operator already supplies a public page. PostgreSQL already stores
the prospect. A crawler, a search API, and an enrichment suite are not
required.

## Estimated usage cost

Zero model calls. No crawl. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend. A missing business is not filled by a purchase.

## Inputs

The operator's business name, market, niche, and public http or https
URL. The reading inputs are the stored prospect rows.

## Outputs

`POST /api/demonstrations/discover` writes one row or returns the row
already stored. `GET /api/operations/discovery` and
`/operations#/discovery` return the measured counts and the source URLs.

## Cache policy

The prospect row is the cache. A repeated source URL reads that row
and does not insert another. The page is not fetched.

## Failure handling

A blank name, an unusable name, or a missing public URL is refused and
is not stored. A duplicate source or a duplicate business returns the
stored row and writes nothing. A score below 100 is preserved and
receives no demonstration. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The write uses the existing
operator write policy. The contract cannot crawl, buy a contact, or send.

## Persistence

Existing `ProspectMemories` rows. One slug is one row. A duplicate
source does not insert a second row. Media files stay on disk.

## Tests

`AdvertiserDiscoveryTests` and `AdvertiserDiscoveryApiTests`.

## Evidence

`docs/architecture/ADVERTISER-DISCOVERY-REGISTER-EVIDENCE.md` and the
PDF report beside it. The earlier screen evidence remains
`ADVERTISER-DISCOVERY-EVIDENCE.md`.

## Acceptance criteria

- A public source is written once.
- The same source URL, including a trailing slash and a different
  name, does not create a second prospect.
- The same business name does not create a second prospect.
- A blank name and an unusable name are refused.
- A named public source below 100 is preserved.
- The reading counts stored rows, scored rows, and preserved rows.
- A prospect without a public source is withheld and is not counted.
- The reading says it is not a census and that a crawler did not run.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Crawlers and bulk scraping. An invented prospect census. Contact
enrichment. Decision-maker routing. Live send. A new subscription.
Hosted Bliss acceptance. Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The count is a loop over stored rows,
and the duplicate check is a string comparison.

Can we reuse something? Yes. The discovery screen and `ProspectMemories`.

Do we already pay for this capability? The application is
`FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
