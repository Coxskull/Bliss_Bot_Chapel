# Economics Engine — Integration Points

This module is a **callee**. Other Bliss Chapel systems keep their
authority. None of these call sites are implemented in the current
phase.

## Callers (future)

| Caller | May ask Economics for | Must not |
| --- | --- | --- |
| Wedding Planner | Explainable rate ranges and quote drafts for compatible inventory after matching | Invent prices, call an LLM as price authority, skip human quote approval |
| Campaign planning / placement UI | Display recommended range next to a planned slot | Persist a placement as if it were a contracted rate |
| Operator console | Inspect recommendations, observations, confidence, and quote versions | Edit historical observations in place |
| Media ledger / settlement (later) | Read **contracted** amounts and compensation-rule version from the accepted quote | Recompute market value or apply a hard-coded 20/80 split |

## Upstream providers (read-only)

Economics reads; it does not become source of truth for these records.

| Source | Used as |
| --- | --- |
| `Creator`, `CreatorPlatform` | Identity, platform, follower count (insufficient alone) |
| `ContentItem`, `AdInventorySlot` | Format, slot type, duration variable |
| `Advertiser`, `AdvertiserProgram`, `AdvertiserOpportunity` | Category, market country, campaign objective context. Opportunity `CommissionPercentage` / `FixedFee` are **not** inventory rates |
| `BlissMatch`, score components, eligibility | Compatibility and demographic-match signals. Matching remains scoring authority |
| `Campaign`, `CampaignPlacement` | What inventory was planned or purchased |
| Measurement (future) | Actual impressions, views, engagement, conversions |
| `DataProvenance` | Existing field-level source/confidence for creator demographics |

## Explicit non-integrations

| System | Rule |
| --- | --- |
| Deterministic matching | Must not call Economics. Compatibility ≠ price |
| Wedding Planner Phase 1 | Workspace/session/message only. No rate calls |
| n8n | May fetch public research into staging payloads. .NET writes observations. n8n is not the rate database |
| AI providers | May extract candidate benchmarks. Status stays `ESTIMATED` or `INFERRED` until a human or approved verifier marks `VERIFIED` |
| Alpha Auto order ledger | Forbidden. Different product and bounded context |
| Fishing Fleet | Discovery only. Economics does not crawl creators |

## Handshake sketch (Wedding Planner, later contract)

1. Advertiser: “I want to advertise women's footwear to women 18–34 in Manila.”
2. Wedding Planner captures the brief in durable workspace state.
3. Bliss matching identifies compatible creators / audiences.
4. Compatible `AdInventorySlot` rows are identified.
5. Economics evaluates market + creator + audience + inventory and
   writes a `RateRecommendation` (range, basis, confidence, factors,
   sources, rule versions).
6. Wedding Planner presents the explainable option/quote. It does not
   recalculate the numbers.
7. Advertiser approval, then Alpha approval where required.
8. Existing Bliss campaign-placement workflow proceeds.
9. Measurement and settlement later write `HistoricalPlacementEconomics`
   / `CampaignPerformanceEconomics` without mutating the original
   recommendation.

## Data-flow boundary

```
Matching ──compatibility──► Inventory
                              │
                              ▼
                     Economics Engine
                      │           │
                      ▼           ▼
              RateRecommendation  Quote(+versions)
                      │           │
                      ▼           ▼
              Wedding Planner   Human approval
                                    │
                                    ▼
                           Campaign Placement
                                    │
                                    ▼
                         Measurement / Ledger
                                    │
                                    └──► Historical economics (append-only)
```

## Guardrails already in this repository

- Vocabulary constants live in `Bliss.Domain/Economics/` with **no**
  default rates or 20/80 shares.
- `AdInventorySlot.DurationSeconds` is documented as a pricing
  *variable*, not a linear multiplier.
- `AdvertiserOpportunity` commission/fee fields are documented as
  affiliate terms, not inventory market value.
- Reserved SQL is not applied (`docs/sql/future-economics-schema.sql`).
- Architecture tests fail if an Economics HTTP API appears before a
  future contract, or if default compensation shares are compiled in.
