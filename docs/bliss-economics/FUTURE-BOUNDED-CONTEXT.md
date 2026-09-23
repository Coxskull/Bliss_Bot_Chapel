# Bliss Economics & Rate Intelligence Engine™

Status: **future bounded context**. Documented so Alpha can implement it
later without interrupting accepted Bliss Phases 1–19 or Wedding Planner
Phases 1–9.

This file is **not** an implementation authorization. Do not add tables,
pricing services, quote APIs, or Wedding Planner price invention until a
later engineering contract explicitly proceeds.

Working module name: **Bliss.Economics** (conceptual). Product name:
**Bliss Economics & Rate Intelligence Engine™**.

## Purpose

Provide the economic intelligence layer that matching, inventory, the
Wedding Planner, campaign planning, measurement, and settlement can
**call** when Alpha needs to estimate, recommend, explain, or quote the
value of advertising inventory.

It does **not** replace:

- Bliss matching (eligibility and compatibility)
- The Wedding Planner (creative and campaign planning workspace)
- Campaign placement (planning bind; `PLANNED` is not activation)
- Creator or advertiser systems of record
- Wedding Planner Phase 9 measurement/learning (advisory, human-attested
  aggregates only; not a price engine)

Advertisers purchase **inventory and audience exposure**. Duration is one
variable. The engine must not collapse to “X dollars per minute.”

There is **no** universal Alpha advertising price. Economics can differ
materially by country, city/metro, audience, actual views, engagement,
demographics, geography, platform, format, duration, inventory position,
historical performance, advertiser category, local economics, and timing.

A creator in Manila is not automatically valued the same as a creator in
Medellín, Panama City, Santo Domingo, Kuala Lumpur, or Jakarta because
subscriber counts look similar. Launch cities are **data**, never
hard-coded calculator constants.

## Architectural position

```
CREATOR + CONTENT + AUDIENCE
        ↓
BLISS MATCHING  (compatibility — not price)
        ↓
COMPATIBLE INVENTORY
        ↓
ECONOMICS & RATE INTELLIGENCE  (this module)
        ↓
THE WEDDING PLANNER™  (asks; does not invent prices)
        ↓
QUOTE / CAMPAIGN PLAN
        ↓
HUMAN APPROVAL
        ↓
CAMPAIGN PLACEMENT  (existing PLANNED bind)
        ↓
MEASUREMENT  (future delivery measurement + WP Phase 9 advisory learning)
        ↓
LEDGER / SETTLEMENT  (future media ledger; not Alpha Auto order finance)
        ↓
HISTORICAL ECONOMIC DATA
        ↺ feeds future rate intelligence
```

.NET remains application authority. PostgreSQL/Supabase remains permanent
truth. n8n may run async research jobs. AI may research, extract,
classify, and recommend. AI is **not** the database and must not silently
control prices.

## Governance that later implementation must keep

| Rule | Meaning |
| --- | --- |
| No universal price | Country, city/metro, audience, inventory, and category are first-class inputs |
| Ranges, not false precision | Low / target / high plus confidence until Alpha history is substantial |
| Explainable | Every material recommendation cites factors, rule/model version, and sources |
| Provenance | Benchmarks store source, geography, metric, value/range, currency, dates, confidence, VERIFIED / ESTIMATED / INFERRED / UNKNOWN |
| Version history | New observations and recommendations are append-only; never overwrite |
| Market value ≠ settlement | Quote, contracted amount, Alpha share, creator share, and other splits are separate, versioned concepts |
| No 20/80 hard-code | Compensation splits are configurable `CompensationRuleVersion` data |
| No dollars-per-minute-only | Duration may inform a model; it is not the product |
| No invented rates | AI research without stored provenance cannot become a durable rate |
| Cities are data | Initial markets (Santo Domingo, Panama City, Medellín, Manila, Kuala Lumpur, Jakarta) seed later; the engine keys on country/city codes |

## Explicit non-goals for this documentation pass

- Do not implement entities, migrations, APIs, or UI now
- Do not insert a Wedding Planner Phase 10; Master Blueprint V1.1 ends at
  measurement/learning
- Do not mutate existing `Campaign`, `CampaignPlacement`, matching scores,
  or Wedding Planner Phase 8/9 records to store prices
- Do not treat Wedding Planner Phase 9 human-attested aggregates as
  verified delivery measurement or as a rate
- Do not reuse Alpha Auto order ledgers as the media economics ledger
- Do not activate, reserve, deliver, or pay from this module

## Integration points (read-only callers later)

Later contracts should add a **query/command façade** owned by
`Bliss.Economics`. Other modules call it; they do not embed pricing
formulas.

