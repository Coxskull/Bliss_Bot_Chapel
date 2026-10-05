# Creative Academy evidence

**Date:** 2026-10-05
**Classification:** local architecture proof. Hosted acceptance is not claimed.
**Delivery:** `NOT_SENT`. Model calls: 0. Campaign ready: refused.

## Updated purpose

The Academy is intended to produce original advertisements at or above an approved reference's perceived commercial quality class. This run implements the production specification, anchor selection, customization rules, casting direction, and dual-gate QA. It does not claim a generated advertisement because no production model is configured.

Doctrine: reproduce the craftsmanship; replace the creative content. Originality without quality parity fails. Quality parity without originality fails.

## What was proved

The Creative Academy stores the supplied patisserie curriculum and judges the stored copy. Maison Fleur Pâtisserie is the quality teacher. It is not cloned. L'Amour Sucré stays REVISE because the headline repeats "escape". Solara stays REVISE because the stored labels include "curdj" and "mjer". Belmonté stays WITHHELD until an operator records the visual benchmark, then PASS. Campaign ready is refused. A production model was not called.

The client-intake full regression passed: 477 tests, 0 failed, 0 skipped. The preceding quality-anchor amendment had 472 passing tests. Five additional facts cover fitness and automotive routing, minimum intake, unsupported niches, recipe output, and model refusal.

The updated amendment adds:

- VidaCare Pharmacy as `ALPHA MASTER PROTOTYPE 01/50`, niche pharmacy/drugstore, purpose quality anchor, creative template no.
- FreshMart Supermarket as an owner-supplied example, not silently promoted to master.
- A pharmacy production brief that keeps the advertiser's burgundy/gold palette, Montserrat font, Spanish copy, prescription-pickup focus, Panama market, and no-families restriction while inheriting the Alpha quality signature.
- The 16-item reference-parity gate.
- A specific-market casting standard for the Philippines, Malaysia, Indonesia, Colombia, Panama, and Dominican Republic.
- Independent customization, quality, originality, and geographic-authenticity gates.
- `REVISE` when quality drift occurs and `ELIGIBLE_TO_CONTINUE` only when all four gates pass. Eligibility still does not set campaign ready or send.

## Updated instruction phase evidence

| Phase | Result | Report | Recording |
| --- | --- | --- | --- |
| A1. Quality extraction | The doctrine and 16 parity criteria are displayed. | `docs/architecture/evidence/academy/phase-a1-quality-extraction.pdf` | `academy_phase_a1_quality_extraction.mp4` |
| A2. Master anchor and customization | VidaCare is Master 01/50. FreshMart is supplied context. The Panama pharmacy brief changes identity without changing the quality floor. | `docs/architecture/evidence/academy/phase-a2-anchor-and-customization.pdf` | `academy_phase_a2_master_examples_and_customization.mp4` |
| A3. Geographic authenticity | A specific market, research-before-casting, advertiser assets, diversity, and anti-stereotype rules are displayed. | `docs/architecture/evidence/academy/phase-a3-geographic-authenticity.pdf` | `academy_phase_a3_geographic_authenticity.mp4` |
| A4. Dual gate | Quality drift returns REVISE. All four gates passed returns ELIGIBLE_TO_CONTINUE with campaign ready false and NOT_SENT. | `docs/architecture/evidence/academy/phase-a4-dual-gate.pdf` | `academy_phase_a4_dual_gate.mp4` |

## Client brand-and-niche workflow

The client can now enter a brand or store name and select a niche. The Academy routes fitness to `nova-fit-reference`, automotive service to `taller-ruta-reference`, pharmacy to `vidacare-master-01`, and patisserie to `maison-fleur`. It prepares a new wide 16:9 production recipe using the client identity and optional Brand DNA.

The newly supplied Nova Fit and Taller Ruta images are stored as niche quality references, not creative templates. Their fictional names, people, offers, environments, copy, and compositions must be replaced.

