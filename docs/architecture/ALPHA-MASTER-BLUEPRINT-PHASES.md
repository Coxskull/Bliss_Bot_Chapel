# Alpha Master Blueprint phases

**Date:** 2026-10-06
**Assigned to:** Erwin
**Status:** OPEN / PENDING IMPLEMENTATION + END-TO-END EVIDENCE

This is the implementation sequence for two controlling amendments:

1. Alpha Advertising Real Estate Catalog. Screen geometry, time, exclusivity, creator authorization, Economics valuation, Ask Alpha retrieval, and proof of delivery.
2. Alpha Creative Academy Reference Library. The 50 prototypes as retrievable quality benchmarks.

For implementation, this Catalog sequence supersedes the earlier Advertising Real Estate Catalog amendment and the later Commercial Inventory Precision and Delivery Verification addendum. There is one Catalog specification. The Academy sequence is the controlling blueprint for the 50 prototypes.

This document is a phase plan. It is not an Engineering Contract, not a price, and not completion evidence. Verbal confirmation does not close either amendment.

## Two systems

| System | Question it answers | What it must not do |
| --- | --- | --- |
| Creative Academy Reference Library | How good must the advertisement look? | Define placement, geometry, or price |
| Advertising Real Estate Catalog | Where may it appear, and how much authorized space does it receive? | Define the quality class or invent a rate |
| Inventory Showcase Library | How do we visually explain an approved product? | Become the contract, the geometry spec, or the price |
| Economics | What is the authorized commercial value? | Accept a rate invented by Ask Alpha or by an illustration |
| Ask Alpha | Show, explain, compare, and negotiate inside authorized rules | Invent a product, a duration, a frequency, an exclusivity, an availability, or a price |
| Bliss | Which creator and advertiser opportunities are compatible? | Sell inventory the creator has not authorized |
| Proof of delivery | What did Alpha actually deliver? | Fabricate impressions, scans, clicks, or performance |

Creative Academy trains quality. The Catalog defines placement. The ships use both. They stay separate.

## Standing rules for every phase

- Economics remains the only pricing authority. A missing Economics result stays unstated.
- Ask Alpha retrieves an ACTIVE Inventory Product. It does not invent one from a picture.
- Showcase artwork is educational. Brand names in the supplied guide (AutoMax, BelloCafe, and the others) are not templates.
- Official occupancy comes from a versioned geometry specification. Percentages drawn on `ARE-GUIDE-001-V1` are illustrative.
- Creator content is protected first. Premium means more of the creator-authorized inventory, not permission to cover more of the creator.
- Academy prototypes teach craftsmanship. They do not supply the business name, person, product photo, environment, words, or composition of the next advertisement.
- Consistency of excellence is required. Consistency of appearance is refused.
- AI may not promote its own output into an ACTIVE Academy reference. A human approves a professor.
- Reuse known intelligence. Do not pay a model twice to rediscover a stored analysis.
- Green does not send. Delivery stays `NOT_SENT` until a real authorized delivery exists. Campaign ready stays false until a human records it.
- Hosted acceptance is not claimed by these phases.
- Extend `AdInventorySlot`, Economics, Ask Alpha, Bliss matching, and the Creative Academy. Do not rebuild a working component to host a new name.

## What is already on disk

The upload dock is `assets/alpha-prototypes/`.

- `creative-academy/inbox/` is empty and waiting for `ACA-001-V1` through `ACA-050-V1`.
- `creative-academy/MANIFEST.tsv` maps those 50 IDs onto the existing niche roster, in niche order, status `AWAITING_UPLOAD`.
- `advertising-real-estate/showcase/ARE-GUIDE-001-V1.png` is the supplied catalog infographic.
- `ARE-001-V1`, `ARE-002-V1`, and `ARE-003-V1` are waiting for the separated showcase illustrations.

Existing local Academy teachers (VidaCare, FreshMart, Nova Fit, Taller Ruta, Brava Moto) stay where they are. They are not silently renumbered into `ACA-001` through `ACA-010`.

## Phase 0 — Control and upload docks

