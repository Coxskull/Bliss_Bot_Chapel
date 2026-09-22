# Wedding Planner Phase 6 — Evidence

## Result

Phase 6 Mature Creative / Asset & Revision Department is implemented
against `PHASE-6-ENGINEERING-CONTRACT.md`.

New logical AI roles: **13**. New executable worker profiles per
successful job: **6**. Exactly **13** immutable role contributions and
exactly **6** `WeddingPlannerAgentRun` receipts per successful package.
Phase 5 concept roles remain pinned provenance only (0 Phase 5 re-runs).

The implementation does not create thirteen fake model calls. After the
six AI stages succeed, the server merges `creative-package.v1` and calls
provider-neutral `IWeddingPlannerCreativeAssetProvider` once per variant.
The asset provider is **not** an AI worker/run.

## Workforce proof

| Executable profile | Durable logical contributions |
| --- | --- |
| `CREATIVE_DIRECTION_V1` | `CREATIVE_DIRECTOR`, `CAMPAIGN_STRATEGIST` |
| `STRATEGY_ADAPTATION_V1` | `AUDIENCE_STRATEGIST`, `OFFER_STRATEGIST`, `CHANNEL_STRATEGIST` |
| `VISUAL_SYSTEM_V1` | `VISUAL_DESIGNER`, `LAYOUT_DESIGNER`, `TYPOGRAPHY_DESIGNER` |
| `IMAGE_DIRECTION_V1` | `IMAGE_PROMPT_DESIGNER` |
| `COPY_SYSTEM_V1` | `HEADLINE_SPECIALIST`, `BODY_COPY_SPECIALIST`, `CTA_SPECIALIST` |
| `VARIANT_PRODUCTION_V1` | `VARIANT_PRODUCER` |

Each profile has its own prompt pack, assigned-role JSON, provider
receipt, tokens, cost, and terminal status. Profiles may share a
provider/model without erasing their separately versioned authority
contracts.

## Creative asset provider (non-AI)

`IWeddingPlannerCreativeAssetProvider` is provider-neutral and separate
from AI runs:

- Local Development/CI: deterministic valid PNG bytes for the exact
  canvas;
- Production path: `RemoteHttp` to the configured endpoint only; raw
  `image/png` body plus bounded safe receipt headers; rejects returned
  URLs, base64, SVG, HTML, and non-PNG content types.

Normative V1 limits and validation:

- PNG only, ≤ 2 MiB per asset, max 4 assets per package, exact
  server-derived canvas dimensions;
- fail-closed structural checks: PNG signature; IHDR/IDAT/IEND only;
  CRC; zlib inflation to exact scanline bytes; filter bytes `0..4`;
- transactional PostgreSQL `bytea` storage (accepted V1 risk);
- authenticated tenant-safe same-origin
  `GET /api/wedding-planner/creative-assets/{id}/content` streams raw
  PNG with `private, no-store`, `nosniff`, and `ETag` — no URL/base64
  secondary fetch.

## Prerequisites and evidence boundary

A creative-production job requires a current-approved Phase 5 concept
package whose latest `APPROVE` decision carries a valid
`SelectedConceptId`. Concept package id, selected concept, and pinned
Brand DNA / Color Profile / Research Report provenance are required on
the job and package. Missing prerequisites return 400.

Job kinds are exactly `INITIAL` | `REVISION`. Both are one-parent
immutable jobs; `REVISION` requires a single parent package (same pins /
selected concept) plus notes and **reruns all six** AI profiles (no
partial packages). Human `APPROVE` selects `selectedVariantId`
decision-only and never mutates package `DocumentJson`. Approval is
**draft creative approval only**.

Brand DNA and Color Profile constrain creative direction only. New
factual claims are forbidden; any preserved claims must keep exact
selected-concept statements and pinned research source ids.

## Durable saga

Migration:
`20260922135431_WeddingPlannerPhase6CreativeDepartment`

The complete migration chain was applied successfully to empty PostgreSQL
database `bliss_phase6_verify` through Phase 6 (latest migration as
above).

New durable records:

- `WeddingPlannerCreativeProductionJobs`
- `WeddingPlannerCreativePackageVersions`
- `WeddingPlannerCreativeRoleContributions`
- `WeddingPlannerCreativeAssets`
- `WeddingPlannerCreativePackageDecisions`
- nullable current-approved creative package pointer on the workspace
- creative profile/assigned-role/output fields on agent runs