FreshMart is still a supplied example, not an approved grocery anchor. Grocery and dental return `NICHE_ANCHOR_REQUIRED` rather than borrowing an unrelated reference.

| Phase | Result | Report | Recording |
| --- | --- | --- | --- |
| B1. Niche router | Fitness and automotive service select their own supplied quality references. Unsupported niches stop. | `docs/architecture/evidence/academy/phase-b1-niche-router.pdf` | `academy_phase_b1_niche_router.mp4` |
| B2. Client intake | Brand/store name plus niche prepares a recipe. Optional fields preserve colors, font, market, language, offer, people, and restrictions. | `docs/architecture/evidence/academy/phase-b2-client-intake.pdf` | `academy_phase_b2_client_intake.mp4` |
| B3. Generation boundary | Recipe readiness is not image generation. The Generate control visibly fails closed without a configured production model. | `docs/architecture/evidence/academy/phase-b3-generation-boundary.pdf` | `academy_phase_b3_generation_boundary.mp4` |
| B4. QA and regression | The four instructor gates remain mandatory. Academy and full regressions pass. Nothing sends. | `docs/architecture/evidence/academy/phase-b4-qa-and-regression.pdf` | `academy_phase_b4_qa_and_regression.mp4` |

## Phases

| Phase | Result | Report | Recording |
| --- | --- | --- | --- |
| 1. Charter and prototype | Maison Fleur is REFERENCE. The image is the supplied teacher. | `docs/architecture/evidence/academy/phase-1-charter.pdf` | `academy_phase1_charter_and_prototype-2.mp4` |
| 2. Creative DNA | Atelier Cendre selects `maison-fleur`. North Clinic has no teacher. A Maison Fleur clone is refused. | `docs/architecture/evidence/academy/phase-2-creative-dna.pdf` | `academy_phase2_creative_dna.mp4` |
| 3. Instructor | L'Amour Sucré REVISE for ESCAPE. Solara REVISE for CURDJ and MJER. Belmonté WITHHELD. | `docs/architecture/evidence/academy/phase-3-instructor.pdf` | `academy_phase3_instructor_defects.mp4` |
| 4. Repair and approval gate | Preserve and repair lists are stored. Belmonté visual benchmark records PASS. Campaign ready returns 400. Regression 464/464. | `docs/architecture/evidence/academy/phase-4-repair-and-gate.pdf` | `academy_phase4_pass_and_refusal.mp4` |

## Scores from the stored copy

Initial weights total 100. Typography is 16. Lighting and depth is 12.

- L'Amour Sucré, visual benchmark met, duplicated ESCAPE: REVISE, score 84.
- L'Amour Sucré, visual benchmark not met, note "Weaker visual contrast.": REVISE, score 72. The duplicated word remains.
- Solara, visual benchmark met, malformed CURDJ and MJER: REVISE. Typography is subtracted once.
- Belmonté, visual benchmark unrecorded: WITHHELD, score unrecorded.
- Belmonté, visual benchmark recorded as met: PASS, score 100. Campaign ready stays false.
- Copied prototype wording: FAIL.
- Obstructed creator face: FAIL.

A vision model did not produce these scores. The text defects are read from the stored transcription of the supplied images. The visual benchmark is an operator record.

## Boundaries that stayed closed

- Hosted acceptance was not claimed. The operations console in this proof runs in Development against a local database.
- Wedding Planner was not opened by this feature.
- Economics was not asked for a price.
- No email, WhatsApp, SMS, social message, crawler, paid enrichment, settlement, payout, or advertiser charge was sent.
- No new advertisement was generated from any supplied image.
- `PRODUCTION_SPEC_READY` means the deterministic brief is ready; it does not mean an image exists.

## Where the proof lives

- Contract: `docs/architecture/contracts/CREATIVE-ACADEMY-CONTRACT.md`
- Operations page: `/operations#/academy`
- Curriculum images: `frontend/operations/academy/`
- Migration: `20261005121837_CreativeAcademy`
- Test summary: `docs/architecture/evidence/academy/dotnet-test.txt`
