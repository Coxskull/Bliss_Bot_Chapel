# ALPHA GLOBAL MEDIA — Podcast Advertising Real Estate 16:9 Geometry V2

> **Historical version notice (ALPHA-357):** This V2 document is retained for traceability, not as the current quality standard. Its six-layout 270×540 side advertisements leave 97,200 pixels of unused outer strips. The actual existing Composer was corrected to full-height 270×720 side ads (Y=180–900) under **ALPHA-VIDEO-GEOMETRY-QUALITY-357-M2M**. Refer to `docs/architecture/ALPHA-VIDEO-GEOMETRY-QUALITY-357-RECEIPT.md` and the V3-QUALITY-357 geometry contract before constructing new layouts. No production activation is implied.


**Standard:** `ARE-PODCAST-16X9-V2`  
**Recorded:** 2026-11-07  
**Authority:** Owner-supplied 2/4/6-rectangle illustration; the **written pixel dimensions**, not the infographic's coverage labels, are the design reference.  
**Status:** `REFERENCE_STANDARD_ONLY_RENDERER_NOT_INTEGRATED` — geometry and validation code exists, but the existing demonstration compositor is **not yet wired to it**. No production rendering, paid release, or creator authorization is inferred.

## Canvas and slot contract

Canvas origin is top left, with X increasing rightward and Y downward. All positions and sizes below are **pixels**, inclusive at the left/top and exclusive at right/bottom. Each advertiser image must be delivered in **the exact width × height of its chosen slot**. A podcast frame must export at **1920 × 1080** (16:9). The public illustration file supplied by the owner is **1672 × 941**, so its pixels cannot be copied directly to establish output dimensions.

| Layout | Slot ID | X | Y | Width | Height | Individual frame area |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| **2** | BOTTOM_LEFT | 0 | 810 | 960 | 270 | 12.50% |
| **2** | BOTTOM_RIGHT | 960 | 810 | 960 | 270 | 12.50% |
| **4** | TOP_LEFT | 0 | 0 | 960 | 180 | 8.33% |
| **4** | TOP_RIGHT | 960 | 0 | 960 | 180 | 8.33% |
| **4** | BOTTOM_LEFT | 0 | 900 | 960 | 180 | 8.33% |
| **4** | BOTTOM_RIGHT | 960 | 900 | 960 | 180 | 8.33% |
| **6** | TOP_LEFT | 0 | 0 | 960 | 180 | 8.33% |
| **6** | TOP_RIGHT | 960 | 0 | 960 | 180 | 8.33% |
| **6** | LEFT_SIDE | 0 | 270 | 270 | 540 | 7.03% |
| **6** | RIGHT_SIDE | 1650 | 270 | 270 | 540 | 7.03% |
| **6** | BOTTOM_LEFT | 0 | 900 | 960 | 180 | 8.33% |
| **6** | BOTTOM_RIGHT | 960 | 900 | 960 | 180 | 8.33% |

**Protected creator regions (never overlay ads):**

| Layout | Creator X,Y | Creator width × height | Creator frame area | Total ad frame area |
| --- | --- | --- | ---: | ---: |
| 2 | 0,0 | 1920 × 810 | 75.00% | **25.00%** |
| 4 | 0,180 | 1920 × 720 | 66.67% | **33.33%** |
| 6 | 270,180 | 1380 × 720 | 47.92% | **47.40%** |

In the six-slot layout, side slots are centered vertically at Y=270–810; the remaining left/right strips at Y=180–270 and Y=810–900 are **not additional advertising slots**. 4.6875% of the full canvas is intentionally outside both the guaranteed creator-safe center and the ad slots.

## Arithmetic correction to the reference infographic

The image labels the two-, four-, and six-slot layouts as approximately **33.3%, 44.4%, and 66.7% advertising coverage**. Those figures do **not** follow from the displayed dimensions on a 1920 × 1080 canvas.

- 2 × (960 × 270) / (1920 × 1080) = **25.00%**.
- 4 × (960 × 180) / (1920 × 1080) = **33.33%**.
- [4 × (960 × 180) + 2 × (270 × 540)] / (1920 × 1080) = **47.3958%** (≈ **47.40%**).
- The image's 1380 × 540 creator-center note for six slots is not the full protected center: after top/bottom 180-pixel rows, that center is **1380 × 720**. The side ads themselves are only 540 pixels tall.

Do **not** carry the infographic percentages into offers, contracts, automated quotations, dashboards or template metadata. Instead calculate pixel coverage from the actual versioned coordinates and state the denominator (full 1920 × 1080 output frame).

## Acceptance rules for future advertiser-template generation

1. Select a versioned layout ID and a slot ID. No guesswork or inferred dimensions from screenshots.
2. Create artwork at exactly the slot's specified **width × height in pixels** (e.g., for a six-slot side ad: **270 × 540**). Wrong-size art fails validation; never silently stretch, crop or upscale it.
3. Render/composite to exactly **1920 × 1080**. Ads must occupy their canonical rectangles without overlap or encroachment into the protected creator region; keep the creator's imagery visible and obtain their approval independently.
4. Validate each slot and final canvas in automated tests. For real output QA, inspect rendered media with `ffprobe` for exact output dimensions and verify pixel placement (and framing across a representative clip). A test that only checks planned coordinates is not proof that an encoder/compositor used them.
5. The existing `ARE-GEO-V1` owner catalog in `assets/alpha-prototypes/OWNER-GEOMETRY.tsv` is a **different layout generation** (e.g., 320 × 1080 vertical and 640 × 180 horizontal slots). It is intentionally **not overwritten** by this standard, and existing `ARE-P01/P02/S01/E01` products are not automatically converted or activated.
6. The existing `DemonstrationVideoStudio.CompositeAsync` outputs a **1280 × 720** demonstration with a differently positioned marketing card. It is **not compliant** with this new layout until explicitly refactored and tested; the new standard deliberately reports `RendererIntegrated: false`.
7. No prices, payment capture, creator placement, ad impression promise, live delivery or source-media rights are authorized by a geometry contract.

## Source of truth

- `Bliss.Domain/AdvertisingRealEstate/PodcastAdvertisingGeometry.cs` provides versioned 2/4/6 layouts, frame and artwork-size validation and area basis points.
- `Bliss.Tests/AdvertisingRealEstate/PodcastAdvertisingGeometryTests.cs` asserts the exact rectangles, no overlaps, protected content and fail-closed rejection of malformed asset dimensions.
- **Future integration target:** make the advertiser-template producer and FFmpeg compositor call the same contract, then prove **actual** output geometry with a rendered fixture. Until then, answer any “does it always match?” question with **NO — standard defined, rendering integration not verified**.
