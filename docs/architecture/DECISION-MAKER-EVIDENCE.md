# Decision-maker evidence

Bounded Development increment after advertiser discovery. An operator
can record a person or a business contact only when a public http or
https source is supplied. The screen does not crawl, buy enrichment,
invent a name, or send a message.

## What works

- A dental prospect still resolves to the configured buying roles,
  including owner/practitioner, practice manager, and marketing manager.
- A person name without a public evidence URL is rejected and is not
  stored. Confidence stays `UNVERIFIED`.
- A directory entry alone stays `LOW` and is not used as a personal name.
- Official evidence plus a different corroborating professional source
  and a company marketing contact produces `MEDIUM` confidence and
  contact tier `TIER_2`.
- The same two sources plus a direct business email produce `HIGH`
  confidence and contact tier `TIER_1`.
- A company marketing contact with no person uses tier `TIER_3` and does
  not invent a name.
- A record older than 90 days is `STALE`. The page and the conversation
  do not use the personal name until it is recorded again.
- Personalization is allowed only for current `MEDIUM` or `HIGH`
  confidence. Delivery stays `NOT_SENT`.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **223 passed, 0 failed, 0 skipped**.

The new proofs are `DecisionMakerEvidenceTests` and
`DecisionMakerEvidenceApiTests`.

## Limits

The operator supplies the evidence. Alpha does not search the web or
check that the page still contains the name. No enrichment subscription
is added. Outreach eligibility and live delivery remain later contracts.
PostgreSQL persistence of this library remains later work.
