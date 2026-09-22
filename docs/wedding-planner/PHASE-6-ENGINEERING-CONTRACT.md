# Wedding Planner Phase 6 — Engineering Contract

Status: authorized for implementation by the user's request to proceed to
Phase 6.

## Objective

Add the **Mature Creative / Asset & Revision Department** as Wedding
Planner creative-production software:

1. an authenticated human submits a creative-production brief for an
   authorized workspace that already has a current-approved Phase 5
   concept package whose latest `APPROVE` decision carries a
   `SelectedConceptId`;
2. **exactly six** executable AI profiles synthesize **exactly thirteen**
   durable production-specialist role contributions for **only** that
   selected concept;
3. the server deterministically merges a canonical immutable
   `creative-package.v1`, then calls
   `IWeddingPlannerCreativeAssetProvider` once per variant to obtain
   validated PNG bytes; and
4. only a human `APPROVE` or `REJECT` decision may change
   current-approved creative-package pointer metadata. Selection of a
   winning variant lives on the decision row and never mutates the
   package.

PostgreSQL remains permanent memory. Phase 6 adds **13 NEW** logical
production specialist roles and **exactly 6** executable AI worker
profiles (not thirteen fake agent runs). Phase 5's four concept-workshop
roles remain **pinned concept provenance** and are **not** re-run or
re-authored. AI generation never equals draft-creative approval. Brand
DNA and Color Profile remain creative constraints only; they are never
factual evidence. New factual claims are forbidden; any
`factualClaims` may only preserve an exact statement plus the same
source IDs from the pinned selected Phase 5 concept. Wedding Planner
orchestrates the creative asset provider and is **not** itself an image
generator. AI stages emit prompts/specs only — never image bytes.

## Assumptions

1. Phases 1–5 remain accepted and unchanged in behavior except for
   additive creative-production tables/pointers, agent-run columns,
   routes, UI surfaces, tests, audit action names, prompt-pack
   registration, and the Phase 6 same-origin asset-content streaming
   exception documented below.
2. A workspace may start a creative-production job only when
   `CurrentApprovedConceptPackageVersionId` is non-null **and** the
   latest `APPROVE` decision for that package carries a valid
   `SelectedConceptId` (`concept_1` | `concept_2` | `concept_3`) present
   in that package. The approved concept package id, selected concept
   id, and the package's pinned Brand DNA / Color Profile / Research
   Report provenance ids (and version numbers) are pinned onto the job
   and creative package as required provenance.
3. Phase 5's four role contributions
   (`BRAND_STRATEGIST`, `ART_DIRECTOR`, `COPYWRITER`,
   `PRODUCTION_ARTIST`) and the selected concept snapshot remain
   immutable provenance. Phase 6 never re-invokes Phase 5 profiles,
   never rewrites concept contributions, and never re-approves the
   concept package.
4. Brand DNA DocumentJson/Summary and Color Profile DocumentJson may
   constrain creative production (voice, palette roles, geometry
   language, typography cues) only. They must never appear as
   `sourceIds` on factual claims and must never be treated as research
   evidence.
5. Research Report `sources[].id` values from the **pinned approved**
   report remain the only valid factual-claim source identifiers. Phase 6
   may **preserve** exact selected-concept claim statements + source ids;
   it may not invent new claims or new source ids.
6. Creative package approval is **draft creative approval only**. It is
   not research, claim, legal, matching, accessibility, compliance,
   campaign-ready, QA, or final production-artwork approval. It does not
   authorize Bliss matching or placement.
7. Draft PNG assets are provider-generated renditions for review only.
   The UI may render `<img>` **only** against the same-origin
   authenticated `GET /creative-assets/{id}/content` endpoint. This
   Phase 6 exception does **not** alter the Phase 5 placeholder-only
   rule for concept prototypes.
8. Bliss matching, review, placement, Alpha Auto, and n8n authority
   remain out of scope and untouched.
9. Phase 7 Chaperone/QA/escalation and Phase 8 handshake / campaign-ready
   are out of scope.

## Workforce allocation

| Item | Phase 6 allocation |
| --- | --- |
| New logical AI roles | **exactly 13** (listed below) |
| Executable AI profiles / successful calls | **exactly 6** |
| Prompt packs | `wp-phase6.creative-direction.v1`, `wp-phase6.strategy-adaptation.v1`, `wp-phase6.visual-system.v1`, `wp-phase6.image-direction.v1`, `wp-phase6.copy-system.v1`, `wp-phase6.variant-production.v1` |
| AI provider | reuse `IWeddingPlannerAiProvider`; shared provider/model allowed; profiles remain separately versioned |
| Creative asset provider | new `IWeddingPlannerCreativeAssetProvider` — **not** an AI worker/run |
| Deterministic server assembly / merge | **not** an AI worker/run |
| Agent runs on success | exactly 6 `WeddingPlannerAgentRun` rows |
| Durable role evidence | exactly 13 immutable `CreativeRoleContribution` rows |
| Phase 5 roles | remain pinned concept provenance; **0** Phase 5 re-runs |
| Human authority | humans alone approve/reject creative packages and select a variant on APPROVE |

### Logical roles (exactly 13 NEW)

| Order | Logical role | Stage profile |
| --- | --- | --- |
| 1 | `CREATIVE_DIRECTOR` | `CREATIVE_DIRECTION_V1` |
| 2 | `CAMPAIGN_STRATEGIST` | `CREATIVE_DIRECTION_V1` |
| 3 | `AUDIENCE_STRATEGIST` | `STRATEGY_ADAPTATION_V1` |
| 4 | `OFFER_STRATEGIST` | `STRATEGY_ADAPTATION_V1` |
| 5 | `CHANNEL_STRATEGIST` | `STRATEGY_ADAPTATION_V1` |
| 6 | `VISUAL_DESIGNER` | `VISUAL_SYSTEM_V1` |
| 7 | `LAYOUT_DESIGNER` | `VISUAL_SYSTEM_V1` |
| 8 | `TYPOGRAPHY_DESIGNER` | `VISUAL_SYSTEM_V1` |
| 9 | `IMAGE_PROMPT_DESIGNER` | `IMAGE_DIRECTION_V1` |
| 10 | `HEADLINE_SPECIALIST` | `COPY_SYSTEM_V1` |
| 11 | `BODY_COPY_SPECIALIST` | `COPY_SYSTEM_V1` |
| 12 | `CTA_SPECIALIST` | `COPY_SYSTEM_V1` |
| 13 | `VARIANT_PRODUCER` | `VARIANT_PRODUCTION_V1` |

### Executable profiles (exactly 6)

| Profile key | Prompt pack | Assigned roles | Agent-run `LogicalRole` (stage) |
| --- | --- | --- | --- |
| `CREATIVE_DIRECTION_V1` | `wp-phase6.creative-direction.v1` | roles 1–2 | `CREATIVE_DIRECTION` |
| `STRATEGY_ADAPTATION_V1` | `wp-phase6.strategy-adaptation.v1` | roles 3–5 | `STRATEGY_ADAPTATION` |
| `VISUAL_SYSTEM_V1` | `wp-phase6.visual-system.v1` | roles 6–8 | `VISUAL_SYSTEM` |
| `IMAGE_DIRECTION_V1` | `wp-phase6.image-direction.v1` | role 9 | `IMAGE_DIRECTION` |
| `COPY_SYSTEM_V1` | `wp-phase6.copy-system.v1` | roles 10–12 | `COPY_SYSTEM` |
| `VARIANT_PRODUCTION_V1` | `wp-phase6.variant-production.v1` | role 13 | `VARIANT_PRODUCTION` |

