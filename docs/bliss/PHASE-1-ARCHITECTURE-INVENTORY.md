# PHASE-1-ARCHITECTURE-INVENTORY

Bliss Bot Chapel Phase 1 is a standalone ASP.NET Core monolith. It establishes the domain model and PostgreSQL schema required for later intelligence work. It does not implement matching, scoring, discovery, or live network integrations.

## Projects

| Project | Responsibility |
| --- | --- |
| `Bliss.Domain` | Entities, status constants, documented enums |
| `Bliss.Infrastructure` | `BlissDbContext`, EF configurations, migrations, Phase 1 seeder |
| `Bliss.Api` | Read-only REST visibility + Swagger |
| `Bliss.Tests` | Domain defaults, persistence cardinality, EF model constraints |

## Entities

| Entity | Table | Notes |
| --- | --- | --- |
| Creator | Creators | Nullable demographics |
| CreatorPlatform | CreatorPlatforms | Many per creator |
| ContentItem | ContentItems | Many per creator |
| AdInventorySlot | AdInventorySlots | Inventory, not a permanent ad |
| Advertiser | Advertisers | Separate from program/opportunity |
| AdvertiserProgram | AdvertiserPrograms | |
| AdvertiserOpportunity | AdvertiserOpportunities | Match target |
| AffiliateNetwork | AffiliateNetworks | |
| NetworkAccess | NetworkAccesses | Advertiser ↔ network |
| ProgramAccess | ProgramAccesses | Independent of network access |
| RuleVersion | RuleVersions | Historical match snapshot key |
| BlissMatch | BlissMatches | Many per creator |
| MatchScoreComponent | MatchScoreComponents | Persistence only |
| EligibilityCheck | EligibilityChecks | Persistence only |
| DataProvenance | DataProvenances | Source / confidence / timestamp |
| Campaign | Campaigns | Minimal foundation |
| CampaignPlacement | CampaignPlacements | Many per content item |
| MatchEvaluationRun | MatchEvaluationRuns | Optional Phase 3 prep, not an engine |

Economics, quotes, and compensation rules are **not** Bliss Phase 1
entities. The separate Economics bounded context now has its own Phase 1
reference-data foundation; see `docs/economics/`.

## Core graphs

```
Creator → CreatorPlatform
Creator → ContentItem → AdInventorySlot → CampaignPlacement ← Campaign
Creator → BlissMatch ← AdvertiserOpportunity ← AdvertiserProgram ← Advertiser
Advertiser → NetworkAccess ← AffiliateNetwork
AdvertiserProgram → ProgramAccess
BlissMatch → RuleVersion
BlissMatch → MatchScoreComponent
BlissMatch → EligibilityCheck
```

## API surface (read-only)

- `GET /api/creators`
- `GET /api/creators/{id}`
- `GET /api/content-items`
- `GET /api/content-items/{id}`
- `GET /api/advertisers`
- `GET /api/advertisers/{id}`
- `GET /api/advertiser-programs`
- `GET /api/advertiser-opportunities`
- `GET /api/bliss/matches`
- `GET /api/bliss/matches/{id}`
- `GET /api/rule-versions`
