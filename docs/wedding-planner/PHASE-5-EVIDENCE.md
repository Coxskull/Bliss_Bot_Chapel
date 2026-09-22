# Wedding Planner Phase 5 — Evidence

## Result

Phase 5 Concept / Prototype Workshop is implemented against
`PHASE-5-ENGINEERING-CONTRACT.md`.

New logical AI roles: **4**. New executable worker profiles per successful
job: **3**. Exactly **4** immutable role contributions and exactly **3**
concepts (`concept_1`, `concept_2`, `concept_3`) per successful package.

The implementation does not create four fake model calls. A successful job
creates exactly three `WeddingPlannerAgentRun` receipts and four durable
role contributions.

## Workforce proof

| Executable profile | Durable logical contributions |
| --- | --- |
| `CONCEPT_STRATEGY_V1` | `BRAND_STRATEGIST` |
| `CONCEPT_CREATIVE_V1` | `ART_DIRECTOR`, `COPYWRITER` |
| `PROTOTYPE_PRODUCTION_V1` | `PRODUCTION_ARTIST` |

Each profile has its own prompt pack, assigned-role JSON, provider receipt,
tokens, cost, and terminal status. Profiles may share a provider/model
without erasing their separately versioned authority contracts.

## Prerequisites and evidence boundary

A workshop job requires current-approved Brand DNA, Color Profile, and
Research Report. All three ids (and version numbers) are pinned onto the job
and package as required provenance. Missing any prerequisite returns 400.

Brand DNA and Color Profile constrain creative direction (voice, palette
roles, geometry language) only. They are never factual evidence and must
never appear as `sourceIds` on factual claims.

Only source ids from the **pinned approved** research report catalog may
support factual claims. The app does not fetch research citation URLs and
does not invent live facts when the local AI provider is active.

## Safe structured prototypes

Prototypes are structured `prototype-spec.v1` only:

- channel canvases are server-derived from `ChannelFormat`
  (`STATIC_SOCIAL_SQUARE` 1080×1080, `STATIC_SOCIAL_STORY` 1080×1920,
  `STATIC_DISPLAY_BANNER` 1200×628, `EMAIL_HERO` 1200×600);
- templates are exactly `LOFI_STACK_V1`, `LOFI_SPLIT_V1`, or
  `LOFI_BANNER_V1`;
- regions are bounded (2–12, unique ids, known types, in-canvas bounds);
- text refs are only `copy.headline` / `copy.body` / `copy.cta`;
- palette refs must exist on the pinned color profile;
- asset fields are placeholders only (`HERO_IMAGE` / `LOGO` / `PRODUCT` /
  `DECORATIVE` labels — no URLs, paths, or bytes).

Forbidden everywhere: image generation, image providers, binary asset
storage, media URL fetch, and arbitrary HTML/CSS/SVG/script. Safe UI uses a
fixed template renderer with HTML-escaped plain text and labeled placeholder
boxes only — never `<img>` or executed markup.

## Durable saga

Migration:
`20260922111259_WeddingPlannerPhase5ConceptWorkshop`

The complete migration chain was applied successfully to an empty PostgreSQL
database through Phase 5.

New durable records:

- `WeddingPlannerWorkshopJobs`
- `WeddingPlannerConceptPackageVersions`
- `WeddingPlannerConceptRoleContributions`
- `WeddingPlannerConceptPackageDecisions`
- nullable current-approved concept package pointer on the workspace
- workshop profile/assigned-role/package-output fields on agent runs

Jobs retain append-oriented stage receipts while their saga status/linkage
advances to a terminal state. Package documents, contributions, and decisions
are immutable after insert. Selection of a winning concept lives only on the decision row as
`selectedConceptId` and never mutates package `DocumentJson`, contribution
rows, or prototype specs.

Human `APPROVE` requires a valid `selectedConceptId`
(`concept_1` | `concept_2` | `concept_3`). Human `REJECT` forbids
selection. Approval is concept-direction only — not research, claim, legal,
matching, accessibility, compliance, campaign-ready, asset, QA, or
production-artwork approval.

Failure behavior is append-oriented:

- AI/validation failure → current run and job `FAILED`, prior receipts kept,
  no package and no contributions;
- successful replay → no AI call;
- failed replay → same failed job, no retry.