Do **not** create thirteen fake agent runs. Durable authority for the
thirteen roles is `CreativeRoleContribution` (+ `AssignedRolesJson` on
each run). Stage `LogicalRole` values exist for Phase 2-compatible run
listing only.

Deterministic server merge after the six stages and per-variant calls to
`IWeddingPlannerCreativeAssetProvider` are **not** AI workers and do
**not** create agent-run rows.

Phase 2 Concierge / Brand DNA Interpreter, Phase 3 Color Intelligence,
Phase 4 Curator, and Phase 5 Concept Workshop remain as allocated. Phase 6
does not extend their tools into campaign-ready marking, matching writes,
QA, or Chaperone authority. Phase 5 profiles are never invoked by Phase 6
jobs.

Blueprint starting allocation (“~13 primary on ~5–7 workers”) is hereby
locked for V1 implementation to **exactly 13 NEW roles on exactly 6 AI
calls/runs**.

## Authority

- Advertisers act only inside the `advertiser_id`-bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- Only an authenticated advertiser or chapel operator/admin may create a
  creative-production job or record an `APPROVE` / `REJECT` decision.
- AI stages may propose creative direction, strategy adaptation, visual
  system, image prompts/specs, copy systems, and variant production
  specs. They cannot approve packages, select the winning variant, write
  matching/review/placement records, invent live facts when the local AI
  provider is active, mutate Brand DNA / color / research / concept
  documents, or emit image bytes.
- The creative asset provider may return raw PNG bytes (plus bounded safe
  receipt headers). It cannot approve packages, write Bliss records, or
  return URLs/base64 for the app to fetch.
- Cross-tenant resource lookup returns **404**, consistent with
  Phases 1–5.
- Anonymous protected reads/writes return **401** when OIDC is enabled.
- Authenticated identities without write authority (e.g. viewer) return
  **403** on job create and decision writes.
- Cross-tenant asset content lookup returns **404** (never 403 that
  confirms existence). Anonymous asset content returns **401**.

## Inputs (human brief)

`POST .../creative-production-jobs` body:

| Field | Required | Rules |
| --- | --- | --- |
| `JobKind` | yes | exactly `INITIAL` \| `REVISION` |
| `Objective` | yes | non-empty trimmed string; max length bounded (recommend 4000) |
| `Formats` | yes | array of **1–4 unique** values drawn only from Phase 5's four: `STATIC_SOCIAL_SQUARE`, `STATIC_SOCIAL_STORY`, `STATIC_DISPLAY_BANNER`, `EMAIL_HERO`; duplicates → **400**; unknown → **400** |
| `RequestedVariantCount` | yes | integer **1–4** and **≥** `Formats.Count` |
| `RevisionParentCreativePackageVersionId` | conditional | required when `JobKind=REVISION`; must be null/omitted when `INITIAL` (if supplied on INITIAL → **400**) |
| `RevisionNotes` | conditional | required non-empty trimmed when `JobKind=REVISION` (max length bounded, recommend 4000); must be null/omitted/empty when `INITIAL` (if non-empty on INITIAL → **400**) |
| `SourceSystem` | yes | Phase 1–5 idempotency pattern |
| `IdempotencyKey` | yes | unique with `SourceSystem` for the **job** |

### Forbidden brief fields

The request body **must reject** (400) any of the following if present
(including aliases / nested objects):

- match / matching ids
- campaign ids
- placement ids
- inventory ids
- readiness / QA / legal flags (`campaignReady`, `qaApproved`,
  `blissReady`, `legalCleared`, etc.)
- provider configuration (AI or asset endpoint/keys/models)
- HTML, CSS, SVG, script, `src`, `url`, `href`, `base64`, or image bytes
- concept override / alternate `SelectedConceptId` / concept package id
  override (server pins current-approved only)

Server also binds, without client override:

- required `ApprovedConceptPackageVersionId` = workspace
  `CurrentApprovedConceptPackageVersionId`;
- required `SelectedConceptId` = latest `APPROVE` decision's
  `SelectedConceptId` for that package;
- required Brand DNA, Color Profile, and Research Report provenance ids
  (+ version numbers) copied from the pinned concept package's pins
  (not re-derived from possibly-drifted workspace pointers alone —
  concept package pins win; if workspace current Brand DNA / color /
  research pointers have moved since concept approval, production still
  uses the **concept package's pinned** provenance for creative
  constraints and claim validation).

If current-approved concept package is null, or no latest APPROVE
decision with valid `SelectedConceptId` exists → **400**.

### Format → exact canvas (server-derived)

| Format | Canvas width × height (px) |
| --- | --- |
| `STATIC_SOCIAL_SQUARE` | 1080 × 1080 |
| `STATIC_SOCIAL_STORY` | 1080 × 1920 |
| `STATIC_DISPLAY_BANNER` | 1200 × 628 |
| `EMAIL_HERO` | 1200 × 600 |

Canvas dimensions are server-derived from each variant's assigned format.
Clients must not supply alternate width/height; if supplied and
mismatched → **400**.

### JobKind rules

#### `INITIAL`

- No parent package.
- Produces a new draft creative package for the pinned selected concept.
- Brief may not carry revision parent/notes.

#### `REVISION`

- Requires exactly one
  `RevisionParentCreativePackageVersionId`.
- Parent must be same workspace, same pinned concept package id, same
  `SelectedConceptId`, and same Brand DNA / Color Profile / Research
  Report provenance pins as the job will pin.
- Current-approved concept package + selected concept must still match
  those pins at job start; otherwise → **400**.
- One parent only (no multi-parent graphs).
- All revisions **rerun all six profiles** for audit simplicity and create
  an entirely new package + entirely new assets (no byte reuse, no
  partial contribution copy).
- Prior package status is unchanged by job start; supersede happens only
  via later human APPROVE of a successor.

Canonical input JSON for hashing is a stable sorted-key object of the
canonical brief fields (`jobKind`, `objective`, `formats`,
`requestedVariantCount`, conditional revision parent id + notes), the
pinned concept package id, `selectedConceptId`, the three DNA/color/
research provenance version ids from the concept package pins, schema
version `creative-production-brief.v1`, and AI/asset-provider contract
versions used. Persist both the canonical JSON string and its lowercase
hex SHA-256 on the job. Actor labels are excluded from the hash.

## Provider contracts

### `IWeddingPlannerAiProvider` (reuse)

Each of the six profiles calls `CompleteAsync` once with:

- stage `LogicalRole` (`CREATIVE_DIRECTION` / `STRATEGY_ADAPTATION` /
  `VISUAL_SYSTEM` / `IMAGE_DIRECTION` / `COPY_SYSTEM` /
  `VARIANT_PRODUCTION`);