| Caller | When it asks | What it must not do |
| --- | --- | --- |
| Wedding Planner | After compatible match + inventory are identified; advertiser states audience/market/objective | Invent or cache prices in Brand DNA, creative, QA, handshake, or measurement-learning documents |
| Bliss matching | Never as a match score. Optional later: display economic context **after** compatibility | Score matches by price or rewrite eligibility |
| Campaign placement | Optional quote pin on a **new** economics FK; existing bind stays planning-only | Gate `PLANNED` bind on a quoted price in current phases |
| WP Phase 9 measurement/learning | Later: pin accepted learning reports as **one** historical factor with attestation limits | Treat Phase 9 as verified impressions or causal CPA |
| Future delivery measurement | Actual impressions/views/engagement become `HistoricalPlacementEconomics` | Overwrite the original recommendation |
| Future media ledger | Contracted amount, Alpha/creator compensation, FX, settlement | Collapse market value into one compensation field |
| n8n | Async research fetch/retry into `ResearchSource` + `MarketBenchmarkObservation` | Become system of record |

Wedding Planner example (future, not current UI):

1. Advertiser: women’s footwear, women 18–34, Manila
2. Bliss matching: compatible creators/audiences
3. Compatible inventory identified (`PRE_ROLL`, `MID_ROLL`, overlays,
   `SPONSORED_SEGMENT`, etc.)
4. Economics engine: market + creator + audience + inventory
5. Explainable rate range returned
6. Wedding Planner presents option/quote
7. Human advertiser/Alpha approval
8. Existing Bliss placement workflow continues

## Pricing models (data, not a single formula)

`PricingModel` rows describe allowed bases. A recommendation selects one
or a hybrid **versioned composition**, never a hidden constant.

| Code (illustrative) | Product meaning |
| --- | --- |
| `CPM` | Cost per 1,000 impressions / listens / views |
| `CPV` | Cost per view where that unit is the purchased exposure |
| `FLAT_PLACEMENT` | Flat-rate placement |
| `FIXED_CAMPAIGN` | Fixed campaign rate |
| `SPONSORSHIP` | Sponsorship rate |
| `HOST_READ` | Host-read / integrated segment |
| `CPA` / `CPL` / `CPS` | Performance economics where contracted |
| `HYBRID` | Explicit weighted or sequenced combination of the above |

Duration (15s, 30s, 60s, 5–15 minutes, etc.) is stored on the request and
on inventory metadata (`AdInventorySlot.DurationSeconds` already exists).
**Do not** assume 10 minutes = 10 × one minute.

Existing Bliss slot types remain inventory **identity**, not prices:

`PRE_ROLL`, `MID_ROLL`, `POST_ROLL`, `LOWER_THIRD`, `PERIMETER_OVERLAY`,
`CORNER_OVERLAY`, `ROTATING_OVERLAY`, `SPONSORED_SEGMENT`.

## Creator and audience economics (factors, not multipliers)

Subscriber/follower count is one input. 100,000 vs 5,000 vs 1,000
subscribers must not auto-scale 20×. Consistent with Bliss: **audience
demographics and advertiser-market compatibility can outweigh topic
similarity** — and they can outweigh raw follower count.

Later snapshots should be able to hold, when known (nullable; UNKNOWN ≠ 0):

- Subscribers / followers
- Average views or listens per content item
- Daily / weekly / monthly views or listens
- Historical reach
- Engagement rate
- Retention where available
- Audience age, gender, geography, language
- Platform, content format, publishing frequency
- Historical campaign performance (Alpha-owned, versioned)

## Market economics (geography as data)

`MarketEconomicProfile` keys on country code + optional city/metro code +
effective interval. Typical contents (all sourced, versioned):

- Typical digital advertising costs
- Local CPM / influencer / podcast / social benchmarks
- Local purchasing power indices (labeled ESTIMATED unless verified)
- Advertiser acquisition economics by category
- Currency code; FX observations as **separate versioned rows**
- Local competition / audience scarcity notes
- Historical Alpha transaction aggregates for that market (when they exist)

Do **not** compile Manila, Medellín, or any city into C# `switch` pricing.

## Recommendation shape (future API)

Inputs (pinned ids + declared campaign context):

Creator → content item → audience snapshot → inventory slot → duration →
country/city → advertiser category → campaign objective → current
benchmarks → historical Alpha data.

Output (immutable `RateRecommendation`):

| Field | Rule |
| --- | --- |
| Low / target / high | Same currency; range required when confidence is not HIGH |
| Pricing basis | One `PricingModel` code or explicit hybrid document |
| Estimated impressions | Nullable; method and source required if present |
| Primary factors | Structured `RateRecommendationFactor` rows, not prose-only |
| Market | Country + optional metro |
| Benchmark as-of | Dates of cited observations |
| Confidence | UNKNOWN / LOW / MEDIUM / HIGH — never implied certainty |
| Supporting sources | FKs to `ResearchSource` / `MarketBenchmarkObservation` |
| Rule/model version | `PricingRuleVersionId` required |

