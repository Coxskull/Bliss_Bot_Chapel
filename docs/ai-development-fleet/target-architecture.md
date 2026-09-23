# Target architecture (desired — not claimed as built)

This is the **intended** Alpha Global Media / Bliss platform. It is **not** the current codebase.

## Target pipeline

```mermaid
flowchart TB
  NET[Internet / Platforms]
  FF[Fishing Fleet discovery]
  BL[Bliss Bot Chapel matching]
  HUM[Human control / review]
  ECO[Economics and Rate Intelligence]
  WP[Wedding Planner quotes / plans]
  CE[Campaign Engine]
  AD[Ad Delivery]
  ME[Measurement]
  FI[Financial Engine - Alpha ledger]
  PAY[Payment providers move money]

  NET --> FF
  FF --> BL
  BL --> HUM
  HUM --> ECO
  ECO --> WP
  WP --> CE
  CE --> AD
  AD --> ME
  ME --> FI
  PAY --> FI
  ME --> ECO
  FI --> ECO
```

## Where current components fit

| Target stage | What exists today | Gap |
| --- | --- | --- |
| Internet / Platforms | ExternalProfileId / URL strings on CreatorPlatform | No crawlers |
| Fishing Fleet | MISSING | Entire subsystem |
| Bliss Bot Chapel | Phase 1 **schema + read API + tests** | Chaperone, Officiant, write workflows |
| Human control | MISSING in Bliss; Alpha Auto has role-verification for **auto** users | Media review queue |
| Campaign Engine | Campaign + Placement **rows** | No match reference, approval, dates, creatives |
| Economics & Rate Intelligence | Phases 1–3 market/creator/inventory/FX inputs + GET-only operator view | No recommendation engine or quotes; see `docs/economics/` |
| Wedding Planner | Phase 1 workspace/session/message | Must ask Economics for prices later; must not invent rates |
| Ad Delivery | Slot types including overlays/sponsored as **constants** | No renderer/runtime |
| Measurement | MISSING | Entire subsystem |
| Financial Engine | Alpha Auto **order** ledger | Must not be reused as media ledger without a new bounded context |

## Missing connections (current)

- No Fishing Fleet → Creator ingest
- CampaignPlacement does not reference BlissMatch (schema now has optional `BlissMatchId`; behavior still incomplete)
- Campaign does not reference Advertiser/Opportunity
- EligibilityCheck is not evaluated
- MatchScoreComponent is not calculated
- No measurement → ledger posting
- Bliss repo ↛ Alpha Auto repo

## Design rules for later missions

- Countries/languages/currencies = **data/configuration**, not per-country tables.
- Advertising rates = versioned observations + rule versions, not a universal CPM or dollars-per-minute.
- Compensation shares = versioned configuration, not a compiled 20/80 split.
- Payment providers ≠ Alpha financial meaning.
- n8n = orchestration; Alpha/Bliss DB = business truth.
- AI research ≠ permanent market truth; provenance required.
- Do not merge Alpha Auto order domain into Bliss tables.
- Do not delete Bliss Phase 1 entities to “start over.”
- Do not implement Economics inside current Wedding Planner phases.