**Purpose.** Give the owner one place to file prototypes and one sequence that later phases must follow.

**Done when.**

- This phase plan is in the repository.
- The upload dock and the 50-row manifest exist.
- The catalog guide image is stored as `ARE-GUIDE-001-V1` and labeled educational.
- Both amendments remain OPEN.

**Evidence.** The folder listing, the manifest, and this document. A screenshot of files in storage is not later-phase acceptance.

This phase does not add a table, an API, a price, or an Ask Alpha behavior change.

---

## Catalog track

### ARE-1 — Showcase library records

Register showcase records for `ARE-001-V1`, `ARE-002-V1`, `ARE-003-V1`, and `ARE-GUIDE-001-V1`.

Each record carries showcase ID, version, linked Inventory Product IDs when those products exist, tier, asset path, description, slot map, advertiser count, creator protected area, disclaimer, and lifecycle `DRAFT`, `HUMAN REVIEW`, `ALPHA APPROVED`, `ACTIVE`, `SUPERSEDED`, or `RETIRED`.

The guide record states that it is an overview and that its printed percentages and any printed prices are not authoritative.

**Done when.** Each uploaded file has one record, the disclaimer is stored on the record, and Ask Alpha cannot treat a `DRAFT` showcase as a sellable product.

**Reuse.** Asset storage already used by Academy lessons and operations generated files. Do not create a second file host.

### ARE-2 — Geometry and occupancy

Define rectangular slots as versioned data:

`LEFT_VERTICAL`, `RIGHT_VERTICAL`, `TOP_LEFT`, `TOP_RIGHT`, `BOTTOM_LEFT`, `BOTTOM_RIGHT`, `BOTTOM_FULL`.

Each slot stores slot ID, position, width, height, coordinates, aspect ratio, screen-area allocation, creator-safe boundary, QR-safe area, content-safe area, permitted creative types, compatible neighbors, incompatible configurations, maximum simultaneous advertisers, tier eligibility, device variant, active flag, and version.

Occupancy is deterministic and versioned:

allocated advertiser area / defined applicable sellable area = occupancy value.

The function reads the specification. It does not estimate pixels from `ARE-GUIDE-001-V1`.

Rectangular rules: straight edges, exact dimensions, no bleed, no blurred filler, no stretch, no overlap. The creative inside the rectangle may be visually strong. The boundary stays exact.

`AdInventorySlot` remains the content-bound slot on a creator item, including `DurationSeconds` as a pricing variable rather than a dollars-per-minute rate. Catalog geometry is the product definition those slots must match. Phase 28 balanced pairs (2, 4, or 6) stay a density rule. They are not replaced by this geometry.

**Done when.** A test computes occupancy from stored geometry for a known configuration, rejects a missing specification, and never reads the guide image to invent a percentage.

### ARE-3 — Inventory Products

Create permanent Inventory Products. Initial IDs:

| Product | Tier label | Configuration | Exclusivity | Showcase |
| --- | --- | --- | --- | --- |
| `ARE-P01` | PREMIUM | `LEFT_VERTICAL` plus bottom remainder. One advertiser. Creator keeps the protected center. | Exclusive when the product, the creator authorization, availability, and Economics all allow it | `ARE-001-V1` |
| `ARE-P02` | PREMIUM | Advertiser A: `LEFT_VERTICAL` + `BOTTOM_LEFT`. Advertiser B: `RIGHT_VERTICAL` + `BOTTOM_RIGHT`. Distinct brand DNA per advertiser. | SHARED. Maximum simultaneous advertisers = 2 | `ARE-002-V1` |
| `ARE-S01` | ENTRY or STANDARD | Smaller approved rectangles such as top pair, bottom pair, or side inventory | SHARED unless a later approved product says otherwise | `ARE-003-V1` |
| `ARE-E01` | EXCLUSIVE | An exclusive product only where inventory rules permit it | EXCLUSIVE | The premium showcase that matches the geometry, never a promise by itself |

External customer language stays on four labels: ENTRY, STANDARD, PREMIUM, EXCLUSIVE. Internal configurations can be more precise. Ask Alpha shows the relevant label, not every slot coordinate.

