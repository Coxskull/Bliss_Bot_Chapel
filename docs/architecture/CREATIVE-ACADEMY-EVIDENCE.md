# Creative Academy evidence

**Date:** 2026-10-05
**Classification:** local architecture proof. Hosted acceptance is not claimed.
**Delivery:** `NOT_SENT`. Model calls: 0. Campaign ready: refused.

## What was proved

The Creative Academy stores the supplied patisserie curriculum and judges the stored copy. Maison Fleur Pâtisserie is the quality teacher. It is not cloned. L'Amour Sucré stays REVISE because the headline repeats "escape". Solara stays REVISE because the stored labels include "curdj" and "mjer". Belmonté stays WITHHELD until an operator records the visual benchmark, then PASS. Campaign ready is refused. A production model was not called.

The full regression passed: 464 tests, 0 failed, 0 skipped. The previous dock count was 452. The 12 new tests are the academy facts.

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
- No fifth advertisement was generated.

## Where the proof lives

- Contract: `docs/architecture/contracts/CREATIVE-ACADEMY-CONTRACT.md`
- Operations page: `/operations#/academy`
- Curriculum images: `frontend/operations/academy/`
- Migration: `20261005121837_CreativeAcademy`
- Test summary: `docs/architecture/evidence/academy/dotnet-test.txt`