- the stage prompt pack;
- JSON response format;
- context built only from same-tenant durable brief, pinned selected
  concept snapshot (from Phase 5 package), pinned Brand DNA framing
  summary/id (constraint), pinned Color Profile summary/palette roles
  (constraint), pinned Research Report summary/source catalog ids
  (evidence boundary for preserved claims only), Phase 5 contribution
  summaries as read-only provenance (not re-authored), revision notes
  when `REVISION`, and prior **Phase 6** stage outputs already persisted
  on the job.

Shared provider/model is allowed. Profile prompt packs remain separately
versioned. Provider failures fail the current run and job; they do not
fabricate packages or call the asset provider.

### Local / Development AI output

When the Local deterministic AI provider is active, every stage output and
the merged package must include a conspicuous marker string:

`SYNTHETIC DEVELOPMENT CREATIVE PACKAGE`

Local outputs must never claim live market facts, live brand legal
clearance, campaign-ready status, or final production artwork. Dev/CI may
use Local freely.

### OpenAI / remote stage instructions (strict)

Remote stage system instructions must:

1. require strict JSON matching the stage schema only;
2. forbid unknown fields;
3. forbid `html`, `css`, `svg`, `script`, `src`, `url`, `href`, `base64`,
   and image bytes in any field;
4. forbid inventing new factual claims or source ids; when preserving
   claims, require exact statement text + exact same source IDs from the
   pinned selected Phase 5 concept;
5. forbid treating Brand DNA or color profile ids as factual source ids;
6. label all marketing copy fields as creative / non-factual unless a
   preserved factual claim object is explicitly supplied;
7. produce only for the pinned `SelectedConceptId` — never invent sibling
   concepts or override selection;
8. emit exactly the requested variant ids `variant_1` .. `variant_N`
   where N = `RequestedVariantCount`, and no others;
9. ensure each requested format is used by at least one variant;
10. for Image Prompt Designer and Variant Producer, emit prompts/specs
    only — never bytes;
11. palette role refs must exist on the pinned color profile.

### Production guard (AI)

When the host environment is Production (or an explicit
`RequireRemoteAiProvider` / Phase 2-equivalent flag is set), starting a
creative-production job with the Local AI provider is rejected
(**400/503** as implemented consistently with Phase 2–5 production
expectations).

### `IWeddingPlannerCreativeAssetProvider` (new)

Wedding Planner **orchestrates** this provider and is not itself an image
generator. AI outputs never supply bytes; only the provider returns raster
bytes after the six AI stages succeed and the server has a validated
merged variant plan.

Contract:

| Member / concern | Rule |
| --- | --- |
| Operation | Generate **one raster PNG** per variant from server-built render spec (format/canvas, copy, palette refs, image prompt/spec refs) |
| Local adapter | Deterministic provider generates **valid deterministic PNG bytes** for the exact canvas; output is stable for identical inputs |
| Remote adapter | `RemoteHttp` posts to the **configured endpoint only**; accepts raw `image/png` response body only plus **bounded safe receipt headers** (e.g. request id, provider latency, bounded cost fields). Rejects returned URLs, base64 payloads, HTML, or non-PNG content types |
| App fetch policy | App calls **only** the configured endpoint; accepts **no** returned URLs/base64 to fetch later |
| Local rejection | Local asset provider is rejected outside Development **or** when an explicit `RequireRemoteCreativeAssetProvider` flag is set (**400/503** consistent with AI production guard) |
| Failure | Provider failure fails the job **after** six successful AI runs with **no** package insert (see failure saga) |

Asset-provider calls are **not** AI workers and create **no**
`WeddingPlannerAgentRun` rows. Persist provider aggregate receipt/cost on
the job and per-asset receipt metadata on each `CreativeAsset` row.

### Forbidden providers / tools in Phase 6

- App-side arbitrary URL fetch for creative media
- Accepting provider-returned asset URLs or base64 for secondary fetch
- HTML/CSS/SVG render sandboxes that execute AI-authored markup
- Non-PNG creative binaries (JPEG/WebP/GIF/SVG/HTML)
- Treating the Local asset provider as Production-safe without the
  remote requirement flag

## V1 asset safety and storage

### Normative limits

| Limit | Value |
| --- | --- |
| Format | **PNG ONLY** |
| Storage | PostgreSQL `bytea` on `CreativeAsset` |
| Max size | **≤ 2 MiB** per asset |
| Max assets per package | **≤ 4** (equals max requested variants) |
| Dimensions | Exact server-derived canvas for the variant format (table above) |
| One raster per variant | Exactly one PNG asset per `variant_1` .. `variant_N` |

### PNG validator (fail closed)

Before insert, every provider PNG must satisfy **all** of:

1. PNG signature (`89 50 4E 47 0D 0A 1A 0A`);
2. only 8-bit **truecolor** (`color type 2`) or **truecolor-alpha**
   (`color type 6`), **non-interlaced**;
3. chunks limited to **`IHDR` / `IDAT` / `IEND`** only (no `tEXt`,
   `iTXt`, `zTXt`, `iCCP`, `eXIf`, `tIME`, or other ancillary chunks);
4. valid CRCs and strict chunk order (`IHDR` first, one or more `IDAT`,
   `IEND` last);
5. exact `IHDR` width/height matching the variant's server-derived canvas;
6. bounded zlib inflation of IDAT payload to **exactly** the expected
   scanline byte count for the declared IHDR
   (`height * (1 + width * bytesPerPixel)`);
7. each scanline filter byte in `0..4`;
8. recomputed SHA-256 of the accepted raw PNG bytes stored on the asset
   row.

This excludes metadata/embedded profiles and SVG/JPEG/WebP/HTML. Invalid
bytes → asset failure path (no package).

### Bounded bytea tradeoff (accepted)

V1 stores draft PNG bytes in PostgreSQL `bytea` for same-tenant
transactional integrity with package + contribution inserts, simple
backup/restore, and a single trust boundary for the same-origin content
endpoint. Tradeoffs accepted for V1: larger DB backups, tighter per-asset
size caps (2 MiB / ≤4 assets), and no CDN. **Future extraction** to an
object/asset store may move bytes behind the same content API while
keeping package JSON asset-id references stable; Phase 6 does not
implement that extraction.

## Durable records

### Extend `WeddingPlannerAgentRun`

Add nullable (reuse Phase 4/5 patterns where compatible):

- `WorkerProfileVersion` — e.g. `CREATIVE_DIRECTION_V1`
- `AssignedRolesJson` — JSON array of the logical roles assigned to that
  run
- `OutputCreativePackageVersionId` — set on the final successful variant
  production run when a package is created; null on earlier stage runs
  and failures

Existing Concierge / Brand DNA / Curator / Workshop runs leave
`OutputCreativePackageVersionId` null. Creative-production stage
`LogicalRole` uses the six stage values above for backward-compatible
listing; the thirteen durable role contributions remain authoritative.