Every product stores product ID, configuration ID, version, tier, included slot IDs, occupancy, advertiser capacity, exclusive or shared, duration options, frequency options, creator authorization requirement, device variants, platform compatibility, disclosure requirement, showcase ID, Economics pricing rule version when one exists, availability, and `ACTIVE` or `INACTIVE`.

**Done when.** Ask Alpha can list these products from stored rows. A request for an unknown shape, including "70% of the screen", returns no invented product.

### ARE-4 — Duration and frequency

Store duration and occurrence options as data on the product.

Duration, where applicable: `DisplayDurationSeconds`, start offset, end offset, campaign period, continuous or intermittent.

Frequency, where applicable: duration per display, occurrence count, frequency, episode count, livestream count, campaign start and end, rotation rule, continuous or intermittent.

15 seconds once and 15 seconds twenty times are different commercial requests. The Catalog records the request. Economics calculates the authorized value. This phase stores no price.

**Done when.** Changing occurrences from 5 to 20 changes the stored commercial request and does not invent a new rate.

### ARE-5 — Device, platform, disclosure, and legibility

Approved variants, as applicable: desktop landscape, mobile landscape, mobile portrait, livestream, recorded video, and later environments only after approval.

Each variant status is one of: `SUPPORTED`, `SUPPORTED WITH APPROVED VARIANT`, `NOT SUPPORTED`, `NEEDS REVIEW`.

Platform fields: `PlatformCompatibility`, placement method, disclosure requirement, creator authorization requirement, restrictions, policy version, and review date. Platform permission is separate from technical fit.

Legibility minima, when specified: text, logo, CTA, QR, disclosures, safe margins, and product visibility. If required information cannot stay legible, the result is QA fail, then recompose, a different creative, or a different inventory product. Do not keep shrinking.

**Done when.** "Will this work on mobile?" returns the stored variant status. A missing status returns `NEEDS REVIEW` or an explicit absence. It does not guess, and it does not scale the desktop asset until it is unreadable.

### ARE-6 — Creator authorization

A creator authorization record names the protected content region, the rectangles the creator allows, maximum simultaneous advertisers, exclusive and shared possibilities, and platform restrictions.

Required sequence:

1. Protect creator content.
2. Define the available perimeter.
3. Define rectangular inventory.
4. Create Inventory Products.
5. Record creator authorization.
6. Assign the commercial tier.
7. Apply duration.
8. Apply frequency.
9. Apply exclusivity.
10. Economics determines authorized value.

Ask Alpha cannot offer a product the creator has not authorized.

**Done when.** A product that exists in the catalog and is absent from the creator authorization is withheld. The stored content item and its `AdInventorySlot` rows are not rewritten by the reading.

### ARE-7 — Economics inputs

Pass approved inputs to the existing Economics engine: Inventory Product, occupancy, position, duration, frequency, occurrences, exclusivity, creator reach, geography, demand, campaign period, platform and device, packages, and any other variable Economics has already approved.

No variable becomes pricing authority until Economics approves it. Ask Alpha reads the Economics result. A missing or declined result states that no authorized number is on file.

**Done when.** A catalog phase test asks Economics through the existing price path and asserts that the Catalog module itself contains no rate and no discount.

### ARE-8 — Ask Alpha retrieval

Required order:

1. Read the customer need.
2. Retrieve ACTIVE Inventory Products.
3. Filter by creator authorization.
4. Filter by availability.
5. Filter by platform and device.
6. Compare screen real estate.
7. Compare exclusivity.
8. Resolve duration.
9. Resolve frequency and occurrences.
10. Retrieve the Economics-authorized price.
11. Show the approved showcase for that product.
12. Discuss only inside the negotiation boundary already enforced for Ask Alpha.
13. Reserve or escalate to a human.

Customer paths:

- A first campaign with a small budget retrieves ENTRY or STANDARD and shows `ARE-003-V1` when that showcase is ACTIVE.
- A request for more visibility retrieves PREMIUM and shows `ARE-001-V1` or `ARE-002-V1` according to shared versus exclusive.
- A request to appear without another advertiser retrieves eligible EXCLUSIVE products only, after creator authorization and availability. If none are available, Ask Alpha says so.
- A mobile question returns the stored variant status.
- A change from 5 occurrences to 20 updates the request and retrieves the Economics result again.
- A demand for 70% of the screen at an invented price retrieves alternatives, states that the option is not authorized, or escalates. It does not create `ARE-Pxx`.

Ask Alpha may explain, compare, collect preferences, name a budget mismatch, and recommend an approved alternative. Ask Alpha may not invent a rate, a discount, inventory, availability, duration, frequency, exclusivity, a platform exception, a campaign result, or a binding exception.

The conversation is written to the existing acquisition ledger.

**Done when.** The six acceptance conversations and the critical failure conversation pass against stored rows, with the showcase ID that was retrieved recorded beside the product ID.

### ARE-9 — Proof of delivery

Lifecycle, recorded only when the event happened:

`RESERVED`, `SCHEDULED`, `CREATIVE READY`, `RENDERED / PREPARED`, `DISPLAY STARTED`, `DISPLAY VERIFIED`, `DURATION RECORDED`, `OCCURRENCES RECORDED`, `CAMPAIGN DELIVERY RECORDED`, `COMPLETED`.

Where the event is real, store Inventory Product ID, campaign ID, advertiser ID, creator and content ID, scheduled placement, actual placement, scheduled duration, actual duration, scheduled occurrences, delivered occurrences, timestamps, QR availability, exceptions, and delivery status.

Impressions, scans, clicks, and engagement stay absent until a lawful measurement exists. An empty delivery record stays empty.

**Done when.** "How will I know the advertisement ran?" returns the stored delivery evidence or an explicit statement that delivery has not been recorded. It does not invent a count.

### ARE-10 — Mission Control reading

Expose, on the existing operations console rather than a second product, the fields needed to audit a product: ID, version, tier, occupancy, exclusive or shared, capacity, creator authorization, device and platform status, disclosure, availability, reservations, duration, scheduled occurrences, delivered occurrences, exceptions, Economics pricing version, Ask Alpha negotiation status, proof-of-delivery status, and human escalations.

**Done when.** An operator can read those fields for one product. The page does not compute a price.

### ARE-11 — Catalog end-to-end evidence

Run Tests 1 through 6 and the critical failure test from the Catalog acceptance note, in the Ask Alpha conversation surface, against the database.

Required evidence:

- Inventory Product retrieved
- Showcase ID retrieved
- The showcase image actually displayed
- Geometry read from structured data
- Creator authorization and availability checked
- Economics rule or an explicit "no authorized price on file"
- Conversation ledger row
- Unsupported request handled without invention
- Human escalation where the boundary requires it
- Screenshot or recording of each test

The Catalog amendment stays OPEN until that evidence is reviewed. Database tables, APIs, unit tests, and a verbal confirmation do not close it.

---

## Academy track

### ACA-1 — Prototype dock and registration

The owner uploads `ACA-001-V1` through `ACA-050-V1` into `assets/alpha-prototypes/creative-academy/inbox/` and marks `MANIFEST.tsv`.

Registration then connects each file to a reference record: reference ID, prototype number, niche, version, asset location, approval status, lifecycle, provenance, rights where known, created date, and approved date.

**Done when.** Gate 1 can show 50 assets, 50 IDs, and a join from record to file. A directory listing alone is not done.

**Reuse.** The niche roster already has these 50 niches. Registration binds a file to that niche. It does not create a second roster.

### ACA-2 — Lifecycle and quality metadata

Lifecycle: `CANDIDATE`, `HUMAN REVIEW`, `ALPHA APPROVED`, `ACTIVE`, `SUPERSEDED`, `RETIRED`.

Only `ACTIVE` references are eligible for normal retrieval.

Quality attributes, when a human classifies them, use `REFERENCE STRENGTH`, `STRONG`, `SUPPORTING`, or `NOT APPLICABLE` for color power, saturation, contrast, lighting, highlights, shadows, product realism, human realism, material realism, depth, separation, typography, hierarchy, composition, commercial polish, screen impact, hero presence, and Premium Dominant Presence.

