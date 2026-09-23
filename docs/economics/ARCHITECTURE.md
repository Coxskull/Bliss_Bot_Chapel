# Bliss Economics & Rate Intelligence Engine™ — Architecture

**Bounded context:** `Bliss.Economics` (conceptual). Separate from Bliss
matching, Wedding Planner, campaign placement, Fishing Fleet, and
Alpha Auto.

**System of record:** PostgreSQL / Supabase.

**Application authority:** .NET.

**Async research / retries:** n8n (orchestration only).

**AI role:** research, extraction, classification, and recommendation
*assistance*. AI is not the database and must not silently control
pricing.

## Purpose

Become the economic intelligence layer that other Bliss Chapel systems
**call** when Alpha needs to estimate, recommend, explain, or quote the
value of advertising inventory.

The objective is **not** one universal Alpha advertising price.

A creator in Manila must not automatically receive the same valuation as
a creator in Medellín, Panama City, Santo Domingo, Kuala Lumpur, or
Jakarta merely because subscriber counts are similar.

## What this module is not

It does not replace:

- Bliss matching (`DeterministicRuleEvaluator`, matches, score components)
- The Wedding Planner™ (advertiser planning personality and workspace)
- Campaign Placement (planned bindings of inventory to campaigns)
- Creator / Advertiser / Opportunity databases
- Measurement, media ledger, or payout execution

Matching answers “is this audience compatible?” Economics answers “what
is this inventory plausibly worth, and why?” Placement answers “was this
slot planned onto a campaign?” Settlement answers “who is owed what
after a contracted amount?”

## Architectural position

```
CREATOR + CONTENT + AUDIENCE
        ↓
BLISS MATCHING
        ↓
COMPATIBLE INVENTORY
        ↓
ECONOMICS & RATE INTELLIGENCE
        ↓
THE WEDDING PLANNER™
        ↓
QUOTE / CAMPAIGN PLAN
        ↓
HUMAN APPROVAL
        ↓
CAMPAIGN PLACEMENT
        ↓
MEASUREMENT
        ↓
LEDGER / SETTLEMENT
        ↓
HISTORICAL ECONOMIC DATA
        ↺ feeds future rate intelligence
```

Wedding Planner presents options. Economics computes and explains
ranges. Humans approve. Placement and settlement remain downstream.

## Pricing principles

1. **The product is advertising inventory and audience exposure**, not
   minutes of airtime. Duration is one variable, stored on the slot and
   on quote line items. Ten minutes is not automatically 10× one minute.
2. **Do not design “X dollars per minute” as the engine.** Support
   multiple pricing models:
   - CPM (cost per 1,000 impressions / listens / views)
   - CPV (cost per view where applicable)
   - Flat-rate placement
   - Fixed campaign rate
   - Sponsorship rate
   - Host-read / integrated segment rate
   - Performance-based CPA / CPL / CPS where applicable
   - Hybrid combinations of the above
3. **Recommend ranges, not false precision.** Especially before Alpha
   has substantial proprietary transaction history, return low / target /
   high, a confidence level, and the factors used.
4. **Market value ≠ compensation / settlement.** Inventory market value,
   advertiser quote, contracted amount, Alpha compensation, creator
   compensation, other participant compensation, and settlement are
   related but separate. A $100 campaign with Alpha 20% / creator 80%
   is an **example**, never a universal rule. Compensation shares are
   versioned configuration.
5. **Launch cities are data, not code.** Santo Domingo, Panama City,
   Medellín, Manila, Kuala Lumpur, and Jakarta are initial Alpha
   *markets*. Country and city/metro belong in `GeographicMarkets`.
   Do not hard-code those six names into calculation logic.
6. **AI research ≠ permanent truth.** Every important benchmark is a
   versioned observation with provenance. Models must not silently
   invent a market rate.
7. **Do not overwrite history.** When benchmarks, rules, or quotes
   change, insert a new version or observation. Historical
   recommendations remain reproducible from the data and rule/model
   version used at that time.
8. **Audience demographics and advertiser-market compatibility can
   outweigh subscriber count and content-topic similarity**, consistent
   with Bliss matching philosophy. Subscriber count is an input, not
   the rate.

## Inventory the engine must price

Reuse existing `AdInventorySlot.SlotType` values. Duration remains a
nullable variable on the slot (`DurationSeconds`).

| Slot type | Constant |
| --- | --- |
| Pre-roll | `PRE_ROLL` |
| Mid-roll | `MID_ROLL` |
| Post-roll | `POST_ROLL` |
| Lower third | `LOWER_THIRD` |
| Perimeter overlay | `PERIMETER_OVERLAY` |
| Corner overlay | `CORNER_OVERLAY` |
| Rotating overlay | `ROTATING_OVERLAY` |
| Sponsored segment | `SPONSORED_SEGMENT` |