## API and authority

Phase 5 adds workshop-job, concept-package, contribution, package-run,
workspace-run, and human decision routes under `/api/wedding-planner`.

Controls preserve Phases 1–4:

- authenticated advertiser is bound to its `advertiser_id`;
- cross-tenant jobs/packages/contributions/runs/decisions return 404;
- anonymous protected requests return 401;
- viewers cannot create jobs or decisions;
- human APPROVE/REJECT requires rationale; APPROVE requires selection;
- later approval supersedes the prior current package without mutating either
  package document;
- no PUT/PATCH/DELETE, image-generation, or media-URL-fetch route exists;
- job and decision idempotency uses `(SourceSystem, IdempotencyKey)`.

## Automated verification

Command:

`dotnet test BlissBotChapel.sln --no-restore`

Result:

- passed: 232
- failed: 0
- skipped: 0

Coverage includes validation, API, persistence, isolation, provider, and
frontend:

- brief/prerequisite/channel/canvas and forbidden-field validation;
- stage schemas, exact 4→3 role/profile mapping, prototype-spec and package
  merge;
- local synthetic marker and exact concept-direction disclaimer;
- happy path exactly 3 agent runs and 4 mapped contributions;
- mid-stage AI and validation failure sagas;
- succeeded/failed idempotent replay;
- decisions, `selectedConceptId` semantics, supersession, immutability,
  tenancy, anonymous/viewer controls;
- production Local-AI guard;
- architecture guards against image generation/providers, media URL fetch,
  Bliss matching, Alpha Auto, and n8n authority;
- public/operations server-document-only fixed renderer (escaped text,
  placeholders, no `<img>`).

Both JavaScript bundles pass `node --check`.

## PostgreSQL workflow smoke test

A real PostgreSQL workflow completed:

1. open workspace and session with current-approved Brand DNA, Color Profile,
   and Research Report;
2. submit a Concept Workshop job;
3. execute the three workshop profiles;
4. persist four role contributions and one `PROPOSED` `concept-package.v1`
   with concepts `concept_1` / `concept_2` / `concept_3`;
5. approve a selected concept with human rationale.

Observed:

- job `SUCCEEDED`;
- package schema `concept-package.v1`;
- concepts `concept_1`, `concept_2`, `concept_3`;
- three runs, all `SUCCEEDED`, one for each expected profile;
- four contributions (`BRAND_STRATEGIST`, `ART_DIRECTOR`, `COPYWRITER`,
  `PRODUCTION_ARTIST`);
- conspicuous `SYNTHETIC DEVELOPMENT PROTOTYPE` marker;
- exact concept-direction disclaimer;
- selected concept human-approved and current.

A later browser workflow created package v3, approved `concept_2`, made v3
current, and superseded prior packages.

## Browser verification

Chrome exercised the PostgreSQL-backed public and operations interfaces at a
~1,440 × 1,000 desktop viewport.

Public:

- auth-disabled, no-advertiser-bound gate rendered with submit disabled and
  all three current-approved prerequisites stated.

Operations:

- loaded job SHA, provenance, cost, three concepts, asset placeholders, four
  contributions, and three run receipts;
- submitted a new workshop job;
- inspected the `PROPOSED` package;
- approved `concept_2` with rationale;
- confirmed current/supersession markers.

An initial long SHA caused horizontal overflow. Layout was corrected with
`min-width` / `overflow-wrap` and reverified with
`scrollWidth == clientWidth`.

## Artifacts

- `/opt/cursor/artifacts/phase5_public_workshop_gate.png`
- `/opt/cursor/artifacts/phase5_current_package_final.png`
- `/opt/cursor/artifacts/phase5_structured_prototype_final.png`
- `/opt/cursor/artifacts/phase5_concept_workshop_verified_end_to_end.mp4`

The end-to-end video was independently reviewed and is suitable; no failures
were observed in the recorded workflow.

## Preserved boundaries

Phase 5 does not add asset/revision production, Chaperone/QA, campaign-ready
state, Bliss handshake, measurement learning, GHL coupling, Alpha Auto, n8n
authority, image generation, media URL fetch, or any change to deterministic
Bliss evaluation, formation, review, or placement.

Phase 5 does not auto-advance to Phase 6.