Each reference also stores `learn` and `doNotCopy`. Persist that classification. Do not pay a model to rediscover it on the next job.

**Done when.** A reference can be retrieved with its learn list and its do-not-copy list, and a superseded version remains readable for historical jobs.

### ACA-3 — Global Alpha Quality DNA

Promote the existing reference-parity qualities into a versioned Global Alpha Quality DNA record: rich commercial color, controlled saturation, tonal contrast, highlight and shadow craft, dimensional depth, foreground and background separation, photorealism where people or products are used, material realism, typography, hierarchy, hero presence, commercial polish, screen impact, and Premium Dominant Presence.

This DNA sets the quality class. Advertiser Brand DNA sets the identity. The two records stay separate. Pharmacy, automotive, restaurant, coffee, law, and cosmetics work must not be forced into one face, one pose, one gradient, or one headline position.

**Done when.** Production reads the DNA version that was ACTIVE at generation time, and a Brand DNA palette is preserved rather than replaced by a reference brand's colors.

### ACA-4 — Four layers and selective retrieval

Keep four layers distinct:

1. Global Alpha Quality DNA.
2. Niche quality intelligence.
3. Market and cultural intelligence for the actual market (Dominican Republic, Panama, Colombia, Philippines, Malaysia, Indonesia, and later approved markets). Specific market context. No generic cast label when the market is known.
4. Advertiser Brand DNA: logo, colors, typography, imagery, tone, terminology, products, CTA, requirements, and rejected concepts.

Retrieval for a new brief selects the relevant ACTIVE references, typically 2 to 5, and stores why each was selected. Example reasons: niche match, product realism, lighting and depth, typography. The job does not attach all 50 images.

Formula:

Global Alpha Quality DNA + selected Academy references + niche intelligence + market context + advertiser Brand DNA + campaign objective + inventory specification = a new original creative.

**Done when.** Gate 2 shows a brief, the reference IDs retrieved, and a stored reason for each ID, without a person attaching the files by hand.

### ACA-5 — Workers, adaptation, and originality

Use the existing worker responsibilities. Do not add a new worker for a job an existing role already has:

- Curator / research for market and niche
- Brand strategist for campaign direction
- Art director for composition and the retrieved Academy notes
- Copywriter for original language
- Production creative worker for the original asset
- Creative review / chaperone for quality, requirements, and originality
- Deterministic QA for mechanical checks
- Human for final authority

Inventory adaptation recomposes the creative into the Catalog geometry for the purchased product. It does not scale the Academy prototype down and call that a placement. Academy prototype dimensions are not podcast-advertising dimensions.

Similarity against Academy references, prior Alpha demos, and other active advertiser creatives returns `PASS`, `REVIEW REQUIRED`, `RECOMPOSE`, `REGENERATE`, or `HUMAN REVIEW`. Copying a reference identity fails.

**Done when.** An adapted asset for a known product uses that product's slot geometry, and a creative that reproduces a reference brand, face, product, or headline fails the originality check.

### ACA-6 — Rejection taxonomy and regression suite

Store failure reasons so rejected work teaches QA and does not become a positive reference. Initial codes include `FLAT_LIGHTING`, `MUDDY_COLORS`, `WEAK_CONTRAST`, `GENERIC_AI_LOOK`, `UNREALISTIC_SKIN`, `ANATOMY_FAILURE`, `PRODUCT_REALISM_FAILURE`, `MATERIAL_REALISM_FAILURE`, `POOR_HIERARCHY`, `UNREADABLE_TYPOGRAPHY`, `OVERLOADED_COMPOSITION`, `WEAK_SCREEN_IMPACT`, `BRAND_DNA_VIOLATION`, `REFERENCE_TOO_SIMILAR`, `INVENTORY_GEOMETRY_FAILURE`, and `QR_FAILURE`.

Freeze 10 to 20 representative briefs. Re-run them when the provider, model, prompt architecture, retrieval, Global Quality DNA, art direction, creative worker, creative QA, or inventory adaptation changes. Compare the new output with the accepted baseline.

