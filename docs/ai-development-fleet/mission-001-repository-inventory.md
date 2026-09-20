# Mission 001 — Repository inventory

Evidence collected 2026-09-20. Conclusions cite project/path. Status vocabulary: IMPLEMENTED, PARTIALLY IMPLEMENTED, DECLARED BUT NOT FUNCTIONAL, TESTED, UNTESTED, MISSING, DUPLICATED, CONFLICTING, TECHNICAL DEBT, SECURITY CONCERN.

## Step 1 — Tools (this agent)

| Tool | Access |
| --- | --- |
| Bliss Bot Chapel GitHub | YES — this workspace |
| Alpha GitHub repositories | YES for public clone/read (`gh search` + clone). This environment is **not** a multi-repo checkout. |
| Cursor Cloud Agent | YES |
| GitHub Copilot Pro | NOT available in this session |
| ChatGPT | NOT available in this session |
| Existing .NET 8 + PostgreSQL 16 | YES on the agent VM |
| Hosted Supabase / production DB | NOT used. Intentionally not connected. |

Secrets were not requested and are not included.

---

## Repositories discovered (Coxskull)

| Repository | Default branch | HEAD SHA (main at audit) | Language | Role vs Bliss blueprint |
| --- | --- | --- | --- | --- |
| `Coxskull/Bliss_Bot_Chapel` | `main` | `c828628d8df233e29b3b6374daf201bfadac6dc7` (merge of Phase 1 evidence PR #2). This working branch: `aee254a75d1977ed6e4e3fc042acfc4fa2ec09f7` | C# | **Bliss Phase 1 foundation** |
| `Coxskull/alpha-backend` | `main` | `84f5ebf66c83ebd5f8ef79b45720b0c6b3163cb5` | C# | **Alpha Auto** marketplace/logistics/finance backend |
| `Coxskull/Alpha-Auto-MVP` | `main` | `d351b81c8ea0989b7fcc633f8c4caf755c0a54b2` | TS + C# monorepo | **Older/skeletal Alpha Auto** slice + frontend |
| `Coxskull/alpha-mvp-v2` | `main` | `7be088a3728f8a8040ba65c90d162ab3db0865e0` | TypeScript | **Alpha Auto** multi-portal frontend |
| `Coxskull/alpha-frontend` | `main` | `0f5850ce8ce0e5a82ca91afef562ba46c6943308` | TypeScript | **Earlier Mission Control-only** frontend |

Other Coxskull repos (pharmacy, portfolio, sher-it, `new`) are **out of scope** for Bliss.

**CONFLICTING / DUPLICATED:** Alpha Auto exists as at least four repos with overlapping portals and APIs. Bliss is a **separate** solution and is **not** referenced from those Alpha Auto codebases (zero `BlissMatch` / `AdInventorySlot` matches there).

README in Bliss states: *This repository is not connected to Alpha Auto.* Verified: no project references, no shared solution.

---

## 1. Bliss_Bot_Chapel — actual structure

```
BlissBotChapel.sln
├── Bliss.Domain
│   ├── Common/EntityStatuses.cs          # status + InventorySlotTypes constants
│   ├── Enums/AccessStatus.cs
│   └── Entities/                         # 18 entity classes
├── Bliss.Infrastructure
│   ├── Configurations/                   # EF IEntityTypeConfiguration per entity
│   ├── Persistence/BlissDbContext.cs
│   ├── Persistence/BlissDbContextFactory.cs
│   ├── Persistence/Phase1DataSeeder.cs
│   ├── Migrations/20260919013044_Phase1Foundation.cs
│   └── DependencyInjection/              # registers seeder only
├── Bliss.Api
│   ├── Program.cs
│   ├── Controllers/                      # GET-only
│   └── Contracts/ApiDtos.cs
└── Bliss.Tests
    ├── Architecture/
    ├── Domain/
    ├── Persistence/
    └── TestDb.cs                         # EF InMemory
```

Documented architecture: `docs/bliss/*` (Phase 1 contract, requirements, acceptance evidence).

### Domain entities (files)

`Creator`, `CreatorPlatform`, `ContentItem`, `AdInventorySlot`, `Advertiser`, `AdvertiserProgram`, `AdvertiserOpportunity`, `AffiliateNetwork`, `NetworkAccess`, `ProgramAccess`, `RuleVersion`, `BlissMatch`, `MatchScoreComponent`, `EligibilityCheck`, `DataProvenance`, `Campaign`, `CampaignPlacement`, `MatchEvaluationRun`.

### DbContext

`Bliss.Infrastructure/Persistence/BlissDbContext.cs` — one context, `ApplyConfigurationsFromAssembly`. PostgreSQL via Npgsql. Design-time factory uses local placeholder host `bliss_phase1` (SECURITY: local placeholder password, not a hosted secret).

### Migrations

Single migration: `20260919013044_Phase1Foundation`. SQL dump: `phase1.sql`, `docs/sql/phase1.sql`.

### API (IMPLEMENTED read-only; UNTESTED against HTTP in automated suite except route-name reflection)

| Controller | Route | Methods |
| --- | --- | --- |
| `CreatorsController` | `api/creators` | GET list, GET `{id}` |
| `ContentItemsController` | `api/content-items` | GET list, GET `{id}` |
| `AdvertisersController` | `api/advertisers` | GET list, GET `{id}` |
| `AdvertiserProgramsController` | `api/advertiser-programs` | GET list |
| `AdvertiserOpportunitiesController` | `api/advertiser-opportunities` | GET list |
| `BlissMatchesController` | `api/bliss/matches` | GET list, GET `{id}` |
| `RuleVersionsController` | `api/rule-versions` | GET list |

No POST/PUT/DELETE. No dedicated endpoints for AffiliateNetwork, NetworkAccess, ProgramAccess, DataProvenance, Campaign, CampaignPlacement, EligibilityCheck, MatchScoreComponent (nested on match detail where seeded).

### Services / interfaces

**MISSING** application services. Controllers query `BlissDbContext` directly. `AddBlissInfrastructure` only registers `Phase1DataSeeder`.

### Authentication / authorization

**MISSING** authentication. `Program.cs` calls `UseAuthorization()` without `AddAuthentication`. **UNTESTED** authorization. All GET endpoints are anonymous.

### Background jobs / queues / n8n

**MISSING** in Bliss.

### External integrations

**MISSING** live Awin, Levanta, ACCESSTRADE, Firecrawl, Tavily, Wise, PayPal, Stripe, Xendit, n8n. Npgsql is PostgreSQL/Supabase-compatible driver only.

### Configuration

Variable names (no values): `ConnectionStrings:DefaultConnection`. Empty in committed `appsettings.json`.

### Logging / errors

ASP.NET default logging. No domain error pipeline. Seed catch logs warning if DB unreachable.

### Existing Bliss / creator / advertiser / campaign / financial / audit

| Area | Status | Evidence |
| --- | --- | --- |
| Bliss match storage | PARTIALLY IMPLEMENTED | Entity + GET API + seed; no matching engine |
| Creator | PARTIALLY IMPLEMENTED | Persistence + GET; no discovery/dedup |
| Advertiser | PARTIALLY IMPLEMENTED | Advertiser ≠ Program ≠ Opportunity tables |
| Campaign | DECLARED BUT NOT FUNCTIONAL | `Campaign`/`CampaignPlacement` persist; no execution |
| Financial/transaction | MISSING | No ledger entities |
| Provenance | PARTIALLY IMPLEMENTED | `DataProvenance` table + one seeded field |

---

## 2. alpha-backend — actual structure (read-only clone)

Single project `Alpha.API.csproj` (no `.sln`). ASP.NET Core 8. Folders: `Controllers/`, `Data/AppDbContext.cs`, `Models/`, `Services/`, `Migrations/`, `Security/`.

**Domain:** automotive orders, drivers, suppliers, mechanics, customers, payments, tax, referrals, entrepreneur earnings, settlements. **Not** Bliss creators/matches/inventory.

**Auth:** JWT + BCrypt + roles — IMPLEMENTED but **inconsistently applied** (many controllers lack `[Authorize]`).

**Payments:** Stripe, PayPal, PayMongo, Maya, Xendit, HitPay providers — IMPLEMENTED as adapters. Wise **MISSING**.

**Tests:** MISSING automated suite. Unauthenticated `api/test/auto-parts-commission`.

**SECURITY CONCERN:** committed connection string and JWT key in `appsettings.json` — see `security-findings.md`.

**TECHNICAL DEBT:** EF migrations cover ~7 tables; `AppDbContext` maps ~40+ entities.

---

## 3. Alpha-Auto-MVP

Monorepo: `alpha-frontend/` (Next.js) + `Alpha.API/` (older, ~15 order endpoints, **no auth**). Frontend expects a much larger Railway-hosted API. **DUPLICATED** vs `alpha-backend` + `alpha-mvp-v2`.

**SECURITY CONCERN:** same class of committed DB/JWT secrets in `Alpha.API/appsettings.json`.

---

## 4. alpha-mvp-v2

Next.js multi-portal Alpha Auto UI (customer, driver, provider, mechanic, mission-control, entrepreneur). JWT via `NEXT_PUBLIC_API_URL`. Referrals **IMPLEMENTED** in UI. Creator/advertiser/campaign/ads/Bliss **MISSING**. Tests **MISSING**.

---

## 5. alpha-frontend

Earlier Mission Control-only UI, **no auth**. Stub pages for messages/escalations/settings. Tests **MISSING**.

---

## Identity chain vs what exists

Target chain: Creator → Platform → Content → Inventory → BlissMatch → Campaign → Placement → Measurement → Transaction → Payout.

| Link | Bliss | Alpha Auto |
| --- | --- | --- |
| Creator | Guid `Creators.Id` | N/A (User/Customer) |
| Creator Platform | Guid + `ExternalProfileId` | MISSING |
| Content | Guid + `ExternalContentId` | MISSING |
| Ad inventory | Guid `AdInventorySlots` | MISSING |
| Bliss Match | Guid | MISSING |
| Campaign | Guid, no advertiser/opportunity FK | MISSING (not marketing campaigns) |
| Placement | Guid, no BlissMatch FK | MISSING |
| Measurement | MISSING | MISSING (ops KPIs only) |
| Transaction / payout | MISSING | IMPLEMENTED for **auto-parts orders** (`payments`, `settlement_queue`, `supplier_payouts`, etc.) — **different bounded context** |

**Breaks:** Campaign has no FK to BlissMatch, Advertiser, or Opportunity. Placement has no BlissMatchId. No measurement tables. No payout tables in Bliss. Alpha Auto payouts are order/settlement, not creator media.

Do not invent new IDs in this mission. Existing Bliss IDs are UUID primary keys plus optional external lookup strings.
