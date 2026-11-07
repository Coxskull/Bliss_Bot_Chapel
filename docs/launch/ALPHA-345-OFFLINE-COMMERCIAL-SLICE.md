# ALPHA Directive 345 — Executable offline commercial evidence slice

**Directive:** ALPHA-CREATIVE-COMMERCIAL-SYSTEM-INTEGRATION-345  
**Repo:** `Coxskull/Bliss_Bot_Chapel` (existing authoritative application source).  
**Implementation:** Existing DRAFT PR #337, branch `feat/agm345-inventory-geometry-offline-20261107` off the main baseline `2ca8b933482e0ce4c0e9b953e043339628d59e6d`.  
**Mode:** `OFFLINE_SYNTHETIC_ONLY`. **$0 spending; no merge, database write/migration, deployment, outreach, payment or STOP change.**

## What we reused rather than duplicated

| Contract | Existing implementation | Current evidence |
|---|---|---|
| Inventory identity, approved statuses, showcase mapping and Ask Alpha intent | `Bliss.Domain/AdvertisingRealEstate/RealEstateCatalog.cs`, `JointAcceptance.cs` | Source implemented. A catalog draft/ACTIVE override alone is not a customer offer. |
| Owner geometry | `OwnerGeometry.cs`, `assets/alpha-prototypes/OWNER-GEOMETRY.tsv` | Machine-readable coordinate rows exist, but no per-creator notarized/signed perimeter or inventory reservation ledger has been verified. |
| Owner lifecycle status | `OwnerAuthorization.cs`, `OWNER-AUTHORIZATION.tsv` | File says creatorAuthorized/ACTIVE. It does **not** establish a creator-specific signed authorization/version for a commercial placement. |
| Inventory slot and campaign binding | `Bliss.Domain/Entities/AdInventorySlot.cs`, `CampaignPlacement.cs`, EF configurations | Existing database models, separate from the new **read-only** evidence gate. |
| Economics authority | `EconomicsAcceptedPriceReader.cs`, `EconomicsNegotiationGate.cs`, `PricingRuleDocument.cs` | Real approved quote readback and negotiation exist; **not wired to this new offline proof**, so price is unavailable, not invented. |
| Creative Academy | `Bliss.Domain/CreativeAcademy/CreativeAcademy.cs`, Creative Acceptance models | Existing quality/policy framework; independent Brand PASS and Quality PASS for Directive 345 are **not yet linked to the new inventory evidence**. |
| Ask Alpha gateway | `RealEstateCatalog.Reply`, `GatewayAskAlphaSimulation.cs` | Existing classifier and local simulation reused; this PR adds a guarded offline seam, not another customer gateway. |
| Showcase source assets | `assets/alpha-prototypes/advertising-real-estate/showcase/` | ARE-001/002/003 and ARE-GUIDE files exist. **Asset approval not inferred from presence.** Git object references in `AGM345-OFFLINE-ASSET-EVIDENCE.tsv`. |

## Implemented machine-enforced guard

- `CommercialGeometryGuard.Review` requires **owner provenance/geometry version**, frame dimensions, explicit protected creator region and **declared sellable advertiser reference area**.
- Rectangles must be nonnegative, non-overlapping for the selected commercial product, fully inside the frame and outside the protected creator region. Missing/invalid/mixed geometry is withheld. **No geometry is inferred from an image.**
- The sweep-line `UnionArea` implementation deduplicates overlapping rectangles mathematically; rejected product overlap does not become valid merely because union area can be computed.
- The distinct occupancy metrics are recorded separately: **frame occupied area** versus **sellable advertising area**. Neither implies price.
- `CommercialInventoryReviewGate.SimulateConversation` uses `RealEstateCatalog.Reply`, then exact stored product identity and the immutable candidate inputs for creator/versioned approval, capacity/exclusivity, device compatibility, geometry, approved showcase digest, **PricingUnavailable** and negotiation handoff. It cannot create a product, emit a binding number, reserve inventory or claim delivery.
- Mobile is explicitly held until a separate owner-approved variant exists. **Fictional evidence never becomes real availability.**
- All records and transcripts carry `OFFLINE_SYNTHETIC_ONLY`, `CommitmentAuthorized=false`, `ReservationAuthorized=false`, `DeliveryVerified=false` and `NOT_SENT`.

