# Harborlight free-generation attempt evidence

**Attempt:** `HV-001`  
**Date:** 2026-10-07  
**Status:** `GENERATED_PENDING_HUMAN_REVIEW`  
**Campaign ready:** false  
**Delivery:** `NOT_SENT`

The owner authorized proceeding with free image generation. One original image was generated through the available Cursor `GenerateImage` capability. No new subscription was purchased, no Academy reference image was sent, and no credential was added to the repository.

This is a real generated attempt, but it is not a completed provider-integrated Harborlight voyage. The capability did not expose its underlying model, provider job ID, or price. Those fields remain `UNREPORTED_BY_PROVIDER`; they are not relabeled as OpenAI and the cost is not called zero.

## Brief

- Advertiser: Harborlight Pharmacy
- Market: Panama City, Panama
- Niche: pharmacy
- Objective: introduce convenient prescription pickup
- Inventory product: ARE-P01
- Headline: “Prescription pickup, ready when you are.”
- New palette: deep ocean navy, luminous coral, warm ivory, and sea-glass teal

## Automatic retrieval

The same deterministic retrieval used by the joint reading selected:

| Reference | Reason | LEARN | DO NOT COPY |
| --- | --- | --- | --- |
| ACA-001-V1 | niche match | premium product lighting on a pharmacy counter | VidaCare; Care for a Brighter You |
| ACA-002-V1 | product realism | shelf product realism and aisle lighting | FreshMart |
| ACA-006-V1 | lighting and depth | lighting and depth in a dining room | pictured restaurant identity |
| ACA-008-V1 | typography | typography for a service shop | Taller Ruta |

References were not manually selected to force a pass. Their image files were not sent to the generator. The prompt used the stored quality concepts and explicit exclusions, not the reference pictures.

## Output

- Finished original: `assets/alpha-prototypes/creative-academy/harborlight/HV-001-original.jpg`
- Dimensions: 1280×720
- SHA-256: `3fa9e45467683c37b3d1fa356aa968bff5b2f0719fa2184ac31ab7f87c1ebd47`
- Attempts: 1
- Provider: Cursor `GenerateImage` capability
- Model: `UNREPORTED_BY_PROVIDER`
- Job ID: `UNREPORTED_BY_PROVIDER`
- Actual cost: `UNREPORTED_BY_PROVIDER`
- New subscription: no

## Inventory preview

`scripts/recompose-harborlight.py` deterministically creates a 1920×1080 test preview:

- `LEFT_VERTICAL`: 320×1080, the original brand panel contained without stretching
- `BOTTOM_FULL`: 1280×180, a purpose-built banner
- protected center: labeled and not occupied by the advertisement
- Academy prototype scaled: no
- delivered: no

The preview is `assets/alpha-prototypes/creative-academy/harborlight/HV-001-adapted-preview.png`.

The first preview cropped the 1280×720 original through the photograph, so the bottom headline was cut off. That crop is replaced. The current bottom banner uses the original lighthouse mark, the full headline “Prescription pickup, ready when you are.”, and “Panama City” on the brand navy field. It is not a photograph crop. Human review of the layout is still open.

## Current checks

| Check | Result |
| --- | --- |
| File validation and hashes | PASS |
| Text/identity originality | PASS |
| Pixel similarity | MEASURED, judgment NOT_JUDGED |
| Visual QA | NOT_RUN |
| Human review | NOT_REQUESTED |
| Regression | BASELINE_NOT_RECORDED |
| Campaign ready | false |
| Delivery | NOT_SENT |

The generated image visibly provides a new Harborlight identity, pharmacy setting, person, product treatment, palette, lighthouse mark, wording, and composition. This observation is not human acceptance. Human review must still evaluate quality, Panama-market authenticity, originality, and the adapted layout.

## Amendments

- Creative Academy: OPEN
- Advertising Real Estate: OPEN
- Hosted acceptance: UNCLAIMED

## HV-4 review sheet

The sheet is `PREPARED` for `HV-001` and bound to the stored original and adapted-preview hashes. It lists every GQD-1 attribute as `UNCLASSIFIED`, every copy check as `NOT_REVIEWED`, and Panama-market authenticity plus the adapted layout as `UNRECORDED`.

Human review remains `NOT_REQUESTED`. The sheet does not satisfy HV-4 and does not establish a regression baseline.

## Pixel similarity

`scripts/measure-harborlight-similarity.py` compared the 1280×720 original with the four retrieved reference files. Each distance is a 32×18 grayscale mean absolute error and an 8×8 average-hash Hamming distance.

| Reference | Mean absolute error | Average-hash distance |
| --- | --- | --- |
| ACA-001-V1 | 67.69 | 31 of 64 |
| ACA-002-V1 | 52.91 | 18 of 64 |
| ACA-006-V1 | 61.68 | 28 of 64 |
| ACA-008-V1 | 57.78 | 26 of 64 |

The measurement status is `MEASURED`. The judgment is `NOT_JUDGED`. The visual grade is `NOT_ASSIGNED`. Visual QA remains `NOT_RUN`. These numbers are not a copy decision and not a quality grade.

## Verification

```text
dotnet test BlissBotChapel.sln --no-restore
Passed: 525
Failed: 0
Skipped: 0
```

The visual PDF evidence package is `docs/architecture/evidence/harborlight-free-attempt-report.pdf`.
