# Mission 001 — Baseline build/test report

Purpose: record condition **before** any Mission 002 construction so later failures are not blamed on new AI work.

## Bliss_Bot_Chapel (this workspace)

| Item | Value |
| --- | --- |
| Repository | `Coxskull/Bliss_Bot_Chapel` |
| Default branch | `main` |
| Default branch SHA | `c828628d8df233e29b3b6374daf201bfadac6dc7` |
| Audit branch | `cursor/ai-mission-001-repository-audit-cc83` |
| Audit commit at test time | `aee254a75d1977ed6e4e3fc042acfc4fa2ec09f7` |
| Solution | `BlissBotChapel.sln` |
| SDK | .NET 8.0.131 |

### Build

```text
dotnet build BlissBotChapel.sln
Build succeeded. 0 Warning(s). 0 Error(s).
```

### Tests

```text
COMMAND: dotnet test BlissBotChapel.sln --verbosity minimal
COMMIT:  aee254a75d1977ed6e4e3fc042acfc4fa2ec09f7
SUITE:   Bliss.Tests.dll (xUnit)
DISCOVERED / TOTAL: 23
PASSED:  23
FAILED:  0
SKIPPED: 0
```

**What these tests prove**

| Test | Proves | Does **not** prove |
| --- | --- | --- |
| `Creator_supports_multiple_content_items` | Two ContentItems can persist for one Creator in EF InMemory | Deduplication, identity resolution, HTTP |
| `Content_item_supports_multiple_ad_inventory_slots` | Four slot types persist on one content item | Ad delivery, rendering, exclusivity rules |
| `Creator_supports_multiple_simultaneous_bliss_matches_without_uniqueness_violation` | Three matches same creator | Scoring, eligibility engines |
| `New_bliss_match_does_not_overwrite_another_valid_match` | Inserting match B leaves match A | Concurrency under Postgres unique constraints beyond model |
| `Multiple_campaign_placements_can_reference_the_same_content_item` | Three placements same content | Campaign execution |
| `Historical_bliss_match_preserves_original_rule_version` | Adding RuleVersion 2 does not rewrite match FK | Immutable score snapshots under edits |
| `Network_access_and_program_access_are_independent` | APPROVED vs UNKNOWN can coexist | Chaperone evaluation |
| `Data_provenance_retains_source_confidence_and_collected_at` | Provenance row round-trips | Freshness jobs, source verification |
| `Unknown_female_percentage_remains_null_and_is_not_converted_to_zero` | NULL ≠ 0 in InMemory | API consumers treating omitted JSON as 0 |
| `Phase1_seed_creates_required_graph` | Seeder graph counts | Hosted TEST DB (that was a prior acceptance run, not this command) |
| `Bliss_match_creator_id_is_not_unique` / placement index tests | EF model has non-unique indexes | Database unique constraints on a live server (verified in Phase 1 acceptance SQL, not this xUnit run) |
| `There_is_no_direct_creator_to_advertiser_foreign_key` | No Creator/ContentItem → Advertiser FK in model | Runtime business rules |
| `Phase1_read_endpoints_are_present` | Controller route attributes exist | HTTP 200, Swagger, auth |
| Entity default tests | CLR default strings/bools | Persistence defaults on Postgres |

**Pre-existing limitations (not regressions):** no HTTP/integration tests; persistence tests use `UseInMemoryDatabase`; no auth tests.

### Migrations present

`Bliss.Infrastructure/Migrations/20260919013044_Phase1Foundation.cs` only.

---

## Alpha Auto repositories (read-only; not built in this workspace)

| Repo | SHA | Automated tests | Notes |
| --- | --- | --- | --- |
| `alpha-backend` | `84f5ebf6…` | **0 test projects** | Manual `POST api/test/auto-parts-commission` is not a test suite |
| `Alpha-Auto-MVP` | `d351b81c…` | **0** frontend/backend tests | In-repo API is a thin order slice |
| `alpha-mvp-v2` | `7be088a3…` | **0** | No `*.test.*` / `*.spec.*` |
| `alpha-frontend` | `0f5850ce…` | **0** | No auth |

These failures/absences **predate** Mission 001. Do not treat them as Bliss Phase 1 regressions.

Alpha Auto **was not compiled** here (change freeze + different product). Inventory is from source inspection.
