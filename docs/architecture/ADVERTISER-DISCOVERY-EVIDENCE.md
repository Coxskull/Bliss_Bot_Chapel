# Advertiser discovery evidence

Bounded Development increment after the ABC Pharmacy demonstration.
It records a prospect an operator found. It does not crawl the web,
buy contact enrichment, email a business, or open the rest of Fishing Fleet.

## What works

- The catalog lists the configured niches and the six initial markets.
- A prospect is stored only when the screen scores 100: known niche,
  initial market, a business name of at least three characters, and an
  absolute http or https source URL.
- A missing public source is rejected. The business name is not invented
  by the screen.
- The stored prospect names the likely buying roles and keeps the
  decision-maker `UNVERIFIED` and the contact tier `TIER_4`.
- One overlay is composited only after that score, and only onto a
  qualified source slice. The ABC Pharmacy reference still uses its four
  concept cards.
- Delivery stays `NOT_SENT`. The conversation still refuses to verify a
  named person.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **213 passed, 0 failed, 0 skipped**.

The new proofs are `OpportunityScreenTests` and
`ProspectDiscoveryApiTests`. The previous demonstration tests remain green.

The same path was exercised in the browser: an empty source is blocked,
Casa Verde at `https://example.com/casa-verde` scores 100, one Table
Concept is prepared, the message stays `NOT_SENT`, and the page refuses
to verify a person or invent a price. The discovery form and the
demonstration page remain readable at a phone width.

## PDF report

`docs/architecture/evidence/Alpha-Advertiser-Discovery-Report.pdf`

The seven-page report records the deterministic score, contract
boundary, discovery screen, unsent message preview, one-concept page,
guarded conversation, automated verification, and known limits.

## Limits

The screen does not search the internet. The operator supplies the name
and the public page. Failed screens are not saved. PostgreSQL persistence
of this library is still a later contract. No new subscription is added.