### Deterministic fictional geometry example — **NOT** live inventory authority

Synthetic selected product `ARE-P01` combines `LEFT_VERTICAL` (320×1080) and `BOTTOM_FULL` (1280×180) on a 1920×1080 frame, with explicitly supplied center protected rectangle (x=320,y=180,w=1280,h=720) and sellable area 1,152,000. Version `G1` is **fictional** even though its dimensions are similar to the existing owner prototype.

- Union advertiser area: **576,000 square pixels**
- Frame reference: **2,073,600 square pixels**, occupancy **2,778 basis points**
- Declared sellable reference: **1,152,000 square pixels**, occupancy **5,000 basis points**
- No protected creator overlap; separate requested product slots do not overlap
- **Price:** PRICING_UNAVAILABLE. **Availability:** only simulated. **No reservation, campaign or delivered impression.**

## Acceptance matrix — what is covered versus still blocked

These are **not** 20 unconditional PASS declarations. Each entry states what is executable in this source slice; exact-head CI is required to mark even a source test passed.

| Case | Source-level scenario / current disposition |
|---|---|
| T01 Lower-budget advertiser | Existing Ask Alpha ENTRY classifier; no real approved quote/active entry source; **HOSTED BLOCKED** |
| T02 Premium visual presence | `SimulateConversation` selects the already stored `ARE-P01` through existing PREMUM intent and validates fictional geometry/permissions. **SOURCE TEST / PRICE BLOCKED** |
| T03 Exclusive inventory | Exact `EXCLUSIVE` product/advertiser-capacity check; no real exclusion reservation ledger. **SOURCE TEST / HOSTED BLOCKED** |
| T04 Mobile compatibility | Mobile not inferred; **REFUSAL TEST / VARIANT BLOCKED** |
| T05 Five versus 20 occurrences | Count retained in quote trace; neither is priced automatically. **SOURCE TEST / ECONOMICS BLOCKED** |
| T06 Proof of delivery | `DeliveryVerified=false` and NOT_SENT invariant. Real impressions remain **BLOCKED** |
| T07 70% screen/$10 | Existing `RealEstateCatalog.Reply` refuses unsupported geometry and fictitious economics. **SOURCE TEST** |
| T08 Missing Economics quote | `PRICING_UNAVAILABLE` + human escalation, never a dollar figure. **SOURCE TEST** |
| T09 Missing creator authorization | Refused before commercial presentation. **SOURCE TEST** |
| T10 Inactive inventory | `UNAVAILABLE` for DRAFT product. **SOURCE TEST** |
| T11 Conflicting occupancy | Product slot overlap is refused. **SOURCE TEST / LIVE RESERVATION LEDGER BLOCKED** |
| T12 Unsupported platform/device | Unknown/mobile held for review. **SOURCE TEST** |
| T13 Invalid geometry | Invalid/mixed/missing boundaries refused. **SOURCE TEST** |
| T14 Quality pass / Brand failure | New `CommercialCreativeQaGateTests` independently holds Brand when logo rights fail despite passing quality dimensions. **OFFLINE TESTED; REAL RIGHTS BLOCKED** |
| T15 Brand pass / quality failure | New gate holds reference quality when lighting craftsmanship is unapproved despite valid Brand DNA evidence. **OFFLINE TESTED; REAL HUMAN QUALITY REVIEW BLOCKED** |
| T16 Unsupported advertising claim | Fictional policy evidence rejects unsubstantiated claims and missing sponsorship disclosure. **OFFLINE TESTED; REAL POLICY AUTHORITY BLOCKED** |
| T17 Country-specific localization | PH, DO, PA, CO, MY, ID require explicit reviewer/language/casting context; no automatic cultural inference. **OFFLINE TESTED; REAL MARKET VERIFICATION BLOCKED** |
| T18 Synthetic delivery vs actual | Synthetic output never claims an impression. **SOURCE REFUSAL TEST / LIVE DELIVERY BLOCKED** |
| T19 Asset missing/unapproved | Missing or unapproved showcase denied, guide cannot define geometry. **SOURCE TEST / FINAL ASSET APPROVAL BLOCKED** |
| T20 Historical version integrity | Product/creator/geometry version mismatch denied; historical DB transaction snapshot untested. **SOURCE TEST / LIVE TRANSACTION BLOCKED** |

