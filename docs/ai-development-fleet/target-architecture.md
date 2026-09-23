# Target architecture (desired — not claimed as built)

This is the **intended** Alpha Global Media / Bliss platform. It is **not** the current codebase.

## Target pipeline

```mermaid
flowchart TB
  NET[Internet / Platforms]
  FF[Fishing Fleet discovery]
  BL[Bliss Bot Chapel]
  HUM[Human control / review]
  CE[Campaign Engine]
  ECO[Economics and rate intelligence]
  WP[Wedding Planner asks economics]
  AD[Ad Delivery]
  ME[Measurement]
  FI[Financial Engine - Alpha media ledger]
  PAY[Payment providers move money]

  NET --> FF
  FF --> BL
  BL --> HUM
  HUM --> CE
  CE --> ECO
  ECO --> WP
  WP --> AD
  AD --> ME
  ME --> FI
  ME --> ECO
  FI --> ECO
  PAY --> FI
```

## Where current components fit

| Target stage | What exists today | Gap |
| --- | --- | --- |
| Internet / Platforms | ExternalProfileId / URL strings on CreatorPlatform | No crawlers |
| Fishing Fleet | MISSING | Entire subsystem |
| Bliss Bot Chapel | Phase 1 **schema + read API + tests** | Chaperone, Officiant, write workflows |
| Human control | MISSING in Bliss; Alpha Auto has role-verification for **auto** users | Media review queue |
| Campaign Engine | Campaign + Placement **rows** | No match reference, approval, dates, creatives |
| Economics & rate intelligence | Architecture only: `docs/bliss-economics/FUTURE-BOUNDED-CONTEXT.md` | No tables, quotes, or pricing APIs |
| Wedding Planner | Phases 1–9 planning + advisory measurement learning | Must not invent prices |
| Ad Delivery | Slot types including overlays as **data** | No renderer/runtime |
| Measurement | WP Phase 9 advisory aggregates only | Delivery event subsystem still MISSING |
| Financial Engine | Alpha Auto **order** ledger | Must not be reused as media ledger without a new bounded context |

## Missing connections (current)

- No Fishing Fleet → Creator ingest
- CampaignPlacement does not reference BlissMatch
- Campaign does not reference Advertiser/Opportunity
- EligibilityCheck is not evaluated
- MatchScoreComponent is not calculated
- No measurement → ledger posting
- Wedding Planner does not query an economics engine (by design until a later contract)
- Bliss repo ↛ Alpha Auto repo

## Design rules for later missions

- Countries/languages/currencies = **data/configuration**, not per-country tables.
- Inventory prices, quotes, and compensation splits belong to a **future** economics bounded context — never matching scores or Wedding Planner documents.
- Payment providers ≠ Alpha financial meaning.
- n8n = orchestration; Alpha/Bliss DB = business truth.
- Do not merge Alpha Auto order domain into Bliss tables.
- Do not delete Bliss Phase 1 entities to “start over.”