Jobs retain append-oriented stage receipts while saga status/linkage
advances to a terminal state. Package `DocumentJson`, contributions,
assets, and decisions are immutable after insert. Variant selection lives
only on the decision row.

Failure after any AI/validation/asset step creates no package, no
contributions, and no durable asset rows. Succeeded/failed replay returns
the existing job without retry.

## API and authority

Phase 6 adds creative-job, package, contribution, asset, asset-content,
package-run, workspace-run, and human decision routes under
`/api/wedding-planner`.

Controls preserve Phases 1–5:

- authenticated advertiser is bound to its `advertiser_id`;
- cross-tenant resources return 404; anonymous protected requests return
  401;
- viewers cannot create jobs or decisions;
- human APPROVE/REJECT requires rationale; APPROVE requires a valid
  variant selection; REJECT forbids selection;
- later approval supersedes the prior current package without mutating
  either package document;
- no PUT/PATCH/DELETE of durable creative documents;
- job and decision idempotency uses `(SourceSystem, IdempotencyKey)`.

## Automated verification

Command:

`dotnet test BlissBotChapel.sln --no-restore`

Result:

- passed: 262
- failed: 0
- skipped: 0
- duration: ~4 seconds

Both frontend scripts (`frontend/public/wedding-planner.js`,
`frontend/operations/app.js`) pass `node --check`.

## PostgreSQL workflow smoke test

Against `bliss_phase6_verify`, seeded with approved DNA / color /
research / concept selection `concept_2`:

1. `INITIAL` job succeeded: 6 runs / 13 contributions / 2 assets;
   human approved `variant_1`;
2. `REVISION` job succeeded: 6 runs / 13 contributions / 2 assets;
   human approved `variant_2`;
3. initial package became `SUPERSEDED`; revision became `APPROVED` /
   `CURRENT`;
4. streamed asset content returned PNG signature and **20,123** bytes.

Recorded summary:
`/opt/cursor/artifacts/phase6-postgres-workflow.json`.

A later browser walkthrough created package **v3** `REVISION`
(6 / 13 / 2), showed `PROPOSED` while v2 remained `CURRENT`, rendered
safe same-origin PNGs, approved `variant_2`, then made v3
`APPROVED`/`CURRENT` with v2 and v1 `SUPERSEDED`.

## Browser verification

Chrome exercised the PostgreSQL-backed public and operations interfaces.

Public:

- under auth-disabled / no bound advertiser, submit was correctly
  disabled;
- gate clearly states current-approved concept requirement, exact
  13→6 workforce mapping, and draft-only approval;
- no `<img>` elements in the gate.

Operations:

- showed selected concept / DNA / color / research / parent pins, input
  SHA, asset provider/cost, exact 13 contributions, 6 run receipts, and
  same-origin PNGs with naturalWidth **1080**;
- no horizontal overflow observed (`scrollWidth == clientWidth`).

## Artifacts

- `/opt/cursor/artifacts/phase6_public_creative_gate.png`
- `/opt/cursor/artifacts/phase6_revision_proposed.png`
- `/opt/cursor/artifacts/phase6_revision_proposed_variants.png`
- `/opt/cursor/artifacts/phase6_revision_approved_current.png`
- `/opt/cursor/artifacts/phase6_approved_current_variant.png`
- `/opt/cursor/artifacts/phase6_revision_png_approval_workflow.mp4`
- `/opt/cursor/artifacts/phase6-postgres-workflow.json`

`videoReview` verdict: **PASS** — temporally clear and suitable evidence.
Recorded flow: revision submit → `PROPOSED` with prior package still
`CURRENT` → PNG inspect → human `APPROVE` → new `CURRENT` / old
`SUPERSEDED` → final PNG.

## State-truthfulness caveat

Jobs/status advance; package `DocumentJson`, contributions, assets, and
decisions remain immutable. PostgreSQL `bytea` storage is an accepted V1
risk. Local PNGs are visibly synthetic abstract stripes, not final
artwork.

## Preserved boundaries

Phase 6 does not add Phase 7 Chaperone/QA/escalation, Phase 8
campaign-ready/handshake, Bliss matching/review/placement writes,
legal/compliance/accessibility certification, measurement learning,
Alpha Auto, or n8n/GHL authority.

Phase 6 does not auto-advance to Phase 7.
