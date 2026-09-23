# Mission 001 — Implementation / gap matrix

Status: ✅ COMPLETE · 🟡 PARTIAL · ❌ MISSING · ⚠️ CONFLICTING · 🔁 DUPLICATED · 🧱 TECHNICAL DEBT · 🔐 SECURITY RISK · 🧪 TEST MISSING

COMPLETE requires evidence. Subjective guesses are not used.

## Bliss requirements (Phase 1)

| Requirement ID | System | Requirement | Existing component | Status | Evidence | Tests | Risk | Dependency | Recommended action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BLC-ARCH-001 | Bliss | Creator many ContentItems | `Creator`,`ContentItem` | ✅ | EF config + tables | `Creator_supports_multiple_content_items` | Low | — | Keep |
| BLC-ARCH-002 | Bliss | ContentItem many slots | `AdInventorySlot` | ✅ | Restrict FK, 4 types seeded | `Content_item_supports_multiple_ad_inventory_slots` | Low | — | Keep |
| BLC-ARCH-003 | Bliss | Many simultaneous matches | `BlissMatch.CreatorId` non-unique | ✅ | Index not unique | uniqueness tests | Low | — | Keep |
| BLC-ARCH-004 | Bliss | Match B does not overwrite A | Separate rows | ✅ | Persistence test | `New_bliss_match_does_not_overwrite_another_valid_match` | Low | — | Keep |
| BLC-ADV-001 | Bliss | Advertiser ≠ Program ≠ Opportunity | 3 tables | ✅ | FKs | Seed graph | Low | — | Keep |
| BLC-ACCESS-001 | Bliss | Network vs program access | `NetworkAccess`,`ProgramAccess` | ✅ | Independent statuses | `Network_access_and_program_access_are_independent` | Low | — | Keep |
| BLC-DATA-001 | Bliss | Provenance | `DataProvenance` | 🟡 | Table+seed; no API | Provenance test | Low | — | Optional GET later |
| BLC-DATA-002 | Bliss | UNKNOWN ≠ 0 | nullable decimals | ✅ | SQL NULL + test | Unknown demographic test | Low | — | Keep; watch API omit-null |
| BLC-HIST-001/002 | Bliss | Rule version immutability | `RuleVersionId` Restrict | ✅ | HIST tests | Historical test | Low | — | Keep |
| BLC-MULTI-001/002 | Bliss | Multi advertiser/placement | No Content→Advertiser FK | ✅ | Model tests | MULTI tests | Low | — | Keep |
| BLC-PLACE-001 | Bliss | Match ≠ placement | Separate tables | 🟡 | Schema only | No behavior test | Med | Campaign engine | Do not collapse tables |
| BLC-AI-001 | Bliss | AI not scoring authority | No scoring service | ✅ | Absence of engine | Architecture | Low | Officiant later | Do not add AI scorer in Phase 2 |
| BLC-API-RO | Bliss | Read visibility | GET controllers | 🟡 | Controllers exist | Route names only 🧪 | Med | Auth later | HTTP tests |
| CHAPERONE | Bliss | Deterministic eligibility | `EligibilityCheck` columns | ❌ engine | Placeholder `NOT_EVALUATED` | None for GEO/PLATFORM/etc | High if faked | RuleVersion | Mission after data contracts |
| OFFICIANT | Bliss | Configurable scoring | `MatchScoreComponent` | ❌ engine | Score/Weight null | None | High if faked | RuleVersion weights | Later |
| FISHING | Discovery | Global creator ingest | — | ❌ | No crawler code in Bliss or Alpha Auto | — | — | Canonical identity | New bounded context |
| CAMPAIGN-EXEC | Bliss | Campaign engine | `Campaign` name/status/created | 🟡 | No advertiser, match, approval, tracking | Placement cardinality only | Med | BlissMatch | Additive columns later |
| AD-DELIVERY | Bliss | Production delivery | SlotType strings | 🟡 schema / ❌ runtime | PRE/MID/POST/LOWER_THIRD only | Slot persist | High if demo=prod | Placements | Do not treat website demo as engine |
| MEASURE | Alpha/Bliss | Impressions/clicks/etc | — | ❌ | No tables | — | — | Placement IDs | New |
| FIN-BLISS | Bliss | Media ledger | — | ❌ | — | — | 🔴 | Measurement | Separate from Auto finance |
| ECO-BLISS | Bliss | Inventory rate intelligence | Vocabulary + reserved SQL | 🟡 docs | `docs/economics/` | `EconomicsBoundaryTests` | Med if faked | After WP current sequence | Do not hard-code CPM or 20/80 |
| FIN-AUTO | Alpha Auto | Order payments/settlements | Many tables + providers | 🟡 | Code present; 🧪 none; 🔐 leaks | None | 🔴 | Rotate secrets first | Do not reuse as media ledger |
| AUTH-BLISS | Bliss | AuthN/Z | `UseAuthorization` only | ❌ | Program.cs | None | 🔐 later | Product decision | Not Phase 1 |
| N8N | All | Orchestration | — | ❌ | Zero n8n workflow files in these repos | — | — | APIs | Do not put truth in n8n |
| HUMAN-REVIEW | Bliss | Review queue | — | ❌ | — | — | — | Matches | Later |

## Chaperone gap (audit only)

| Rule | Exists? |
| --- | --- |
| Geography eligibility | MISSING (strings stored) |
| Platform allow-list | MISSING (`CreatorPlatform.Platform` is a free string) |
| Minimum audience | MISSING (`AudienceSize` stored, not evaluated) |
| Language | MISSING |
| Campaign dates | MISSING on `Campaign`; optional StartAt/EndAt on placement unused |
| Opportunity status | STORED (`Status`), not evaluated |
| Prohibited categories / methods | MISSING |
| Alpha/provider eligibility | `NetworkAccess`/`ProgramAccess` **stored**, not composed into APPROVED/REVIEW_REQUIRED/INELIGIBLE |
| Reason codes GEO_NOT_ELIGIBLE etc. | MISSING (`ReasonCode` column exists for future use) |