Internal run idempotency keys are the job key with a stable suffix, e.g.
`{jobKey}:CREATIVE_DIRECTION`, `:STRATEGY_ADAPTATION`, `:VISUAL_SYSTEM`,
`:IMAGE_DIRECTION`, `:COPY_SYSTEM`, `:VARIANT_PRODUCTION` (exact suffix
strings locked in implementation constants). Each successful job
therefore owns exactly six run rows with tokens, cost, status,
worker/prompt/provider/model/adapter evidence.

### `WeddingPlannerCreativeProductionJob`

Single entity for both `INITIAL` and `REVISION` (statuses `RUNNING` |
`SUCCEEDED` | `FAILED`):

- advertiser/workspace scope
- `JobKind` (`INITIAL` \| `REVISION`)
- canonical `InputJson`, `InputSha256`
- brief fields (`Objective`, `FormatsJson`, `RequestedVariantCount`,
  conditional revision parent id + notes)
- pinned `ApprovedConceptPackageVersionId`, `SelectedConceptId`
- pinned Brand DNA, Color Profile, and Research Report provenance ids
  (+ version numbers snapshot from concept package pins)
- six stage output JSON fields (raw/canonical per-stage worker outputs)
- six agent-run FKs (nullable until created):
  `CreativeDirectionAgentRunId`, `StrategyAdaptationAgentRunId`,
  `VisualSystemAgentRunId`, `ImageDirectionAgentRunId`,
  `CopySystemAgentRunId`, `VariantProductionAgentRunId`
- asset-provider aggregate receipt/cost fields (nullable until assets
  attempted/completed)
- `OutputCreativePackageVersionId` (null unless SUCCEEDED)
- status, bounded error code/message, timestamps, actor metadata
- `SourceSystem`, `IdempotencyKey`

Unique `(SourceSystem, IdempotencyKey)` is the **job** idempotency
boundary. Replaying a `FAILED` job returns the failed job and **never**
retries AI or asset calls. Replaying a `SUCCEEDED` job returns the
existing job/package linkage with **no** AI calls and **no** asset
provider calls.

### `WeddingPlannerCreativePackageVersion`

Immutable snapshot:

- advertiser/workspace scope; monotonic `VersionNumber` per workspace
- `SchemaVersion` = `creative-package.v1`
- `DocumentJson`, `Summary`
- producing job id; producing variant-production agent-run id
- pinned concept package id + `SelectedConceptId`
- Brand DNA + Color Profile + Research Report provenance ids (+ version
  numbers)
- `JobKind`; optional `ParentCreativePackageVersionId` (REVISION only)
- `Status`: `PROPOSED` | `APPROVED` | `REJECTED` | `SUPERSEDED`
- source/idempotency: recommend unique `(SourceSystem, IdempotencyKey)` on
  package with `{jobKey}:PACKAGE`
- created-at / actor metadata

No PUT/PATCH of payload fields after insert. Variant selection is **not**
stored on this row. Package JSON references asset **ids only** — never
bytes or URLs.

### `WeddingPlannerCreativeRoleContribution`

Exactly **13** immutable rows per successful package:

- advertiser/workspace/package/job scope
- `LogicalRole` (one of the thirteen; unique per package)
- `ProducingAgentRunId` (the stage run that emitted that role)
- contribution payload JSON (role-owned fields only)
- created-at

No extras. No missing roles. Contributions are never edited in place.
Never create thirteen fake agent runs to “match” these thirteen rows.
Phase 5 contributions are **not** duplicated into this table.

### `WeddingPlannerCreativeAsset`

Immutable asset row per variant PNG:

- advertiser/workspace/package/job/variant scope
- `VariantId` (`variant_1` .. `variant_N`)
- `Format`, exact `Width`, `Height`
- `ContentType` = `image/png`
- `Bytes` (`bytea`), `ByteSize`, `Sha256` (lowercase hex)
- provider name/adapter, bounded receipt headers/json, provider cost
  fields
- created-at

No update of bytes after insert. Package `DocumentJson` stores asset id +
metadata refs only.

### `WeddingPlannerCreativePackageDecision`

Immutable human decision:

- `Decision`: `APPROVE` | `REJECT`
- required non-empty rationale
- `SelectedVariantId`:
  - on `APPROVE`: **required**; must be exactly one of
    `variant_1` .. `variant_N` present in the package
  - on `REJECT`: **must be null / omitted**; if supplied → **400**
- actor, source/idempotency, timestamp
- Unique `(SourceSystem, IdempotencyKey)`

Selection lives **only** on this decision. Approving never mutates
`DocumentJson`, contribution rows, or asset bytes. Approval is draft
creative approval only — never campaign-ready / final / QA / legal /
matching.

### Workspace pointer

Add nullable `CurrentApprovedCreativePackageVersionId` on
`WeddingPlannerWorkspace`. Approval sets it. Approving a later version
sets the prior current package to `SUPERSEDED` without mutating
`DocumentJson` or assets. Rejected versions never become current. Only
`PROPOSED` versions accept a first decision; later attempts on
non-`PROPOSED` are **400**.

## Output schemas

### Stage ownership (normative)

| Stage | Roles | May emit | Must not emit |
| --- | --- | --- | --- |
| `CREATIVE_DIRECTION_V1` | Creative Director + Campaign Strategist | selected-concept creative north star; campaign framing notes; production principles for requested formats/variants | detailed layouts, typography scales, image prompts, final copy strings, variant canvas assignments |
| `STRATEGY_ADAPTATION_V1` | Audience + Offer + Channel Strategists | audience adaptation, offer framing, channel/format adaptation notes referencing prior direction | visual system tokens, image prompts, final headline/body/cta, asset bytes |
| `VISUAL_SYSTEM_V1` | Visual + Layout + Typography Designers | visual system, layout regions/geometry language, typography roles/scales; palette role refs to pinned color profile | final marketing copy invention beyond referencing strategy; image bytes; new concept ids |
| `IMAGE_DIRECTION_V1` | Image Prompt Designer | exactly one image prompt/spec per requested variant | copy text ownership; layout ownership overrides; bytes/URLs/base64 |
| `COPY_SYSTEM_V1` | Headline + Body + CTA Specialists | per-variant `copy.kind=CREATIVE_NON_FACTUAL` headline/body/cta; optional preserved `factualClaims` | new factual claims; visual/layout ownership; image bytes |
| `VARIANT_PRODUCTION_V1` | Variant Producer | exact `variant_1`..`N` production specs: format assignment, canvas, refs to prior stage outputs, image-prompt ref, copy ref, palette refs | new copy invention; new prompts beyond binding refs; bytes; extra/missing variants |

Variant ids must remain exactly `variant_1` .. `variant_N` across stages
that emit variants. Renames, extras, or gaps fail closed. Each requested
format must be used at least once among the N variants.

### Per-stage AI output examples

#### Creative direction — `creative-direction-worker-output.v1`