Example **concept** (not a seeded rate): estimated range $180–$260,
recommended quote $225, confidence MEDIUM, because of attested average
views, Manila concentration, demographic overlap, mid-roll, 60-second
host-read, comparable-market CPM range, and **limited Alpha history**.

## Learning from Alpha history

Public research bootstraps. Alpha history should eventually dominate.

Preserve, as separate versioned facts, never as one mutable blob:

Quoted rate → advertiser response → accepted/declined → final negotiated
rate → inventory purchased → actual impressions/views →
engagement/conversions where available → creator compensation → Alpha
compensation → campaign performance.

When benchmarks change, insert new observation versions. Historical
recommendations remain reproducible from the data and
`PricingRuleVersion` used at that time.

## Compensation vs market value

Keep these ledgers conceptually separate:

1. Inventory market value (recommendation)
2. Advertiser quote (`Quote` / `QuoteVersion`)
3. Final contracted amount
4. Alpha compensation
5. Creator compensation
6. Other authorized participant compensation
7. Settlement

Related, not identical. A historical example of $100 with 20% Alpha / 80%
creator is **illustrative only**.

## Candidate durable records

Names may change at implementation time. PostgreSQL remains the store.
All new FKs should use `ON DELETE RESTRICT`. Unique natural keys plus
`(SourceSystem, IdempotencyKey)` on recommendation/quote/research jobs.

| Entity | Role |
| --- | --- |
| `GeographicMarket` | Country + optional metro; ISO-like codes; no per-city tables hardcoded in logic |
| `MarketEconomicProfile` | Versioned market snapshot |
| `IndustryEconomicProfile` | Advertiser category / industry economics |
| `CreatorAudienceSnapshot` | Point-in-time audience composition |
| `CreatorPerformanceSnapshot` | Point-in-time reach/engagement |
| `InventoryRateBenchmark` | Slot-type + format + duration-band + market benchmark |
| `PricingModel` | Allowed pricing bases |
| `PricingRuleVersion` | Immutable rule/document that produced a recommendation |
| `CompensationRuleVersion` | Immutable split/settlement rules |
| `ResearchSource` | Catalog of public/approved sources |
| `MarketBenchmarkObservation` | One metric observation with provenance |
| `RateRecommendation` | Immutable recommendation document |
| `RateRecommendationFactor` | Explainable factor rows |
| `Quote` | Commercial quote header |
| `QuoteLineItem` | Inventory line |
| `QuoteVersion` | Immutable quote revision |
| `HistoricalPlacementEconomics` | Post-hoc actuals vs quote (pins existing placement/run ids) |
| `CampaignPerformanceEconomics` | Campaign-level rolled actuals |
| `FxObservation` | Versioned FX; not a live silent rewrite of old quotes |

Research observation provenance (minimum):

- Source and source URL/reference
- Geographic market, industry/category, platform
- Metric, value or range, currency
- Publication/research date, retrieved date
- Confidence
- VERIFIED / ESTIMATED / INFERRED / UNKNOWN

## Workforce (later contract only)

This module is **not** a Wedding Planner phase. A later contract may add:

- Deterministic `pricing-rules.v1` (PASS/BLOCK/WARN as contracted) before
  AI
- Curator-like research workers for public benchmark extraction
- Human operator/admin acceptance of benchmark catalogs and quotes
- Zero authority for AI to approve quotes or mutate compensation rules

Reuse Wedding Planner provider patterns (`IWeddingPlannerResearchProvider`
style) rather than a new silent LLM memory.

## Sequencing

| Now | Later, only with a dedicated contract |
| --- | --- |
| This architecture document | EF model, migration, APIs, UI |
| Identified integration points | Wedding Planner “ask economics” façade |
| No hard-coded prices in current code | Seed **data** for launch markets |
| WP Phase 9 remains advisory measurement | Delivery measurement + media ledger + this engine composing history |

Recommended crane order once authorized: geographic market data →
provenance-backed benchmark intake → deterministic range engine → quote
versions → pin quotes from Wedding Planner → ingest actuals from
measurement → compensation rule versions → settlement. Do not skip to
autonomous quoting.

## Acceptance of this documentation pass

Preserved requirement, identified callers, no implementation of pricing,
and no interruption of the current Bliss/Wedding Planner sequence.
The long-term objective remains:

**Right advertiser + right audience + right inventory + right market
economics + defensible price + auditable data.**