## Officiant gap (audit only)

Weights configurable: **MISSING** (no rule payload). Component scores stored: **YES, nullable**. Historical scores immutable: **schema can store**; **no write path that mutates**. AI separated: **YES (no AI scorer)**. Confidence vs compatibility: **two nullable columns** on `BlissMatch`, unused.

## Fishing Fleet gap

Reusable: `Creator`, `CreatorPlatform`, `DataProvenance`, `CountryCode`/`Language` fields (global-by-column).  
Not reusable as discovery: Alpha Auto users/customers.  
Do **not** create PhilippinesCreatorTable.  
MISSING: platform adapters, identity resolution, dedup, snapshots, freshness, verification missions.

## Campaign engine gap

| Capability | Status |
| --- | --- |
| Campaign entity | PARTIAL (Id, Name, Status, CreatedAt) |
| Campaign status | PARTIAL (string, default DRAFT) |
| Placement | PARTIAL |
| Creator assignment | MISSING (only via ContentItem.CreatorId) |
| Advertiser assignment | MISSING |
| Opportunity assignment | MISSING |
| Bliss Match reference | MISSING |
| Creative assets | MISSING |
| Dates | PARTIAL on placement only |
| Tracking / approval / completion / cancel workflows | MISSING |

Do **not** create a second campaign model. Extend `Campaign`/`CampaignPlacement` additively when contracted.

## Ad delivery gap

| Slot type | Status |
| --- | --- |
| PRE_ROLL, MID_ROLL, POST_ROLL, LOWER_THIRD | Constants + seed + tests |
| PERIMETER_OVERLAY, CORNER_OVERLAY, ROTATING_OVERLAY, SPONSORED_SEGMENT | Constants exist; not all seeded |

One ContentItem many slots: **YES** (tested). One campaign many placements: **YES** (tested). Multiple advertisers on different slots: **architecturally possible** (no unique advertiser-on-content constraint). Placement history: **rows persist**; no versioning table. **No production delivery infrastructure.** Alpha Auto UI is not an ad renderer.

## Measurement gap

Impressions, views, clicks, conversions, sales, revenue, completion, renewal, refund, chargeback, complaint, provider event, tracking event, measurement source/timestamp: **MISSING** in Bliss. Alpha Auto dashboards use order/ops stats, not ad measurement.

## Economics & Rate Intelligence gap (Bliss)

Future bounded context. **Do not implement in current Wedding Planner phases.**

Documented: architecture, integration points, reserved PostgreSQL schema, pricing-model vocabulary.

MISSING: EF entities/migrations, recommendation engine, quotes, market observations, n8n research jobs, AI extraction with provenance writes.

Must not: universal Alpha price, dollars-per-minute as the product, hard-coded launch-city rate cards, compiled 20/80 compensation, AI-invented rates without observations.

Market value stays separate from media ledger settlement.

## Financial engine gap (Bliss)

Transaction, ledger entry, advertiser payment, affiliate revenue, creator earnings/payable, Alpha revenue, processor fee, payout, refund, chargeback, FX, settlement, reconciliation, external provider IDs linked to BlissMatch: **MISSING**.

Alpha Auto **has** payment providers and ledgers for **orders**. Principle: providers move money; Alpha ledger records meaning — **not implemented for media**. **Do not** integrate live Wise/PayPal for Bliss in Mission 001/002.

## n8n findings

No n8n workflow JSON, webhook controllers named n8n, or orchestration project in the five audited repos.  
**There is nothing to migrate out of n8n yet.** If n8n exists only in a hosted account, it is **outside this git evidence**. Treat that as **UNKNOWN / BLOCKED** pending export of workflows.

## Global scalability

| Occurrence | Classification | Path |
| --- | --- | --- |
| Seed `CountryCode = PH`, `PrimaryGeography = Metro Manila`, `English / Tagalog` | ✅ Appropriate **test data** | `Phase1DataSeeder.cs` |
| Opportunity `MarketCountryCode = PH` | ✅ Test data | same |
| Alpha Auto `CountryCurrencyService` PH/MX/US | ⚠️ Potential hard-coding | `alpha-backend/Services/CountryCurrencyService.cs` |
| PayMongo forced PHP | ❌ Architectural coupling to PH rails | `PayMongoProvider.cs` |
| Auth default country MX / language `es` | ⚠️ | `AuthController.cs` |
| Frontend checkout PH/MX/US maps | ⚠️ | `alpha-mvp-v2` checkout |

Bliss core entities are **global by architecture** (ISO-like country codes, language strings). Alpha Auto commerce is **operationally** PH/MX/US-centric.

## Provider coupling

| Provider | Bliss | Alpha Auto |
| --- | --- | --- |
| Npgsql/Supabase Postgres | Driver only | Core DB |
| Awin / Levanta / ACCESSTRADE | MISSING | MISSING |
| Firecrawl / Tavily | MISSING | MISSING |
| Wise | MISSING | MISSING |
| PayPal / Stripe / Xendit / PayMongo / Maya / HitPay | MISSING | Tightly in `Services/Providers/*` and controllers |
| n8n | MISSING | MISSING in git |

Bliss business logic is **not** owned by affiliate networks. Alpha Auto **payment** logic is owned by provider classes (acceptable adapters) but webhooks/auth are uneven (PayMongo unsigned webhook).