```json
{
  "schemaVersion": "creative-direction-worker-output.v1",
  "workerProfileVersion": "CREATIVE_DIRECTION_V1",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "selectedConceptId": "concept_1",
  "contributions": [
    {
      "logicalRole": "CREATIVE_DIRECTOR",
      "summary": "North star for the selected concept.",
      "direction": {
        "northStar": "Quiet confidence rendered as draft production artwork.",
        "principles": ["generous negative space", "one focal CTA"]
      }
    },
    {
      "logicalRole": "CAMPAIGN_STRATEGIST",
      "summary": "Campaign framing without campaign ids.",
      "framing": {
        "objectiveEcho": "…",
        "formatPlanNotes": "Cover square and story with consistent voice."
      }
    }
  ]
}
```

Rules:

- `contributions` must include exactly `CREATIVE_DIRECTOR` and
  `CAMPAIGN_STRATEGIST`.
- `selectedConceptId` must equal the job pin.
- No variant ids required yet; no copy/image bytes.

#### Strategy adaptation — `strategy-adaptation-worker-output.v1`

```json
{
  "schemaVersion": "strategy-adaptation-worker-output.v1",
  "workerProfileVersion": "STRATEGY_ADAPTATION_V1",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "selectedConceptId": "concept_1",
  "contributions": [
    {
      "logicalRole": "AUDIENCE_STRATEGIST",
      "summary": "Audience adaptation.",
      "audienceAdaptation": { "notes": "…" }
    },
    {
      "logicalRole": "OFFER_STRATEGIST",
      "summary": "Offer framing (non-factual).",
      "offerFraming": { "notes": "…" }
    },
    {
      "logicalRole": "CHANNEL_STRATEGIST",
      "summary": "Channel/format adaptation.",
      "channelAdaptation": {
        "formats": ["STATIC_SOCIAL_SQUARE", "STATIC_SOCIAL_STORY"]
      }
    }
  ]
}
```

Rules:

- Exactly the three strategist roles; no extras/gaps.
- Channel adaptation may reference only formats present in the brief.

#### Visual system — `visual-system-worker-output.v1`

```json
{
  "schemaVersion": "visual-system-worker-output.v1",
  "workerProfileVersion": "VISUAL_SYSTEM_V1",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "selectedConceptId": "concept_1",
  "contributions": [
    {
      "logicalRole": "VISUAL_DESIGNER",
      "summary": "Visual system.",
      "visualSystem": {
        "paletteRoleRefs": ["primary", "background", "accent"],
        "atmosphere": "…"
      }
    },
    {
      "logicalRole": "LAYOUT_DESIGNER",
      "summary": "Layout system.",
      "layoutSystem": {
        "geometryLanguage": "centered stack with CTA footer band"
      }
    },
    {
      "logicalRole": "TYPOGRAPHY_DESIGNER",
      "summary": "Typography system.",
      "typographySystem": {
        "headlineRole": "display",
        "bodyRole": "text",
        "ctaRole": "label"
      }
    }
  ]
}
```

Rules:

- Exactly the three designer roles.
- `paletteRoleRefs` must exist on the pinned color profile.

#### Image direction — `image-direction-worker-output.v1`

```json
{
  "schemaVersion": "image-direction-worker-output.v1",
  "workerProfileVersion": "IMAGE_DIRECTION_V1",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "selectedConceptId": "concept_1",
  "contributions": [
    {
      "logicalRole": "IMAGE_PROMPT_DESIGNER",
      "summary": "Image prompts per variant.",
      "imagePrompts": [
        {
          "variantId": "variant_1",
          "prompt": "Soft natural light, empty ceremony atmosphere, no text in frame.",
          "negativeConstraints": ["no logos", "no readable signage"]
        },
        {
          "variantId": "variant_2",
          "prompt": "Vertical soft gradient wash suggesting dusk reception.",
          "negativeConstraints": ["no faces", "no watermarks"]
        }
      ]
    }
  ]
}
```

Rules:

- Exactly one contribution: `IMAGE_PROMPT_DESIGNER`.
- Exactly N prompt objects keyed by `variant_1`..`variant_N`.
- No URLs, bytes, or base64.

#### Copy system — `copy-system-worker-output.v1`

```json
{
  "schemaVersion": "copy-system-worker-output.v1",
  "workerProfileVersion": "COPY_SYSTEM_V1",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "selectedConceptId": "concept_1",
  "contributions": [
    {
      "logicalRole": "HEADLINE_SPECIALIST",
      "summary": "Headlines per variant.",
      "headlines": [
        { "variantId": "variant_1", "text": "Your day, thoughtfully planned." },
        { "variantId": "variant_2", "text": "A calm next step." }
      ]
    },
    {
      "logicalRole": "BODY_COPY_SPECIALIST",
      "summary": "Body copy per variant.",
      "bodies": [
        {
          "variantId": "variant_1",
          "text": "A calm invitation to explore options together.",
          "factualClaims": []
        },
        {
          "variantId": "variant_2",
          "text": "Host with confidence and clarity.",
          "factualClaims": [
            {
              "statement": "Couples often research venues months ahead.",
              "sourceIds": ["src_1"]
            }
          ]
        }
      ]
    },
    {
      "logicalRole": "CTA_SPECIALIST",
      "summary": "CTAs per variant.",
      "ctas": [
        { "variantId": "variant_1", "text": "Start planning" },
        { "variantId": "variant_2", "text": "Continue" }
      ]
    }
  ]
}
```

Rules:

- Exactly the three copy specialist roles.
- Assembled per-variant `copy.kind` must be exactly `CREATIVE_NON_FACTUAL`.
- `factualClaims`, when present, may **only** preserve an exact
  `statement` string and the **same** `sourceIds` array membership from
  the pinned selected Phase 5 concept's `factualClaims`. New statements,
  altered wording, added/removed source ids, or provenance ids used as
  sources → fail closed.
- Brand DNA / color / research / concept / job / package ids **cannot**
  be used as `sourceIds`.

#### Variant production — `variant-production-worker-output.v1`

```json
{
  "schemaVersion": "variant-production-worker-output.v1",
  "workerProfileVersion": "VARIANT_PRODUCTION_V1",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "selectedConceptId": "concept_1",
  "contributions": [
    {
      "logicalRole": "VARIANT_PRODUCER",
      "summary": "Production specs for requested variants.",
      "variants": [
        {
          "id": "variant_1",
          "format": "STATIC_SOCIAL_SQUARE",
          "canvas": { "width": 1080, "height": 1080 },
          "refs": {
            "direction": "contributions.CREATIVE_DIRECTOR",
            "visualSystem": "contributions.VISUAL_DESIGNER",
            "layout": "contributions.LAYOUT_DESIGNER",
            "typography": "contributions.TYPOGRAPHY_DESIGNER",
            "imagePrompt": "imagePrompts.variant_1",
            "headline": "headlines.variant_1",
            "body": "bodies.variant_1",
            "cta": "ctas.variant_1"
          },
          "paletteRoleRefs": ["primary", "background", "accent"]
        },
        {
          "id": "variant_2",
          "format": "STATIC_SOCIAL_STORY",
          "canvas": { "width": 1080, "height": 1920 },
          "refs": {
            "direction": "contributions.CREATIVE_DIRECTOR",
            "visualSystem": "contributions.VISUAL_DESIGNER",
            "layout": "contributions.LAYOUT_DESIGNER",
            "typography": "contributions.TYPOGRAPHY_DESIGNER",
            "imagePrompt": "imagePrompts.variant_2",
            "headline": "headlines.variant_2",
            "body": "bodies.variant_2",
            "cta": "ctas.variant_2"
          },
          "paletteRoleRefs": ["primary", "secondary", "background"]
        }
      ]
    }
  ]
}
```

