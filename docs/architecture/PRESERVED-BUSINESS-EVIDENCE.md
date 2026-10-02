# Preserved business

Phase 8 of the acquisition tracker. A legitimate business is a name of
at least three characters plus an absolute http or https source URL.
That record is kept when the road scores below 100. Alpha manufactures
no demonstration, records no decision-maker, and sends nothing.

A missing name or a missing public URL is still rejected and is not
stored. The score of 100 remains the gate for one demonstration.

## What works

- Restaurant Puerto Azul in Quito, recorded from
  `https://example.com/puerto-azul`, scores 75. The niche and the name
  and the public URL each add 25. Quito is outside the initial six
  cities, so the market point is withheld.
- The stored state is `PRESERVED`. Concepts stay empty. Decision-maker
  confidence stays `UNVERIFIED`. Delivery stays `NOT_SENT`.
- Buying roles stay the restaurant catalog, including Owner.
- Produce returns 400: the road score is below 100 and no demonstration
  is manufactured.
- A decision-maker submission returns 400. Nothing is sent.
- An unknown niche such as “other” stores the business with an empty
  buying-role list. Alpha does not invent the default owner list.
- A blank market is stored as “Outside the initial markets.”
- The preserved page and the unsent preview say that no demonstration
  was manufactured and nothing was sent.

Casa Verde at score 100 and ABC Pharmacy stay on the demonstration
path. Puerto Azul and `example.com` are fixtures. They are not a claim
about a real business.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **225 passed, 0 failed, 0 skipped**.

The new proofs are the outside-market case in `OpportunityScreenTests`
and `PreservedBusinessApiTests`.

## Browser path

On Source Media, choose Outside the initial markets, enter Quito, and
score Puerto Azul. The notice names the score, the preserved state, and
`NOT_SENT`. The prospect row has no produce button. The preserved page
and the preview repeat that nothing was manufactured and nothing was
sent.