Typical durations (15s, 30s, 60s, 5 / 10 / 15 minutes) are **data**,
not multipliers.

## Creator / audience economics (inputs, not the formula)

The engine should be able to consider, when present:

- Subscribers / followers
- Average views / listens per content item
- Daily / weekly / monthly views or listens
- Historical reach
- Engagement rate
- Audience retention where available
- Audience age, gender, geography, language
- Platform and content format
- Publishing frequency
- Historical campaign performance

Unknown metrics stay unknown. They must not coerce to zero value.

Creator A (100,000 subscribers), Creator B (5,000), and Creator C
(1,000) must not automatically share a rate, and A is not automatically
worth 20× B.

`Creator.AudienceSize` and `CreatorPlatform.Followers` are insufficient
alone. Future snapshots (`CreatorAudienceSnapshot`,
`CreatorPerformanceSnapshot`) version those measurements with
provenance.

## Market economics (inputs)

Possible market variables, stored as profiles and observations—not
compiled constants:

- Typical digital advertising costs
- Local CPM, creator/influencer, podcast, and social-media benchmarks
- Local purchasing power
- Advertiser / customer-acquisition economics
- Industry / category economics
- Currency and exchange rate (as dated observations)
- Local competition
- Audience scarcity / value
- Historical Alpha transaction data (preferred once it exists)

## AI market research

AI may research **publicly available** market information from the web
and approved data sources via n8n jobs. Each material benchmark
preserves:

- Source name
- Source URL / reference
- Geographic market
- Industry / category
- Platform
- Metric
- Value or range
- Currency
- Publication / research date
- Date retrieved
- Confidence level
- Status: `VERIFIED` / `ESTIMATED` / `INFERRED` / `UNKNOWN`

A recommendation without supporting observations or an explicit
`UNKNOWN` gap must not be treated as a quoted rate.

## Rate recommendation shape

Inputs (any may be absent; absence lowers confidence):

Creator → content item → audience data → inventory slot → duration →
country / city → advertiser category → campaign objective → current
market benchmarks → historical Alpha data.

Output (structured rows, not a prompt):

| Field | Meaning |
| --- | --- |
| Low / target / high | Recommended rate range in a named currency |
| Pricing basis | CPM, flat, sponsorship, hybrid, etc. |
| Estimated impressions | When the basis needs reach |
| Primary economic factors | Child `RateRecommendationFactor` rows |
| Market | FK to `GeographicMarkets` |
| Benchmark date | As-of date of observations used |
| Confidence | `LOW` / `MEDIUM` / `HIGH` / `UNKNOWN` |
| Supporting sources | Links to observations / research sources |
| Pricing rule version | Exact rule/model version used |
| Compensation rule version | Recorded when a split is *illustrated*, never implied as market value |

Illustrative (not a seeded production rate):

- Estimated market range: $180–$260
- Recommended Alpha quote: $225
- Confidence: MEDIUM
- Reasoning: 42,000 average verified views; Manila audience
  concentration; 68% target demographic match; mid-roll; 60-second
  host-read; comparable-market CPM range; limited Alpha historical data

## Learning from Alpha's own data

Public research bootstraps the system. Alpha transaction history should
eventually dominate comparable-market guesses.

Preserve the chain:

Quoted rate → advertiser response → accepted / declined → final
negotiated rate → inventory purchased → actual impressions / views →
engagement / conversions where available → creator compensation →
Alpha compensation → campaign performance.

External benchmarks and Alpha actuals remain comparable, never merged
by overwrite.

## Alpha + creator economics

```
Inventory market value
        ↓
Advertiser quote
        ↓
Final contracted amount
        ↓
Alpha compensation
        ↓
Creator compensation
        ↓
Other authorized participant compensation
        ↓
Settlement (media ledger — separate bounded work)
```

Market value calculation must not write settlement entries. Settlement
must not recompute market value.

## Governance

| Layer | Owns |
| --- | --- |
| .NET | Recommendation, quote, and compensation **application** authority |
| PostgreSQL | Permanent structured economics |
| n8n | Async research fetch, retries, extraction jobs |
| AI | Assist research/extraction; never the system of record |
| Humans | Approve quotes and overrides |

Every material rate recommendation must be reproducible from:

- input snapshot (creator/audience/inventory/market ids and as-of time)
- observation ids used
- `PricingRuleVersion`
- optional `CompensationRuleVersion` if a split was shown

## Entity catalog (names may be refined at implementation)