Rules:

- Exactly one contribution: `VARIANT_PRODUCER`.
- Exactly N variants `variant_1`..`variant_N`.
- Each brief format used ≥ once.
- Canvas exact for format; palette refs valid on pinned color profile.
- Refs must resolve to prior stage outputs; dangling refs fail closed.
- No bytes/URLs/markup.

### Canonical package `creative-package.v1`

Server merges and canonicalizes after all six AI stages succeed, then
calls the asset provider per variant, validates PNGs, and only then
transactionally inserts package + 13 contributions + N assets:

```json
{
  "schemaVersion": "creative-package.v1",
  "disclaimer": "Approval of this package is draft creative approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, QA, or final production-artwork approval. It does not authorize Bliss matching or placement. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim exactly preserves a cited claim from the pinned selected concept using source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Draft PNG assets are provider-generated renditions for review only; Wedding Planner orchestrates providers and is not itself an image generator. Phase 5 concept contributions remain pinned provenance and are not re-approved here.",
  "marker": "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE",
  "provenance": {
    "approvedConceptPackageVersionId": "<guid>",
    "selectedConceptId": "concept_1",
    "approvedBrandDnaVersionId": "<guid>",
    "approvedBrandDnaVersionNumber": 1,
    "approvedColorProfileVersionId": "<guid>",
    "approvedColorProfileVersionNumber": 1,
    "approvedResearchReportVersionId": "<guid>",
    "approvedResearchReportVersionNumber": 1,
    "creativeProductionJobId": "<guid>",
    "jobKind": "INITIAL",
    "parentCreativePackageVersionId": null
  },
  "selectedConceptSnapshot": {
    "id": "concept_1",
    "name": "Quiet Confidence",
    "rationale": "…",
    "visualDirection": "…",
    "paletteRoleRefs": ["primary", "background", "accent"],
    "copy": {
      "kind": "CREATIVE_NON_FACTUAL",
      "headline": "…",
      "body": "…",
      "cta": "…"
    },
    "factualClaims": []
  },
  "brief": {
    "objective": "…",
    "formats": ["STATIC_SOCIAL_SQUARE", "STATIC_SOCIAL_STORY"],
    "requestedVariantCount": 2,
    "revisionNotes": null
  },
  "variants": [
    {
      "id": "variant_1",
      "format": "STATIC_SOCIAL_SQUARE",
      "canvas": { "width": 1080, "height": 1080 },
      "paletteRoleRefs": ["primary", "background", "accent"],
      "copy": {
        "kind": "CREATIVE_NON_FACTUAL",
        "headline": "…",
        "body": "…",
        "cta": "…"
      },
      "factualClaims": [],
      "imagePrompt": "…",
      "asset": {
        "creativeAssetId": "<guid>",
        "contentType": "image/png",
        "byteSize": 12345,
        "sha256": "…",
        "width": 1080,
        "height": 1080
      }
    },
    {
      "id": "variant_2",
      "format": "STATIC_SOCIAL_STORY",
      "canvas": { "width": 1080, "height": 1920 },
      "paletteRoleRefs": ["primary", "secondary", "background"],
      "copy": {
        "kind": "CREATIVE_NON_FACTUAL",
        "headline": "…",
        "body": "…",
        "cta": "…"
      },
      "factualClaims": [],
      "imagePrompt": "…",
      "asset": {
        "creativeAssetId": "<guid>",
        "contentType": "image/png",
        "byteSize": 23456,
        "sha256": "…",
        "width": 1080,
        "height": 1920
      }
    }
  ],
  "contributions": [
    { "logicalRole": "CREATIVE_DIRECTOR", "summary": "…" },
    { "logicalRole": "CAMPAIGN_STRATEGIST", "summary": "…" },
    { "logicalRole": "AUDIENCE_STRATEGIST", "summary": "…" },
    { "logicalRole": "OFFER_STRATEGIST", "summary": "…" },
    { "logicalRole": "CHANNEL_STRATEGIST", "summary": "…" },
    { "logicalRole": "VISUAL_DESIGNER", "summary": "…" },
    { "logicalRole": "LAYOUT_DESIGNER", "summary": "…" },
    { "logicalRole": "TYPOGRAPHY_DESIGNER", "summary": "…" },
    { "logicalRole": "IMAGE_PROMPT_DESIGNER", "summary": "…" },
    { "logicalRole": "HEADLINE_SPECIALIST", "summary": "…" },
    { "logicalRole": "BODY_COPY_SPECIALIST", "summary": "…" },
    { "logicalRole": "CTA_SPECIALIST", "summary": "…" },
    { "logicalRole": "VARIANT_PRODUCER", "summary": "…" }
  ]
}
```

`Summary` is a short server-built string naming schema version, jobKind,
selectedConceptId, variant count N, contribution count (13), and format
list.

Validation fail closed if merged output:

- lacks exactly **N** variants with ids `variant_1`..`variant_N`;
- fails to use each requested format at least once;
- lacks exactly **13** contributions in workforce order;
- omits the exact disclaimer string;
- contains dangling factual-claim source ids or non-preserved claims;
- uses Brand DNA / color / provenance ids as source ids;
- contains unknown fields or forbidden markup/media keys / bytes / URLs;
- has any variant whose format/canvas diverges from the server table;
- references missing asset ids or embeds bytes/URLs in package JSON;
- was produced under Local AI without the conspicuous
  `SYNTHETIC DEVELOPMENT CREATIVE PACKAGE` marker (required on Local
  path; recommended retained on merged document whenever Local AI was
  used).

**No partial packages.** Transactional insert of package + 13
contributions + N assets happens only after all six AI stages succeed,
merge validation succeeds, and **all** N assets are provider-returned and
PNG-validated. Failure at any of those points creates **no** package,
**no** contributions, and **no** durable asset rows (transient provider
bytes are discarded).

## Asset content API

Authenticated same-tenant:

`GET /api/wedding-planner/creative-assets/{id}/content`

Normative response behavior:

| Concern | Rule |
| --- | --- |
| Body | Stream raw PNG bytes from `CreativeAsset.Bytes` |
| `Content-Type` | `image/png` |
| `X-Content-Type-Options` | `nosniff` |
| `Cache-Control` | `private, no-store` |
| `ETag` | strong etag derived from stored SHA-256 (or equivalent durable hash) |
| `Content-Disposition` | `inline; filename="creative-asset-{id}.png"` (bounded safe name) |
| Redirects | **never** redirect to external URLs |
| Cross-tenant | **404** |
| Anonymous (OIDC on) | **401** |

UI may render `<img src="…/creative-assets/{id}/content">` **only** to
this same-origin endpoint. This Phase 6 exception does **not** alter the
Phase 5 concept-prototype placeholder-only rule.

## Flow

1. authorize workspace write access;
2. require current-approved concept package + latest APPROVE
   `SelectedConceptId`; else **400**;