## Exact developer commands (reproducible)

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --filter FullyQualifiedName~CommercialInventoryReviewGateTests --no-restore --verbosity minimal
dotnet test Bliss.Tests/Bliss.Tests.csproj --no-restore --verbosity minimal
```

These are **requested commands** and require actual CI logs; `--no-restore` assumes the CI workflow has restored NuGet packages. No migration or external API is required.

## Directive 345 / P5 — source-level dual approval with existing Academy (2026-11-07)

New `Bliss.Domain/CreativeAcademy/CommercialCreativeQaGate.cs` reuses:

- `Bliss.Domain/Entities/WeddingPlannerBrandDna.cs`: the persisted Wedding Planner versioned `CONFIRMED` brand row (NOT a new brand database) plus **separate fictional reviewer evidence** for logos, approved copy, restrictions, language, palette and typography; merely confirming the brief does not establish trademark rights.
- `CreativeAcademy.ReferenceParityGate`'s full list of **16 existing visual craftsmanship dimensions**, ACTIVE `ReferenceLibrary` provenance and DO NOT COPY notes, and `GlobalVisualDnaGate` as a secondary hard-failure review. The quality teacher cannot become the advertiser identity.
- `CommercialGeometryGuard` versioned creator area. Missing layout keeps the independently passing brand and quality findings separate but blocks joint eligibility.

`BrandStatus` and `QualityStatus` are **independent**: a brand mismatch cannot be concealed by premium art, and attractive brand-compliant work cannot be called reference-quality without individual dimension evidence. Candidate byte-digest identity is a **third joint approval gate**, never silently merged into either dimension.

An additional fictional policy review holds unsupported claims, disclosure, sector, creator rights and platform checks. Market review holds uncertain casting/language/local styling for six declared countries (DO, PA, CO, PH, MY, ID). Lack of market evidence yields `LOCALIZATION_REVIEW_REQUIRED` and no false cultural assertion. The runner now records fictional `FICTIONAL_DOUBLE_PASS` / T14 / T15 / T16 / T17 decisions in its synthetic JSON artifact.

**Even a double PASS produces only `SyntheticCandidateEligibleForAdvertiserReview=true`; `ExternalUseAuthorized=false`, `CampaignReady=false`, and `Delivery=NOT_SENT`.** The synthetic sample hash, brand, quality reference, reviewers, and locale attestations are **not real-world approvals**. The named FizzUp, PortoCafé, AquaPure, NaturaViva and Frutália source assets are still not verified against approved reference IDs. There is no external creative publication or actual image generation in this slice. The new tests must pass at the **final exact head** before their source behavior can be accepted.

## Next gated integration, not silently completed

1. Obtain a **verified, current and versioned** creator/product/slot authority source, and store availability + exclusivity + occurrence data in the existing authorized inventory ledger; do not rely on OWNER-AUTHORIZATION.tsv alone for customer commitments.
2. Confirm which `ARE-*` assets have approved content hashes, lifecycle and association. This PR records GitHub **blob SHA-1** refs, which are **not** the actual byte-level SHA-256 and are **not** commercial approval. The five FizzUp, PortoCafé, AquaPure, NaturaViva and Frutália quality references have **not been separately matched/approved by exact asset ID** here; do not substitute generic Creative Academy images for them.
3. Wire a non-mutating authoritative Economics quote/version readback to the existing `EconomicsAcceptedPriceReader`, using the product ID + geometry version + occurrence quantity. No quote or negotiated dollar amount from a prompt.
4. Integrate approved Brand DNA, independent quality parity, market localization, claim policy and synthetic delivery-state receipts through existing Creative Academy and campaign models; no new databases or parallel matching engine.
5. Test T01–T20 end-to-end with authentic **offline** dependency stubs, then separately require explicit owner acceptance for any real action.

**Directive 345 remains OPEN until the full end-to-end offline story is verified.** This draft intentionally enforces fail-closed states for work not yet connected.