| Entity | Role |
| --- | --- |
| `GeographicMarket` | Country + optional city/metro, currency code. Data, not per-city tables |
| `MarketEconomicProfile` | Versioned local advertising-economics profile |
| `IndustryEconomicProfile` | Category / industry economics |
| `CreatorAudienceSnapshot` | Versioned audience composition for a creator |
| `CreatorPerformanceSnapshot` | Versioned reach, views, engagement, frequency |
| `InventoryRateBenchmark` | Slot-type + format + duration-band benchmarks |
| `PricingModel` | Catalog of CPM, CPV, flat, sponsorship, hybrid, etc. |
| `PricingRuleVersion` | Immutable priced-rule document (JSON) used by a recommendation |
| `CompensationRuleVersion` | Immutable participant-share document. No default 20/80 |
| `ResearchSource` | Named public or approved source |
| `MarketBenchmarkObservation` | One dated metric/range with provenance and verification status |
| `RateRecommendation` | Explainable low/target/high result |
| `RateRecommendationFactor` | One contributing factor and its weight/rationale |
| `Quote` | Advertiser-facing commercial envelope |
| `QuoteVersion` | Immutable quote snapshot; never silent overwrite |
| `QuoteLineItem` | Inventory + pricing basis + amounts |
| `QuoteOutcome` | Accepted / declined / negotiated response |
| `HistoricalPlacementEconomics` | What was quoted vs contracted vs delivered |
| `CampaignPerformanceEconomics` | Post-campaign outcomes feeding later rates |
| `EconomicsResearchRun` | Async research job audit (n8n is not the ledger) |

Existing `DataProvenance` remains valid for creator/demographic fields.
Economics observations have a dedicated table because they are
first-class market facts, not polymorphic field notes.

Existing `AdvertiserOpportunity.CommissionPercentage` / `FixedFee` are
**affiliate-opportunity commercial terms**. They are not inventory
market rates and must not be read as the Economics Engine.

## API status

Economics Phases 1–6 implement reference data, versioned economic
inputs, deterministic recommendations, human-controlled commercial
history, and non-settlement compensation illustrations:

- `GET /api/economics/markets`
- `GET /api/economics/pricing-models`
- `GET /api/economics/research-sources`
- `GET /api/economics/observations`
- `GET /api/economics/audience-snapshots`
- `GET /api/economics/performance-snapshots`
- `GET /api/economics/market-profiles`
- `GET /api/economics/industry-profiles`
- `GET /api/economics/inventory-benchmarks`
- `GET /api/economics/exchange-rates`
- `GET /api/economics/pricing-rule-versions`
- `POST /api/economics/recommendations`
- `GET /api/economics/recommendations`
- `GET /api/economics/recommendations/{id}`
- `POST /api/economics/quotes`
- `GET /api/economics/quotes`
- `GET /api/economics/quotes/{id}`
- `POST /api/economics/quotes/{id}/versions`
- `POST /api/economics/quotes/{id}/approvals`
- `POST /api/economics/quotes/{id}/outcomes`
- `GET /api/economics/compensation-rule-versions`
- `GET /api/economics/compensation-illustrations`
- `GET /api/economics/compensation-illustrations/{id}`
- `POST /api/economics/compensation-illustrations`

Wedding Planner must call .NET. It must not call an LLM for a price.

## Phase placement

| Work | Status |
| --- | --- |
| Bliss matching Phases 1–19 | Accepted foundation. Unchanged. |
| Wedding Planner Phase 1 | Foundation only. Unchanged. |
| Wedding Planner Phases 2–7 | Conversation, creative, QA. Must not invent rates. |
| Wedding Planner Phase 8 handshake | Creative + approved match + compatible inventory → planned placement. Must **ask** Economics when quotes are in scope; Phase 8 is not rewritten now. |
| Wedding Planner Phase 9 | Measurement/learning. Consumes historical economics; does not own pricing rules. |
| Economics Phase 1 | Reference-data foundation implemented and accepted. |
| Economics Phase 2 | Creator audience/performance snapshots implemented and accepted. |
| Economics Phase 3 | Market/industry profiles, inventory benchmarks, and FX implemented and accepted. |
| Economics Phase 4 | Deterministic recommendations implemented and accepted. |
| Economics Phase 5 | Versioned quotes, approvals, and outcomes implemented and accepted. |
| Economics Phase 6 | Versioned compensation policies and non-settlement illustrations implemented; pending owner acceptance. |
| Economics Phases 7–9 | Individually gated by `PHASE-ROADMAP.md`; not authorized. |
| Media ledger / payouts | Separate later work. Reads contracted amounts; does not price inventory. |

Do not auto-advance this module into the current Wedding Planner
implementation.