3. pin concept package, selected concept, and DNA/color/research
   provenance from concept package pins;
4. validate brief (`JobKind`, formats 1–4 unique from Phase 5 four,
   variant count 1–4 and ≥ format count, revision parent/notes
   conditional rules, forbidden fields);
5. for `REVISION`, validate single parent same-workspace + exact same
   pins/selected concept + current-approved concept still matches;
6. replay existing job when `(SourceSystem, IdempotencyKey)` exists
   (SUCCEEDED → return existing; FAILED → return existing, no retry);
7. insert `RUNNING` job with canonical input/SHA and pins;
8. create run #1 (`CREATIVE_DIRECTION`), invoke AI, persist stage output;
9. create run #2 (`STRATEGY_ADAPTATION`), invoke AI, persist stage output;
10. create run #3 (`VISUAL_SYSTEM`), invoke AI, persist stage output;
11. create run #4 (`IMAGE_DIRECTION`), invoke AI, persist stage output;
12. create run #5 (`COPY_SYSTEM`), invoke AI, persist stage output;
13. create run #6 (`VARIANT_PRODUCTION`), invoke AI, persist stage output;
14. deterministic server merge/validate `creative-package.v1` plan
    (variants/refs/copy/claims/palette) — still **no** package insert;
15. call `IWeddingPlannerCreativeAssetProvider` once per variant; validate
    each PNG; on any failure → job `FAILED`, no package;
16. transactional insert `PROPOSED` package + exactly 13 contributions +
    N assets; link run/package FKs; persist asset-provider aggregate;
    job `SUCCEEDED`;
17. audit throughout.

## Failure saga (normative)

| Condition | Agent runs | Assets | Package | Job |
| --- | --- | --- | --- | --- |
| AI stage N fails | prior SUCCEEDED kept; current `FAILED`; later not created | none | none | `FAILED` |
| Merge/validate fails after six successful runs | all 6 SUCCEEDED kept | none | none | `FAILED` |
| Asset provider/PNG validation fails after six successful runs | all 6 SUCCEEDED kept | none durable | none | `FAILED` |
| Idempotent SUCCEEDED replay | unchanged (exactly 6 runs) | unchanged | unchanged | return existing; **no** AI/asset calls |
| Idempotent FAILED replay | unchanged | none | none | return existing; **never** retry |

Costs are tracked separately: six AI run token/cost fields **plus**
asset-provider aggregate/per-asset cost fields. Successful path evidence:
exactly **6** agent runs with token/cost fields, exactly **1** `PROPOSED`
package, exactly **13** role contributions, exactly **N** creative
assets. Provider/AI/asset transport failures surface as **502** after
durable failure persistence when appropriate; validation/auth errors
remain 400/401/403/404.

## API

Phase 1–5 routes remain unchanged. Add under `/api/wedding-planner`:

- `POST /workspaces/{id}/creative-production-jobs`
- `GET /workspaces/{id}/creative-production-jobs`
- `GET /creative-production-jobs/{id}`
- `GET /workspaces/{id}/creative-packages`
- `GET /creative-packages/{id}`
- `GET /creative-packages/{id}/contributions`
- `GET /creative-packages/{id}/assets`
- `GET /creative-packages/{id}/agent-runs`
- `POST /creative-packages/{id}/decisions`
- `GET /creative-assets/{id}`
- `GET /creative-assets/{id}/content`
- `GET /workspaces/{id}/agent-runs` (workspace-scoped list; includes
  creative-production + prior phase runs)

No endpoint that accepts client-uploaded creative bytes for package
creation. No PUT/PATCH/DELETE for package, contribution, or asset
payloads. No endpoint that mutates selected variant onto the package
row. No endpoint that marks campaign-ready / QA / legal.

### Decision body

