# Catalog Geometry Approval Report

Date: 2026-10-07  
Geometry version: `ARE-GEO-V1`  
Scope: owner-authorized recommended geometry and current acceptance state

## Decision

The owner authorized a recommended 1920×1080 catalog layout. These values are recommendations, not measurements inferred from `ARE-GUIDE-001-V1` or any showcase image. Product slot combinations are alternatives and are not intended to be occupied simultaneously.

| Slot | Width | Height | Origin | Area |
| --- | ---: | ---: | ---: | ---: |
| `LEFT_VERTICAL` | 320 | 1080 | 0, 0 | 345600 |
| `RIGHT_VERTICAL` | 320 | 1080 | 1600, 0 | 345600 |
| `TOP_LEFT` | 640 | 180 | 320, 0 | 115200 |
| `TOP_RIGHT` | 640 | 180 | 960, 0 | 115200 |
| `BOTTOM_LEFT` | 640 | 180 | 320, 900 | 115200 |
| `BOTTOM_RIGHT` | 640 | 180 | 960, 900 | 115200 |
| `BOTTOM_FULL` | 1280 | 180 | 320, 900 | 230400 |

Every area equals width × height and every rectangle is within the 1920×1080 canvas. The source of record is `assets/alpha-prototypes/OWNER-GEOMETRY.tsv`.

## Verified result

- Catalog geometry: `RECORDED`
- Academy references: 50 uploaded and 50 `ACTIVE`
- Sample reference retrieval: `RETRIEVED`
- ARE-P01 adaptation: `RECOMPOSED` into `LEFT_VERTICAL` and `BOTTOM_FULL`
- Creator authorization: recorded
- Device and platform status: `OWNER_APPROVED`
- Full regression suite: 520 passed, 0 failed, 0 skipped
- Model calls during geometry/joint reading: 0
- Campaign ready: false
- Delivery: `NOT_SENT`

## Remaining blockers

This is a geometry approval report, not a completed Harborlight voyage or consolidated autonomous-acceptance report.

- Economics has no authorized prices for ARE-P01, ARE-P02, ARE-S01, or ARE-E01.
- Fifteen seconds is stored, but duration and occurrence changes are unpriced.
- OpenAI credits/provider configuration are deferred; the Harborlight generation was not run.
- Visual QA, human review, recorded generation cost, and regression baselines remain absent.
- Global Quality DNA grades remain `UNCLASSIFIED`.
- No display happened, so proof of delivery remains absent.
- Hosted acceptance remains `UNCLAIMED`.
- The catalog and Academy amendments remain `OPEN`.
