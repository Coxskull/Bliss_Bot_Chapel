# ALPHA-345 — Commercial catalog / Creative Academy integration
Engineering workstream · 2026-11-07 · **OFFLINE ONLY, STOP ENGAGED**

## Reused systems and authoritative locations

| Capability | Existing code or record | Actual limit |
| --- | --- | --- |
| Ask Alpha commercial catalog | `Bliss.Api/Controllers/BlueprintPhaseController.cs`, `Bliss.Api/Operations/BlueprintPhaseService.cs`, `Bliss.Domain/AdvertisingRealEstate/RealEstateCatalog.cs`, `frontend/operations/catalog.js` | Legacy blueprint demo used owner-wide TSV approval; new `ask-evidence` inspection never makes a sale |
| Ask Alpha visitor gateway | `Bliss.Api/Controllers/AskAlphaVisitorConsentController.cs` and `Bliss.Api/Security/AskAlpha*` | Visitor consent and advertising product authorization are different |
| Economics | `Bliss.Api/Controllers/EconomicsController.cs`, `Bliss.Infrastructure/Persistence/QuoteService.cs`, `Bliss.Api/Demonstrations/EconomicsAcceptedPriceReader.cs` | Accepted quotes exist, but catalog product/creator-specific pricing binding is not established |
| Bliss creator and inventory | `Bliss.Domain/Entities/Creator.cs`, `ContentItem.cs`, `AdInventorySlot.cs`, `BlissMatch.cs` | `AdInventorySlot.IsAvailable` is not a product-level reservation or consent grant |
| Wedding Planner and Brand DNA | `Bliss.Infrastructure/Persistence/WeddingPlannerBrandDnaService.cs`, `WeddingPlannerEconomicsHandshakeService.cs`, existing `AcademyProductionBrief`/review records | Reusable customer inputs; approval cannot be inferred from sample art |
| Creative Academy | `CreativeAcademyController.cs`, `CreativeAcademyService.cs`, `GlobalVisualDnaGate.cs`, `frontend/public/ad-composer.js` in PR #333 | Browser-original PNG is functional in CI; premium photoreal quality not independently judged |
| Product catalog and geometry | Existing `RealEstateProducts`, `OWNER-GEOMETRY.tsv`, `OwnerGeometryFile`, `CatalogGeometrySafety` | Geometries are owner recommendations on a 1920x1080 canvas, not creator-authorized sellable occupancy |
| Showcase asset storage | Existing repo asset paths and `BlueprintPhaseService.OpenShowcase` | Four files on disk; art is explanatory, not a contract |
| Campaign/delivery | `CampaignPlacementRun`, `CampaignMeasurementController`, `CampaignMeasurementService` | PLANNED/NOT_SENT is not displayed/verified delivery; no campaign was run |
| Database | Existing `BlissDbContext` PostgreSQL mapping | **No new tables, schema migrations, live database changes, or provider resources** |

## Implemented integration

`POST /api/operations/blueprint/ask-evidence` is **read-only** and reuses the existing `RealEstateProducts` table, `RealEstateCatalog.Reply` intent classifier, owner geometry reader, and existing `OpenShowcase` file resolver. It fetches original image bytes via the pre-existing image URL and calculates SHA-256 for each verified asset. It does **not** call `MirrorProductsAsync`, change STOP, create quotes, send a campaign or write the conversation ledger.

The route can show an ACTIVE, version-matched **candidate under engineering inspection**, but always reports:

- `CREATOR_SPECIFIC_AUTHORIZATION_MISSING` — the `OWNER-AUTHORIZATION.tsv` line `creatorAuthorized=true` is a generic owner-supplied claim, not creator-specific, product-specific permission.
- `PRODUCT_SLOT_AVAILABILITY_UNVERIFIED` — catalog product IDs have no approved binding to an individual `AdInventorySlot`, creator, time window or reservation.
- `DEVICE_PLATFORM_VARIANTS_UNVERIFIED` — 1920x1080 recommendations do not prove mobile/vertical legibility or platform acceptance.
- `DURATION_FREQUENCY_UNVERIFIED` — no approved 20-occurrence inventory schedule was retrieved.
- `PRICING_UNAVAILABLE` — Economics does not have a proven accepted quote tied to these product/version/slot terms; no illustrative price is disclosed.
- `LOCALIZATION_REVIEW_REQUIRED` — DO, PA, CO, PH, MY, ID require evidence of language, location, casting and rights.
- `INSPECTION_ONLY_NOT_A_SELLABLE_OFFER`, `offerEligible=false`, `campaignReady=false`, `delivery=NOT_SENT`, `modelCalls=0`.

The existing operator catalog JavaScript now uses this strict path for questions, labels the image as non-authoritative, and surfaces source image SHA-256 and refusal reasons. No invented customer approvals or pricing.