```json
{
  "decision": "APPROVE",
  "rationale": "Variant 1 best fits the selected concept.",
  "selectedVariantId": "variant_1",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

```json
{
  "decision": "REJECT",
  "rationale": "None of the variants are usable.",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

| Decision | `SelectedVariantId` |
| --- | --- |
| `APPROVE` | required; must be one of `variant_1` .. `variant_N` in package |
| `REJECT` | forbidden (null/omitted only) |

## Failure and audit

| Condition | Behavior |
| --- | --- |
| Anonymous (OIDC on) | 401 |
| Viewer / no write authority | 403 on job create and decisions |
| Missing or cross-tenant id (including assets) | 404 |
| Invalid brief, forbidden fields, empty rationale, illegal status transition, missing concept prerequisites, revision pin mismatch, Local AI/asset provider in Production guard, APPROVE without/invalid selection, REJECT with selection | 400 (or documented 503 for misconfigured production provider) |
| Idempotent replay | existing job/decision; no second AI/asset/package rows; audit replay |

Audit actions (append-only): creative-production job
started/succeeded/failed/replayed; each creative agent run
start/success/failure/replay; asset provider attempt/success/failure
(aggregate); creative package proposed/approved/rejected/superseded/
replayed; asset content read (optional bounded access audit). Events
carry actor, request id, workspace/advertiser, job/package/run/asset
ids, and on approve the `selectedVariantId`. Secrets are never stored.

Cost evidence: persist token/cost fields on each of the six agent runs
and asset-provider cost fields separately. Package DTO/list views may
surface summed estimated USD for operators without exposing secrets.

## UI requirements

### Public (`frontend/public`)

- Show Creative Production only when an advertiser-capable session can
  access the workspace and a current-approved concept package with
  `SelectedConceptId` exists — or clearly disable submit with
  prerequisites stated.
- Controls: brief form (`JobKind`, objective, multi-select 1–4 formats,
  requested variant count, conditional revision parent + notes), submit
  job, list jobs/packages, inspect selected-concept snapshot + 13
  contributions + N variants + linked agent runs, approve/reject with
  required rationale and variant selection on approve only.
- Display the draft-creative disclaimer **verbatim**.
- When Local/synthetic AI path was used, show
  `SYNTHETIC DEVELOPMENT CREATIVE PACKAGE` conspicuously.
- May render `<img>` **only** to same-origin
  `/creative-assets/{id}/content`. Must not fetch arbitrary URLs or
  render Phase 5 prototypes as images.
- Do not imply research re-approval, concept re-approval, legal
  clearance, campaign-ready, matching changes, QA, or final artwork
  certification.
- Preserve Phase 2–5 Concierge / Brand DNA / Color / Curator / Concept
  Workshop surfaces (Phase 5 remains placeholder-only).

### Operations (`frontend/operations`)

- List creative-production jobs, packages, contributions, assets,
  decisions, and agent runs for the selected workspace.
- Expose job create and approve/reject for operator/admin.
- Show provider/profile/prompt versions, input SHA-256, concept +
  DNA/color/research pins, selectedConceptId, costs/tokens (AI and
  assets separately), selectedVariantId on decisions, and
  current-approved creative-package marker.
- Local/synthetic packages must remain visually conspicuous in Dev.

## Tests (exhaustive minimum)

**Brief / prerequisites**

- Job requires current-approved concept package + latest APPROVE
  `SelectedConceptId`; missing → 400.
- Formats must be 1–4 unique from Phase 5 four; duplicates/unknown → 400.
- `RequestedVariantCount` 1–4 and ≥ format count; else 400.
- `INITIAL` forbids revision parent/notes; `REVISION` requires both.
- Revision parent must be same workspace and exact same pins/selected
  concept; current-approved concept must still match; else 400.
- Forbidden match/campaign/placement/inventory ids, readiness/QA/legal
  flags, provider config, URLs/base64/markup, concept override → 400.
- Canvas derived exactly per format table; mismatched client canvas → 400.

**Schema / merge / claims**

- Each stage accepts only its owned roles; extras/missing fail.
- Exactly 13 contributions in order after success.
- Exactly `variant_1`..`variant_N`; each requested format used ≥ once.
- `copy.kind` must be `CREATIVE_NON_FACTUAL`.
- New factual claims fail; preserved claims require exact statement +
  same source IDs from selected Phase 5 concept.
- paletteRoleRefs unknown to pinned color profile fail.
- Merge requires exact disclaimer and full pins + selected concept
  snapshot + parent id when REVISION.
- Local AI path requires conspicuous
  `SYNTHETIC DEVELOPMENT CREATIVE PACKAGE`.
- AI outputs never accepted as image bytes.

**Assets / PNG validator**

- Local deterministic provider yields valid deterministic PNGs for exact
  canvases in Development.
- RemoteHttp accepts raw `image/png` only; rejects URL/base64/HTML/
  wrong content type.
- Local asset provider rejected outside Development or when
  `RequireRemoteCreativeAssetProvider` is set.
- Validator rejects missing signature, wrong color type, interlacing,
  ancillary chunks, bad CRC/order, wrong IHDR dims, zlib oversize/
  undersize, bad filter bytes; accepts only IHDR/IDAT/IEND 8-bit
  truecolor/truecolor-alpha non-interlaced with matching SHA-256.
- ≤2 MiB/asset and ≤4 assets/package enforced.
- Package JSON references asset ids only (no bytes/URLs).

**API / persistence / saga**

- Success path: 1 job SUCCEEDED, 6 agent runs SUCCEEDED with token/cost
  evidence, 1 PROPOSED package, 13 contributions, N assets; never 13
  agent runs; Phase 5 profiles not invoked.
- Mid-stage AI fail → current run FAILED, priors kept, no package/assets.
- Merge fail after six runs → no package/assets.
- Asset fail after six runs → no package/assets.
- SUCCEEDED replay → no AI/asset calls; same ids.
- FAILED replay → no retry; same failed job.
- APPROVE requires valid `SelectedVariantId`; sets pointer; selection
  stored on decision only; package DocumentJson unchanged.
- Later APPROVE supersedes prior current without payload/asset mutation.
- REJECT forbids selection; never sets pointer; empty rationale 400;
  non-PROPOSED decision 400.
- Asset content: same-tenant streams `image/png` with nosniff, private
  no-store, ETag, inline filename; cross-tenant 404; anonymous 401; never
  redirects.
- Advertiser A cannot access B's jobs/packages/contributions/assets/runs
  (404).
- Anonymous 401; viewer 403 on writes.
- Production guards reject Local AI and Local asset provider when
  enabled.

**Safe UI / architecture**

- UI `<img>` only to same-origin asset content endpoint; Phase 5 preview
  remains placeholders-only.
- Existing Bliss matching/review/placement and Wedding Planner Phase 1–5
  tests remain green.
- Architecture tests prove Phase 6 sources do not reference deterministic
  match evaluation, Alpha Auto, or n8n authority; do not implement
  Chaperone/QA/escalation or campaign-ready handshake; do not treat Brand
  DNA/color ids as factual sources; do not re-run Phase 5 profiles; and
  treat Wedding Planner as orchestrator of
  `IWeddingPlannerCreativeAssetProvider`, not as an in-process image
  generator model.

## Exact disclaimer (locked)

The following string must appear verbatim in `creative-package.v1`
`DocumentJson.disclaimer`:

> Approval of this package is draft creative approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, QA, or final production-artwork approval. It does not authorize Bliss matching or placement. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim exactly preserves a cited claim from the pinned selected concept using source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Draft PNG assets are provider-generated renditions for review only; Wedding Planner orchestrates providers and is not itself an image generator. Phase 5 concept contributions remain pinned provenance and are not re-approved here.

## Explicit exclusions

- Phase 7 Chaperone / QA AI / escalation
- Phase 8 Bliss handshake / campaign-ready state
- Matching, review, or placement changes / Bliss writes
- Measurement / learning
- Alpha Auto
- n8n authority / GHL coupling
- Re-running or re-authoring Phase 5 concept roles/profiles
- Thirteen fake per-role agent runs
- Mutating selected variant onto the package document
- Research report or concept package mutation / re-approval via creative
  production
- Legal / compliance / accessibility certification products
- Non-PNG creative binaries; app fetch of provider-returned URLs/base64
- Object/CDN asset-store extraction (future only; documented tradeoff)
- Treating Brand DNA or color profiles as factual evidence

## Known residual risks (accepted for Phase 6)

1. **Claim risk:** Models may still phrase creative copy like a fact even
   when `copy.kind` is `CREATIVE_NON_FACTUAL`. Mitigation: disclaimer,
   preserve-only `factualClaims` gate against the selected Phase 5
   concept, UI labeling of creative vs preserved claims. Residual:
   humans can still misread tone as certified fact.
2. **Draft-asset risk:** Valid PNG renditions can be mistaken for final
   production artwork or campaign-ready creatives. Mitigation: disclaimer,
   draft-only approval language, Local
   `SYNTHETIC DEVELOPMENT CREATIVE PACKAGE` marker, no campaign-ready
   flags. Residual: screenshots/downloads may still be forwarded out of
   context.
3. **Provider-honesty risk:** Remote asset providers may return
   unexpected pixels that pass structural PNG validation. Mitigation:
   strict PNG subset validator, exact canvas dims, SHA-256 receipt,
   configured-endpoint-only policy. Residual: semantic content (e.g.
   unintended text-in-image) is not QA'd in Phase 6.
4. **Bytea operational risk:** Storing up to 4 × 2 MiB PNGs in PostgreSQL
   increases backup/row size. Mitigation: hard caps, transactional
   integrity preference for V1, documented future asset-store extraction.
   Residual: large workspaces still pressure DB storage before extraction.
5. **Revision full-rerun cost:** Every REVISION reruns all six AI profiles
   and regenerates all assets for audit simplicity. Residual: higher
   token/asset cost versus incremental patching (explicitly rejected for
   V1).
6. **Selection vs package drift risk (process):** Humans approve a
   variant id without mutating the package; later readers must join
   decision → `SelectedVariantId`. Residual: UI that fails to surface the
   decision selection could show all variants as equally current.

These residuals do not expand Phase 6 scope into Chaperone/QA, legal
review, campaign-ready handshake, or Bliss writes.

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 6 does not auto-advance to Phase 7. Phases 1–5 behavior
and all Bliss matching boundaries remain preserved.
