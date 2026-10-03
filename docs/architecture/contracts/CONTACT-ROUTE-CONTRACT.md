# Engineering Contract: Contact route audit

**Status:** the priority contract after bounded advertiser discovery.
It does not open a transmitter, a second CRM, or enrichment. Bliss
hosted acceptance and Economics Phase 9 stay on their own paths.

## Objective

Read each stored prospect's contact route from evidence, freshness,
suppression, and the preview adapter. Record an explicit transmission
request once, and do not transmit it.

## AI capability

None. A stored road, a stale date, and an idempotency key are ordinary
software.

## Deterministic capability

`ContactRouteAudit` classifies the route and accepts or refuses the
transmission request. `DeliveryPolicy` and `PreviewDeliveryAdapter`
stay the only preview path. They still do not transmit.

## Existing reusable assets

Decision-maker evidence, contact roads, suppression, the preview
adapter, and the operations console.

## Required provider

None. The route adapter is `preview-adapter` or `none`. No vendor
transmitter is added.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The prospect row already stores evidence, freshness, roads, and preview
decisions. A messaging suite and an enrichment provider are not
required.

## Estimated usage cost

Zero model calls. No transmission. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend. A missing person is not filled by a purchase.

## Inputs

Stored prospects: name, suppression, freshness, evidence URL, road
count, and whether a preview was prepared. A transmission request
supplies explicit words and an idempotency key.

## Outputs

`GET /api/operations/routes` and `/operations#/routes`. `POST
/api/operations/routes/transmission` writes one `ContactRouteAudits`
row or returns the row already stored.

## Cache policy

The audit row is the record of the request. The same idempotency key
returns that row and does not insert another. The evidence page is not
fetched.

## Failure handling

A blank name is not listed. Suppression comes before the adapter. Stale
evidence is not used. A missing public evidence URL does not invent a
person. A missing road is not invented. Words that are blank, or an
unusable key, are refused and not stored. An address in the recorded
words is not repeated on the reading. Delivery remains `NOT_SENT`.

## Authorization

The reading is on the operations console. The transmission write uses
the existing operator write policy. The contract cannot send, buy a
contact, or name a person who is not already stored.

## Persistence

Existing prospect rows for the route. New `ContactRouteAudits` rows for
the explicit request. One idempotency key is one row.

## Tests

`ContactRouteAuditTests` and `ContactRouteApiTests`.

## Evidence

`docs/architecture/CONTACT-ROUTE-EVIDENCE.md` and the PDF report beside
it.

## Acceptance criteria

- A suppressed prospect stays suppressed. The adapter does not run.
- Stale evidence is not used, including when a preview was prepared
  earlier.
- A missing public evidence URL does not invent a person.
- A route without a stored road does not invent one.
- A current preview names `preview-adapter` and transmission
  `NOT_SENT`.
- Evidence with a road and no preview stays withheld.
- An explicit transmission request is stored once. The same key does
  not write a second row.
- Blank words and an unusable key are refused.
- The reading says a public address is not permission to send and that
  it is not a census.
- Green does not send. Delivery remains `NOT_SENT`.

## Out of scope

Sending because a public email, page, or messaging number exists.
Green as send. A second CRM or messaging suite. Contact enrichment.
A crawler. Hosted Bliss acceptance. Economics Phase 9.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The route is a classification of
stored fields, and the audit is one unique key.

Can we reuse something? Yes. The evidence, the roads, the suppression
flag, and the preview adapter.

Do we already pay for this capability? The application is
`FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