## Geometry / creator-content safety

Stored recommended canvas: **1920x1080**. Mathematical union results from the exact `OWNER-GEOMETRY.tsv` coordinates (not illustrator percentages):

| Proposed configuration | Union allocated area | Percent of **total frame**, NOT sellable area | Status |
| --- | ---: | ---: | --- |
| ARE-P01 / ARE-E01 (left + full bottom) | 576,000 px² | 27.78% | Protected creator region needs binding |
| ARE-P02 (left/right + split bottom) | 921,600 px² | 44.44% | Protected creator region needs binding |
| ARE-S01 (4 corners) | 460,800 px² | 22.22% | Protected creator region needs binding |

`CatalogGeometrySafety` uses exact rectangle union (no double count), rejects out-of-bounds geometry and flags illegal overlaps and creator-content intersections when the protected area is explicitly provided. A fixture-only hypothetical center of (320,180) 1280x720 tests that these four configurations do not intrude; this is **not an independently signed creator-area approval**, and the default inspection therefore reports `PROTECTED_CREATOR_REGION_UNVERIFIED`.

No **70% for $10** product or price was created. Total-frame share is not a product price and does not determine economic occupancy or screen rights.

## Creative-quality reference assets

See `ALPHA-345-ASSET-REGISTER.json` — nine distinct records with provenance and content hashes/status. Quality group: **FizzUp, AquaPure and NaturaViva** verified as separate image bytes in the connected library (1672x941 PNG); **PortoCafé and Frutália** not verified as exact standalone files and require upload/identification. These remain quality references, not advertisement templates or registered product showcases. The four ARE images are on Git with stable Git blob digests and remain a separate group.

Visual references evidence transferable principles (lighting, material texture, product hierarchy, contrast and typography); **fictional product or environmental claims have no approved commercial substantiation**. Do not reproduce sample packaging, scene, slogan, brand or people for a real advertiser.

The existing `GlobalVisualDnaGate.EvaluateIndependent` now evaluates separately:
1. **BRAND**: approved brand snapshot, logo/media rights, specific customer colors, products, copy, language, typography and claims.
2. **QUALITY**: original composition, professional photography/lighting, color, depth, realism, materials, hero dominance, typography and documented human review.
3. **LOCALIZATION**: country is DO/PA/CO/PH/MY/ID with independent market/language/environment/casting rights review.

All three must pass before **eligibility for advertiser review**. Even three PASS states do not authorize a production campaign: `campaignReady=false`, `NOT_SENT`. Synthetic `LumiLokal` (fictional Philippines brief) exercises positive/negative gating without claiming an actual human visual-quality approval.

## Seven Ask Alpha real API test prompts (disposable fixture)

| Customer request | Existing intent classification | Strict commercial result |
| --- | --- | --- |
| Inexpensive first campaign | ENTRY | Retrieve ARE-S01 candidate image only, price/permission unavailable |
| Premium more screen | PREMIUM | Retrieve ARE-P01/P02 draft configuration evidence only |
| No advertiser beside me | EXCLUSIVITY | Retrieve P01/E01 candidates without guaranteeing exclusivity |
| Works on mobile? | DEVICE | No compatible device variant proven — review required |
| 20 times instead of 5 | FREQUENCY | No authorized schedule/price for changed occurrence count |
| How do I know it ran? | DELIVERY | No delivery verification evidence or invented impression count |
| 70% of screen for $10 | UNSUPPORTED | Refuse the invented geometry/price; no sellable offer |

`Bliss.Tests/Api/BlueprintCommercialEvidenceApiTests.cs` issues the seven **real HTTP calls** to the existing ASP.NET controller on an isolated EF InMemory fixture containing synthetic ACTIVE rows in the **existing** `RealEstateProducts` table. It GETs the actual showcase image bytes through existing HTTP routes and verifies each SHA-256 matches the receipt. It asserts no new quote, reservation, campaign, or conversation record is created by those calls.

**Test execution state:** pending exact-head GitHub CI result until runs are complete. Existing legacy catalog tests are separate and must not be confused with new verified-offer acceptance.

## Next required release actions (not authorized in this offline task)

Obtain explicit durable creator-to-inventory-product/version/slot/period rights; record protected-content area and device variants; bind creator-specific availability/exclusivity and schedules; attach an accepted Economics quote and negotiation policy to the exact product/version; provide real advertiser-approved brand assets and validated country localization; complete independent visual human QA and future delivery verification. Add canonical schema links only through an approved migration plan; do not introduce duplicate databases or services.

**Controls:** $0 additional spend. Global STOP ENGAGED. No production campaign, outreach, payment, merge, deployment, or live database mutation. No unapproved creative image license.
