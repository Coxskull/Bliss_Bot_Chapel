# ALPHA-345 — Offline acceptance matrix (T01–T20)

Date: 2026-11-07. Evidence scope: isolated GitHub CI fixture only. **Nothing here grants permission to launch.** Test results below describe the contracts exercised by code; exact-head green CI readback is required before saying a check passed.

| ID | Required question / invariant | Test evidence | Status before exact-head CI |
| --- | --- | --- | --- |
| T01 | Inexpensive first campaign | `BlueprintCommercialEvidenceApiTests` / ENTRY / ARE-S01 existing row and SHA-256 showcase | PENDING_CI |
| T02 | Premium larger display | Same HTTP case / PREMIUM / ARE-P01/P02 | PENDING_CI |
| T03 | No adjacent advertiser | EXCLUSIVITY / no creator proof or exclusivity reservation inferred | PENDING_CI |
| T04 | Mobile suitability | DEVICE / no format or device approval invented | PENDING_CI |
| T05 | 5 vs 20 displays | FREQUENCY / no schedule, occurrence price or capacity invented | PENDING_CI |
| T06 | Proof of display | DELIVERY / no impressions or delivery evidence invented | PENDING_CI |
| T07 | 70% screen for $10 | UNSUPPORTED / no proposed sellable product/price | PENDING_CI |
| T08 | Missing Economics quote | All HTTP fixture replies say PRICING_UNAVAILABLE, no quote created | PENDING_CI |
| T09 | Missing creator permission | Owner-wide TSV cannot create creator-specific consent | PENDING_CI |
| T10 | Inactive inventory | `CommercialAuthorityBoundaryTests` / DRAFT and mismatched metadata never offered | PENDING_CI |
| T11 | Conflicting simultaneous advertisers | Existing table has product capacity but no canonical product-slot-time reservation authority | BLOCKED — B-345-INV-01 |
| T12 | Unsupported mobile/platform | DEVICE refusal; platform variant/rights unavailable | PENDING_CI (refusal only) |
| T13 | Invalid / overlapping geometry | `Alpha345GeometryAndAssetManifestTests` exact union/out-of-bounds/protected region check | PENDING_CI |
| T14 | Visual quality PASS, brand FAIL | Independent quality/brand fixture toggles | PENDING_CI (synthetic gate only) |
| T15 | Brand PASS, visual quality FAIL | Independent quality unreviewed/originality withheld fixture | PENDING_CI (synthetic gate only) |
| T16 | Unsupported advertising claim | `CommercialAuthorityBoundaryTests` unsubtantiated claims fail brand axis | PENDING_CI |
| T17 | Country localization | Six-country restricted gate with unreviewed setting/language/casting | PENDING_CI (review gate only) |
| T18 | Synthetic versus real delivery | `SyntheticDeliveryReceiptTests` null counts, no sent/rendered claim | PENDING_CI (synthetic contract only) |
| T19 | File missing or unapproved | `Alpha345GeometryAndAssetManifestTests` 4 Git hashes, 3 library references, 2 exact names unresolved | PENDING_CI |
| T20 | Historical product/quote/asset version locking | Existing Brand DNA / Campaign Handoff append-only records; no catalog-product-version→quote→campaign binding | BLOCKED — B-345-LINEAGE-01 |

## Exact blockers

**B-345-INV-01 — Missing creator-to-product authorization and inventory reservation binding.**  
STATUS: BLOCKED. EXACT_ACTION_REQUIRED: human-approved creator/product/version/slot/device/period consent, protected content area and exclusivity/availability rules; then an approved canonical mapping in the existing database. WHY_REQUIRED: current `OWNER-AUTHORIZATION.tsv` records a generic owner approval, and `AdInventorySlot` points to a `ContentItem`, but `RealEstateProductRow` has no creator/slot/period-specific authority. EVIDENCE: repo source, strict API returns `CREATOR_SPECIFIC_AUTHORIZATION_MISSING`. AUTHORIZED_NEXT_STEP: prepare schema/link design and separate approval for any migration or protected data write.

**B-345-ECON-01 — Economics quote not bound to inventory product and version.**  
STATUS: BLOCKED. EXACT_ACTION_REQUIRED: accepted Economics quote/negotiation rules referencing exact product/version/slot/creator/period and market, not a benchmark or invented price. WHY_REQUIRED: `EconomicsAcceptedPriceReader` reads accepted quotes by quote ID, and no verified catalog-product quote lineage exists. AUTHORIZED_NEXT_STEP: design join using existing Economics records and request bounded staging approval when ready.

**B-345-ASSET-01 — Two exact quality references unresolved.**  
STATUS: BLOCKED. EXACT_ACTION_REQUIRED: owner identifies/uploads original PortoCafé and Frutália files with rights; do not substitute similarly named pictures. EVIDENCE: asset register has 3/5 verified-bytes with SHA-256 and four Git showcase objects. AUTHORIZED_NEXT_STEP: offline upload to the connected asset library, separately from sellable product showcases.

**B-345-QA-01 — Real creative quality and localization human evidence absent.**  
STATUS: BLOCKED. EXACT_ACTION_REQUIRED: advertiser-approved branding, country language, rights/casting/content permissions, material advertising claim substantiation and independent rubric evaluation of finished new-brand image. EVIDENCE: `GlobalVisualDnaGate.EvaluateIndependent` checks synthetic flags; not a visual reviewer or new provider. AUTHORIZED_NEXT_STEP: use an existing lawful source media/zero-credit composer, evaluate finished files with authorized human QA.

**B-345-LINEAGE-01 — Live delivery/provenance and hosted gates absent.**  
STATUS: BLOCKED. EXACT_ACTION_REQUIRED: immutable binding product/version+quote+creative+reservation+creator and verified player telemetry, plus separate hosted OIDC/TLS/backup acceptance. EVIDENCE: `CampaignPlacementRun` and `CampaignObservation` distinguish planning/observations from delivery; static synthetic receipt is NOT a real impression. AUTHORIZED_NEXT_STEP: signed staging acceptance with STOP engaged; no live campaigns.

## How to run

Existing CI: `dotnet test Bliss.Tests/Bliss.Tests.csproj --configuration Release --no-build --no-restore` on the PR's exact checked-out head. Filtered offline tests: `dotnet test Bliss.Tests/Bliss.Tests.csproj --filter "FullyQualifiedName~BlueprintCommercialEvidenceApiTests|FullyQualifiedName~CommercialAuthorityBoundaryTests|FullyQualifiedName~Alpha345IndependentQualityTests|FullyQualifiedName~Alpha345GeometryAndAssetManifestTests|FullyQualifiedName~SyntheticDeliveryReceiptTests"`.

All synthetic fixtures use the existing project's EF InMemory provider. No protected account writes, live prices, live creator permissions, emails, campaigns, new databases, migrations, merges or deployments are authorized.