A run that returns HTTP 200 and looks flatter, more generic, or more obviously synthetic than the baseline is a quality failure.

**Done when.** The suite names its briefs, the baseline assets, and the pass or fail for the latest run. The suite is not marked passed because the API responded.

### ACA-7 — Provenance, provider router, and cost

Store source, generation provider, ownership, approval history, permitted internal use, permitted provider use, restrictions, version, lifecycle, dates, and approving authority where they are known.

Do not send an asset to an external provider when its restrictions forbid that use.

Route generation through a replaceable provider adapter. The Academy record stays in Alpha storage and the Alpha database. Provider memory is not the system of record.

Follow the existing cost rules: reuse before generate, software before AI, cache known intelligence, retrieve a few references rather than all 50, use the least expensive capability that can do the job, and do not open a new subscription without a named capability gap.

**Done when.** A generation job records provider, model, and cost, and a second job for the same ACTIVE reference reads the stored quality metadata instead of calling a model to re-analyze it.

### ACA-8 — Autonomous creative acceptance

Use a new fictitious advertiser that is not one of the 50 prototypes, plus an approved test market, niche, objective, brand requirements, and an exact Inventory Product specification. Do not coach the creative by hand during the run.

Expected flow: brief, market and niche research, Global Quality DNA, reference retrieval with reasons, new advertiser Brand DNA, brand strategy, art direction, original copy, generation, inventory adaptation, similarity check, creative QA, deterministic QA, human review.

Three gates, all required:

1. **Storage.** 50 assets, 50 reference IDs, metadata, lifecycle, and version.
2. **Retrieval.** The system selects ACTIVE references and records why.
3. **Production quality.** A new advertisement follows the advertiser Brand DNA, reaches the Academy quality class, does not clone a reference, fits the inventory specification, and can be reviewed by a human.

Required evidence includes the finished original, the inventory-adapted creative, similarity result, QA result, any rejection and retry, cost record, workflow trace, and human review. A screenshot of 50 files or 50 database rows does not pass Gate 3.

The Academy amendment stays OPEN until that evidence is reviewed.

---

## J-1 — Joint acceptance

One path, still two systems:

1. Ask Alpha retrieves an ACTIVE Inventory Product and its showcase.
2. Economics supplies the only price, or states that none is authorized.
3. The Academy retrieves 2 to 5 quality references for a new brand and records why.
4. Production creates an original creative and recomposes it into that product's geometry.
5. Creator authorization and platform status were checked before the offer.
6. Delivery evidence is present only if a display actually occurred.
7. Delivery remains `NOT_SENT` and campaign ready remains false unless a separate human authorization says otherwise.

**Done when.** One recorded conversation shows the product ID, the showcase ID, the Academy reference IDs with reasons, the Economics source, the adapted creative, and the delivery status. Both parent amendments stay OPEN until the owner reviews that evidence.

## Evidence the owner still has to supply

- The 50 prototype files, named `ACA-001-V1` through `ACA-050-V1`, in `assets/alpha-prototypes/creative-academy/inbox/`.
- The three separated showcase illustrations in `ARE-001-V1`, `ARE-002-V1`, and `ARE-003-V1`.
- Human `learn` / `doNotCopy` notes and quality classifications. They are not invented from the filename.
- Review of the Catalog conversation recordings and the Academy production recording before either status leaves OPEN.

## Status

Catalog: OPEN / PENDING IMPLEMENTATION + END-TO-END EVIDENCE.

Academy Reference Library: OPEN / PENDING IMPLEMENTATION + AUTONOMOUS CREATIVE ACCEPTANCE EVIDENCE.

Phase 0, the upload dock, is in the repository. A later change installs the draft catalog engine at `/api/operations/blueprint` and the operations view `/operations#/catalog`. That engine names the draft products, refuses unrecorded geometry, refuses an invented price, and retrieves Academy references only when a file is ACTIVE. It does not mark ARE-11 or ACA-8 accepted. Both amendments remain OPEN until the owner reviews end-to-end evidence.
