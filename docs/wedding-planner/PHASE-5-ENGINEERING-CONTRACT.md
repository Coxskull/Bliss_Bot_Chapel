# Wedding Planner Phase 5 — Engineering Contract

Status: authorized for implementation by the request to proceed to Phase 5.

## Objective

Add the **Concept / Prototype Workshop** as Wedding Planner creative-direction
software:

1. an authenticated human submits a workshop brief for an authorized
   workspace that already has current-approved Brand DNA, Color Profile, and
   Research Report versions;
2. **exactly three** executable AI profiles synthesize **exactly four**
   durable role contributions; and
3. the server merges a canonical immutable `concept-package.v1` containing
   exactly three concepts and structured `prototype-spec.v1` specs. Only a
   human `APPROVE` or `REJECT` decision may change current-approved concept
   package pointer metadata. Selection of a winning concept lives on the
   decision row and never mutates the package.

PostgreSQL remains permanent memory. Phase 5 adds **4** logical workshop
roles and **3** executable worker profiles (not four fake agent runs).
AI generation never equals concept-direction approval. Brand DNA and Color
Profile are creative constraints only; they are never factual evidence.
Only source IDs from the pinned approved research report may support
factual claims. The app never generates images, never calls image
providers/tools, and never executes arbitrary HTML/CSS/SVG/script from AI
output.

## Assumptions

1. Phases 1–4 remain accepted and unchanged in behavior except for additive
   workshop tables/pointers, agent-run columns, routes, UI surfaces, tests,
   audit action names, and prompt-pack registration.
2. A workspace may start a workshop job only when **all three** of
   `CurrentApprovedBrandDnaVersionId`,
   `CurrentApprovedColorProfileVersionId`, and
   `CurrentApprovedResearchReportVersionId` are non-null. All three ids
   (and their version numbers) are pinned onto the job and package as
   required provenance.
3. Brand DNA DocumentJson/Summary and Color Profile DocumentJson may
   constrain creative direction (voice, palette roles, geometry language)
   only. They must never appear as `sourceIds` on factual claims and must
   never be treated as research evidence.
4. Research Report `sources[].id` values from the **pinned approved**
   report are the **only** valid factual-claim source identifiers.
5. Concept package approval is **concept-direction approval only**. It is
   not research, claim, legal, matching, accessibility, compliance,
   campaign-ready, asset, QA, or production-artwork approval.
6. Safe UI may render fixed low-fi components from stored server JSON and
   the pinned color palette with escaped text only. It must not invent or
   rearrange content, execute arbitrary markup/styles, or fetch/render
   real images.
7. Bliss matching, review, placement, Alpha Auto, and n8n authority remain
   out of scope and untouched.
8. Phase 6 asset/revision department and Phase 7 Chaperone/QA are out of
   scope.

## Workforce allocation

| Item | Phase 5 allocation |
| --- | --- |
| New logical AI roles | 4 (listed below) |
| Executable profiles / successful calls | exactly 3 |
| Prompt packs | `wp-phase5.concept-strategy.v1`, `wp-phase5.concept-creative.v1`, `wp-phase5.prototype-production.v1` |
| AI provider | reuse `IWeddingPlannerAiProvider`; shared provider/model allowed; profiles remain separately versioned |
| Image / asset provider | **none** — forbidden in Phase 5 |
| Agent runs on success | exactly 3 `WeddingPlannerAgentRun` rows |
| Durable role evidence | exactly 4 immutable `ConceptRoleContribution` rows |
| Human authority | humans alone approve/reject concept packages and select a concept on APPROVE |

### Logical roles (exactly 4)

| Order | Logical role | Stage profile |
| --- | --- | --- |
| 1 | `BRAND_STRATEGIST` | `CONCEPT_STRATEGY_V1` |
| 2 | `ART_DIRECTOR` | `CONCEPT_CREATIVE_V1` |
| 3 | `COPYWRITER` | `CONCEPT_CREATIVE_V1` |
| 4 | `PRODUCTION_ARTIST` | `PROTOTYPE_PRODUCTION_V1` |

### Executable profiles (exactly 3)

| Profile key | Prompt pack | Assigned roles | Agent-run `LogicalRole` (stage) |
| --- | --- | --- | --- |
| `CONCEPT_STRATEGY_V1` | `wp-phase5.concept-strategy.v1` | role 1 | `CONCEPT_STRATEGY` |
| `CONCEPT_CREATIVE_V1` | `wp-phase5.concept-creative.v1` | roles 2–3 | `CONCEPT_CREATIVE` |
| `PROTOTYPE_PRODUCTION_V1` | `wp-phase5.prototype-production.v1` | role 4 | `PROTOTYPE_PRODUCTION` |

Do **not** create four fake agent runs. Durable authority for the four
roles is `ConceptRoleContribution` (+ `AssignedRolesJson` on each run).
Stage `LogicalRole` values exist for Phase 2-compatible run listing only.

Phase 2 Concierge / Brand DNA Interpreter, Phase 3 Color Intelligence, and
Phase 4 Curator remain as allocated. Phase 5 does not extend their tools
into asset generation, campaign-ready marking, matching writes, or QA.

## Authority

- Advertisers act only inside the `advertiser_id`-bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- Only an authenticated advertiser or chapel operator/admin may create a
  workshop job or record an `APPROVE` / `REJECT` decision.
- AI stages may propose concepts, creative direction, copy, and prototype
  specs. They cannot approve packages, select the winning concept, write
  matching/review/placement records, generate images, invent live facts
  when the local AI provider is active, or mutate Brand DNA / color /
  research documents.
- Cross-tenant resource lookup returns **404**, consistent with Phases 1–4.
- Anonymous protected reads/writes return **401** when OIDC is enabled.
- Authenticated identities without write authority (e.g. viewer) return
  **403** on job create and decision writes.

## Inputs (human brief)

`POST .../workshop-jobs` body:

| Field | Required | Rules |
| --- | --- | --- |
| `Objective` | yes | non-empty trimmed string; max length bounded (recommend 4000) |
| `CampaignGoal` | yes | planning text only — **not** a campaign id; non-empty trimmed; max length bounded (recommend 500) |
| `AudienceFocus` | yes | non-empty trimmed string; max length bounded (recommend 1000) |
| `ChannelFormat` | yes | enum exactly one of: `STATIC_SOCIAL_SQUARE`, `STATIC_SOCIAL_STORY`, `STATIC_DISPLAY_BANNER`, `EMAIL_HERO` |
| `Deliverables` | yes | array of 1–6 non-empty trimmed strings; each max length bounded |
| `Cta` | yes | non-empty trimmed string; max length bounded (recommend 200) |
| `Constraints` | yes | array of 0–12 trimmed strings (empty array allowed); each max length bounded |
| `SourceSystem` | yes | Phase 1–4 idempotency pattern |
| `IdempotencyKey` | yes | unique with `SourceSystem` for the **job** |

### Forbidden brief fields

The request body **must reject** (400) any of the following if present
(including aliases / nested objects):

- match / matching ids
- campaign ids (distinct from free-text `CampaignGoal`)
- placement ids
- inventory ids
- readiness flags (`campaignReady`, `qaApproved`, `blissReady`, etc.)
- asset / image / provider tool configuration
- HTML, CSS, SVG, script, `src`, `url`, `href`, `base64`, or image bytes

Server also binds, without client override:

- required `ApprovedBrandDnaVersionId` = workspace current approved Brand DNA;
- required `ApprovedColorProfileVersionId` = workspace current approved color
  profile;
- required `ApprovedResearchReportVersionId` = workspace current approved
  research report.

If any of the three current-approved pointers is null → **400**.

### Channel format → exact canvas

| `ChannelFormat` | Canvas width × height (px) |
| --- | --- |
| `STATIC_SOCIAL_SQUARE` | 1080 × 1080 |
| `STATIC_SOCIAL_STORY` | 1080 × 1920 |
| `STATIC_DISPLAY_BANNER` | 1200 × 628 |
| `EMAIL_HERO` | 1200 × 600 |

Canvas dimensions are server-derived from `ChannelFormat`. Clients must not
supply alternate width/height; if supplied and mismatched → **400**.

Canonical input JSON for hashing is a stable sorted-key object of the
canonical brief fields (objective, campaignGoal, audienceFocus,
channelFormat, deliverables, cta, constraints), the three approved
provenance version ids, schema version `workshop-brief.v1`, and
AI/provider contract versions used. Persist both the canonical JSON string
and its lowercase hex SHA-256 on the job. Actor labels are excluded from
the hash.

## Provider contracts

### `IWeddingPlannerAiProvider` (reuse)

Each of the three profiles calls `CompleteAsync` once with:

- stage `LogicalRole` (`CONCEPT_STRATEGY` / `CONCEPT_CREATIVE` /
  `PROTOTYPE_PRODUCTION`);
- the stage prompt pack;
- JSON response format;
- context built only from same-tenant durable brief, pinned Brand DNA
  framing summary/id (constraint), pinned Color Profile summary/palette
  roles (constraint), pinned Research Report summary/source catalog ids
  (evidence boundary), and prior stage outputs already persisted on the
  job.

Shared provider/model is allowed. Profile prompt packs remain separately
versioned. Provider failures fail the current run and job; they do not
fabricate packages.

### Local / Development AI output

When the Local deterministic AI provider is active, every stage output and
the merged package must include a conspicuous marker string:

`SYNTHETIC DEVELOPMENT PROTOTYPE`

Local outputs must never claim live market facts, live brand legal
clearance, or production-ready artwork. Dev/CI may use Local freely.

### OpenAI / remote stage instructions (strict)

Remote stage system instructions must:

1. require strict JSON matching the stage schema only;
2. forbid unknown fields;
3. forbid `html`, `css`, `svg`, `script`, `src`, `url`, `href`, `base64`,
   and image bytes in any field;
4. forbid inventing source ids not present in the pinned research report
   source catalog;
5. forbid treating Brand DNA or color profile ids as factual source ids;
6. label all marketing copy fields as creative / non-factual unless a
   factual claim object with valid source ids is explicitly supplied;
7. emit exactly the concept ids `concept_1`, `concept_2`, `concept_3` and
   no others;
8. for Production Artist, emit `prototype-spec.v1` only — no freeform
   markup.

### Production guard

When the host environment is Production (or an explicit
`RequireRemoteAiProvider` / Phase 2-equivalent flag is set), starting a
workshop job with the Local AI provider is rejected (**400/503** as
implemented consistently with Phase 2–4 production expectations).

### Forbidden providers / tools in Phase 5

- Image generation APIs
- Asset storage upload pipelines for creative binaries
- HTML/CSS render sandboxes that execute AI-authored markup
- Any tool that fetches arbitrary URLs for creative preview

## Durable records

### Extend `WeddingPlannerAgentRun`

Add nullable (if not already present from Phase 4 patterns, reuse the same
columns where compatible):

- `WorkerProfileVersion` — e.g. `CONCEPT_STRATEGY_V1`
- `AssignedRolesJson` — JSON array of the logical roles assigned to that run
- `OutputConceptPackageVersionId` — set on the final successful production
  run when a package is created; null on earlier stage runs and failures

Existing Concierge / Brand DNA / Curator runs leave
`OutputConceptPackageVersionId` null. Workshop stage `LogicalRole` uses
the three stage values above for backward-compatible listing; the four
durable role contributions remain authoritative.

Internal run idempotency keys are the job key with a stable suffix, e.g.
`{jobKey}:CONCEPT_STRATEGY`, `:CONCEPT_CREATIVE`,
`:PROTOTYPE_PRODUCTION` (exact suffix strings locked in implementation
constants). Each successful job therefore owns exactly three run rows with
tokens, cost, status, worker/prompt/provider/model/adapter evidence.

### `WeddingPlannerWorkshopJob`

Durable orchestration row (statuses `RUNNING` | `SUCCEEDED` | `FAILED`):

- advertiser/workspace scope
- canonical `InputJson`, `InputSha256`
- brief fields (`Objective`, `CampaignGoal`, `AudienceFocus`,
  `ChannelFormat`, `DeliverablesJson`, `Cta`, `ConstraintsJson`)
- required Brand DNA, Color Profile, and Research Report provenance ids
  (+ version numbers snapshot)
- three stage output JSON fields (raw/canonical per-stage worker outputs)
- `StrategyAgentRunId`, `CreativeAgentRunId`, `ProductionAgentRunId`
  (nullable until created)
- `OutputConceptPackageVersionId` (null unless SUCCEEDED)
- status, bounded error code/message, timestamps, actor metadata
- `SourceSystem`, `IdempotencyKey`

Unique `(SourceSystem, IdempotencyKey)` is the **job** idempotency
boundary. Replaying a `FAILED` job returns the failed job and **never**
retries AI. Replaying a `SUCCEEDED` job returns the existing job/package
linkage with **no** AI calls.

### `WeddingPlannerConceptPackageVersion`

Immutable snapshot:

- advertiser/workspace scope; monotonic `VersionNumber` per workspace
- `SchemaVersion` = `concept-package.v1`
- `DocumentJson`, `Summary`
- producing job id; producing production agent-run id
- Brand DNA + Color Profile + Research Report provenance ids (+ version
  numbers)
- `ChannelFormat` and exact canvas width/height
- `Status`: `PROPOSED` | `APPROVED` | `REJECTED` | `SUPERSEDED`
- source/idempotency: recommend unique `(SourceSystem, IdempotencyKey)` on
  package with `{jobKey}:PACKAGE`
- created-at / actor metadata

No PUT/PATCH of payload fields after insert. Concept selection is **not**
stored on this row.

### `WeddingPlannerConceptRoleContribution`

Exactly **4** immutable rows per successful package:

- advertiser/workspace/package/job scope
- `LogicalRole` (one of the four; unique per package)
- `ProducingAgentRunId` (the stage run that emitted that role)
- contribution payload JSON (role-owned fields only)
- created-at

No extras. No missing roles. Contributions are never edited in place.
Never create four fake agent runs to “match” these four rows.

### `WeddingPlannerConceptPackageDecision`

Immutable human decision:

- `Decision`: `APPROVE` | `REJECT`
- required non-empty rationale
- `SelectedConceptId`:
  - on `APPROVE`: **required**; must be exactly one of
    `concept_1` | `concept_2` | `concept_3` present in the package
  - on `REJECT`: **must be null / omitted**; if supplied → **400**
- actor, source/idempotency, timestamp
- Unique `(SourceSystem, IdempotencyKey)`

Selection lives **only** on this decision. Approving never mutates
`DocumentJson`, contribution rows, or prototype specs. Approval is
concept-direction only.

### Workspace pointer

Add nullable `CurrentApprovedConceptPackageVersionId` on
`WeddingPlannerWorkspace`. Approval sets it. Approving a later version
sets the prior current package to `SUPERSEDED` without mutating
`DocumentJson`. Rejected versions never become current. Only `PROPOSED`
versions accept a first decision; later attempts on non-`PROPOSED` are
**400**.

## Output schemas

### Stage ownership (normative)

| Stage | Roles | May emit | Must not emit |
| --- | --- | --- | --- |
| `CONCEPT_STRATEGY_V1` | Brand Strategist | concept ids/names/rationales for exactly `concept_1`–`concept_3`; strategy summary; audience/channel framing notes | visual direction, palette refs, copy/claims, prototype specs |
| `CONCEPT_CREATIVE_V1` | Art Director + Copywriter | for the **same** three concept ids: visual direction, palette role refs, copy (`headline`/`body`/`cta`), optional `factualClaims` | new/missing/extra concept ids; prototype region layouts; html/css |
| `PROTOTYPE_PRODUCTION_V1` | Production Artist | `prototype-spec.v1` only for the same three concept ids | new copy text (except referencing existing copy paths); new palette roles; new concept ids; image bytes |

Concept ids must remain exactly `concept_1`, `concept_2`, `concept_3`
across all stages. Renames, extras, or gaps fail closed.

### Per-stage AI output examples

#### Strategy — `concept-strategy-worker-output.v1`

```json
{
  "schemaVersion": "concept-strategy-worker-output.v1",
  "workerProfileVersion": "CONCEPT_STRATEGY_V1",
  "marker": "SYNTHETIC DEVELOPMENT PROTOTYPE",
  "contributions": [
    {
      "logicalRole": "BRAND_STRATEGIST",
      "summary": "Three directions aligned to the brief objective.",
      "concepts": [
        {
          "id": "concept_1",
          "name": "Quiet Confidence",
          "rationale": "Leads with understated brand voice for the stated audience."
        },
        {
          "id": "concept_2",
          "name": "Warm Invitation",
          "rationale": "Centers hospitality cues without inventing product facts."
        },
        {
          "id": "concept_3",
          "name": "Clear Next Step",
          "rationale": "Privileges CTA clarity for the selected channel format."
        }
      ]
    }
  ]
}
```

Rules:

- `contributions` must include exactly `BRAND_STRATEGIST` and no extras.
- Exactly three concepts with ids `concept_1`–`concept_3`.
- Local marker required when Local provider is active (may be omitted or
  mirrored by server when remote; merged package always carries the Phase 5
  disclaimer).

#### Creative — `concept-creative-worker-output.v1`

```json
{
  "schemaVersion": "concept-creative-worker-output.v1",
  "workerProfileVersion": "CONCEPT_CREATIVE_V1",
  "marker": "SYNTHETIC DEVELOPMENT PROTOTYPE",
  "contributions": [
    {
      "logicalRole": "ART_DIRECTOR",
      "summary": "Visual direction per concept.",
      "concepts": [
        {
          "id": "concept_1",
          "visualDirection": "Centered quiet hero with generous negative space.",
          "paletteRoleRefs": ["primary", "background", "accent"]
        },
        {
          "id": "concept_2",
          "visualDirection": "Split frame with soft secondary wash.",
          "paletteRoleRefs": ["primary", "secondary", "background"]
        },
        {
          "id": "concept_3",
          "visualDirection": "Banner-forward CTA block with logo slot.",
          "paletteRoleRefs": ["primary", "accent", "background"]
        }
      ]
    },
    {
      "logicalRole": "COPYWRITER",
      "summary": "Creative non-factual copy per concept.",
      "concepts": [
        {
          "id": "concept_1",
          "copy": {
            "kind": "CREATIVE_NON_FACTUAL",
            "headline": "Your day, thoughtfully planned.",
            "body": "A calm invitation to explore options together.",
            "cta": "Start planning"
          },
          "factualClaims": []
        },
        {
          "id": "concept_2",
          "copy": {
            "kind": "CREATIVE_NON_FACTUAL",
            "headline": "Warm welcomes begin here.",
            "body": "Host with confidence and clarity.",
            "cta": "See the experience"
          },
          "factualClaims": [
            {
              "statement": "Couples often research venues months ahead.",
              "sourceIds": ["src_1"]
            }
          ]
        },
        {
          "id": "concept_3",
          "copy": {
            "kind": "CREATIVE_NON_FACTUAL",
            "headline": "One clear next step.",
            "body": "Keep the path simple from glance to action.",
            "cta": "Continue"
          }
        }
      ]
    }
  ]
}
```

Rules:

- `contributions` must include exactly `ART_DIRECTOR` and `COPYWRITER`.
- Concept ids must match strategy output exactly (`concept_1`–`concept_3`).
- `copy.kind` must be exactly `CREATIVE_NON_FACTUAL`.
- `factualClaims` is optional; when present, each claim requires one or
  more nonempty `sourceIds` that exist in the pinned approved research
  report `sources` catalog.
- Brand DNA version id, color profile version id, research report version
  id, job id, package id, and provenance ids **cannot** be used as
  `sourceIds`.
- `paletteRoleRefs` must reference palette roles that exist on the pinned
  approved color profile; unknown roles fail closed.
- Unknown fields fail closed.

#### Production — `prototype-production-worker-output.v1`

```json
{
  "schemaVersion": "prototype-production-worker-output.v1",
  "workerProfileVersion": "PROTOTYPE_PRODUCTION_V1",
  "marker": "SYNTHETIC DEVELOPMENT PROTOTYPE",
  "contributions": [
    {
      "logicalRole": "PRODUCTION_ARTIST",
      "summary": "Low-fi prototype specs for three concepts.",
      "prototypes": [
        {
          "conceptId": "concept_1",
          "spec": { /* prototype-spec.v1 — see below */ }
        },
        {
          "conceptId": "concept_2",
          "spec": { /* prototype-spec.v1 */ }
        },
        {
          "conceptId": "concept_3",
          "spec": { /* prototype-spec.v1 */ }
        }
      ]
    }
  ]
}
```

Rules:

- Exactly one contribution: `PRODUCTION_ARTIST`.
- Exactly three prototypes keyed to `concept_1`–`concept_3`.
- Each `spec` must validate as `prototype-spec.v1`.
- No copy invention: text regions may only reference
  `copy.headline` / `copy.body` / `copy.cta` from the creative stage for
  that concept.

### Structured `prototype-spec.v1`

```json
{
  "schemaVersion": "prototype-spec.v1",
  "format": "STATIC_SOCIAL_SQUARE",
  "canvas": { "width": 1080, "height": 1080 },
  "template": "LOFI_STACK_V1",
  "regions": [
    {
      "id": "r1",
      "type": "HERO",
      "bounds": { "x": 0, "y": 0, "w": 1080, "h": 640 },
      "assetPlaceholder": {
        "kind": "HERO_IMAGE",
        "label": "Hero atmosphere placeholder"
      }
    },
    {
      "id": "r2",
      "type": "HEADLINE",
      "bounds": { "x": 64, "y": 680, "w": 952, "h": 96 },
      "textRef": "copy.headline",
      "paletteRoleRef": "primary"
    },
    {
      "id": "r3",
      "type": "BODY",
      "bounds": { "x": 64, "y": 792, "w": 952, "h": 120 },
      "textRef": "copy.body",
      "paletteRoleRef": "neutral800"
    },
    {
      "id": "r4",
      "type": "CTA",
      "bounds": { "x": 64, "y": 940, "w": 360, "h": 72 },
      "textRef": "copy.cta",
      "paletteRoleRef": "accent"
    },
    {
      "id": "r5",
      "type": "LOGO_SLOT",
      "bounds": { "x": 900, "y": 960, "w": 116, "h": 48 },
      "assetPlaceholder": {
        "kind": "LOGO",
        "label": "Logo placeholder"
      }
    }
  ]
}
```

#### `prototype-spec.v1` validation (fail closed)

| Rule | Requirement |
| --- | --- |
| `format` | Must equal the job brief `ChannelFormat` |
| `canvas` | Exact pixels for that format (table above); no client override |
| `template` | Exactly one of `LOFI_STACK_V1`, `LOFI_SPLIT_V1`, `LOFI_BANNER_V1` |
| `regions` | 2–12 items; unique `id`s |
| `regions[].type` | Exactly one of `HERO`, `HEADER`, `BODY`, `HEADLINE`, `SUBHEAD`, `CTA`, `FOOTER`, `LOGO_SLOT` |
| `textRef` | When present: only `copy.headline`, `copy.body`, or `copy.cta` |
| `paletteRoleRef` | When present: must exist on pinned color-profile palette |
| `assetPlaceholder.kind` | When present: `HERO_IMAGE` \| `LOGO` \| `PRODUCT` \| `DECORATIVE` |
| `assetPlaceholder.label` | Non-empty trimmed label only — no URLs, paths, or bytes |
| Bounds | `x,y ≥ 0`; `w,h > 0`; `x+w ≤ canvas.width`; `y+h ≤ canvas.height` |
| Forbidden keys anywhere | `html`, `css`, `svg`, `script`, `src`, `url`, `href`, `base64`, image bytes, unknown fields |
| Images | No image generation; no provider calls; no binary storage |

### Canonical package `concept-package.v1`

Server merges and canonicalizes after all three stages succeed:

```json
{
  "schemaVersion": "concept-package.v1",
  "disclaimer": "Approval of this package is concept-direction approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, asset, QA, or production-artwork approval. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim cites source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Prototypes are structured low-fi specs only; no images are generated.",
  "marker": "SYNTHETIC DEVELOPMENT PROTOTYPE",
  "provenance": {
    "approvedBrandDnaVersionId": "<guid>",
    "approvedBrandDnaVersionNumber": 1,
    "approvedColorProfileVersionId": "<guid>",
    "approvedColorProfileVersionNumber": 1,
    "approvedResearchReportVersionId": "<guid>",
    "approvedResearchReportVersionNumber": 1,
    "workshopJobId": "<guid>"
  },
  "brief": {
    "objective": "…",
    "campaignGoal": "…",
    "audienceFocus": "…",
    "channelFormat": "STATIC_SOCIAL_SQUARE",
    "canvas": { "width": 1080, "height": 1080 },
    "deliverables": ["…"],
    "cta": "…",
    "constraints": []
  },
  "concepts": [
    {
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
      "factualClaims": [],
      "prototype": { /* prototype-spec.v1 */ }
    },
    {
      "id": "concept_2",
      "name": "Warm Invitation",
      "rationale": "…",
      "visualDirection": "…",
      "paletteRoleRefs": ["primary", "secondary", "background"],
      "copy": {
        "kind": "CREATIVE_NON_FACTUAL",
        "headline": "…",
        "body": "…",
        "cta": "…"
      },
      "factualClaims": [
        {
          "statement": "…",
          "sourceIds": ["src_1"]
        }
      ],
      "prototype": { /* prototype-spec.v1 */ }
    },
    {
      "id": "concept_3",
      "name": "Clear Next Step",
      "rationale": "…",
      "visualDirection": "…",
      "paletteRoleRefs": ["primary", "accent", "background"],
      "copy": {
        "kind": "CREATIVE_NON_FACTUAL",
        "headline": "…",
        "body": "…",
        "cta": "…"
      },
      "factualClaims": [],
      "prototype": { /* prototype-spec.v1 */ }
    }
  ],
  "contributions": [
    { "logicalRole": "BRAND_STRATEGIST", "summary": "…" },
    { "logicalRole": "ART_DIRECTOR", "summary": "…" },
    { "logicalRole": "COPYWRITER", "summary": "…" },
    { "logicalRole": "PRODUCTION_ARTIST", "summary": "…" }
  ]
}
```

`Summary` is a short server-built string naming schema version,
channelFormat, concept count (3), and contribution count (4).

Validation fail closed if merged output:

- lacks exactly **3** concepts with ids `concept_1`–`concept_3`;
- lacks exactly **4** contributions in workforce order;
- omits the exact disclaimer string (or an exact locked constant equal in
  meaning and enumerated exclusions);
- contains dangling factual-claim source ids;
- uses Brand DNA / color / provenance ids as source ids;
- contains unknown fields or forbidden markup/media keys;
- has any prototype whose `format`/canvas diverges from the brief;
- was produced under Local AI without the conspicuous
  `SYNTHETIC DEVELOPMENT PROTOTYPE` marker (required on Local path;
  recommended retained on merged document whenever Local was used).

**No partial packages.** Merge/insert happens only after all three stages
and full validation succeed.

## Safe UI preview contract

Safe UI (public + operations) **may**:

- render fixed low-fi layout components chosen by `template`
  (`LOFI_STACK_V1` / `LOFI_SPLIT_V1` / `LOFI_BANNER_V1`);
- place regions using stored `bounds` from server JSON;
- fill text from `textRef` → concept `copy.*` with HTML-escaped plain text
  only;
- apply colors from pinned color-profile palette roles referenced by
  `paletteRoleRef`;
- show asset **placeholders** (labeled boxes) for
  `HERO_IMAGE` / `LOGO` / `PRODUCT` / `DECORATIVE`.

Safe UI **must not**:

- invent or rearrange concepts, copy, or regions beyond the stored JSON;
- execute or inject arbitrary HTML/CSS/SVG/script from AI or stored JSON;
- render `<img>` or fetch image URLs;
- treat creative copy as certified factual claims;
- imply campaign-ready, Bliss matching, or QA approval.

## Flow

1. authorize workspace write access;
2. require current-approved Brand DNA, Color Profile, and Research Report;
   else **400**;
3. validate brief (including channelFormat enum and forbidden fields);
4. replay existing job when `(SourceSystem, IdempotencyKey)` exists
   (SUCCEEDED → return existing; FAILED → return existing, no retry);
5. insert `RUNNING` job with canonical input/SHA and pinned provenance;
6. create run #1 (`CONCEPT_STRATEGY`), invoke AI, persist stage output;
7. create run #2 (`CONCEPT_CREATIVE`), invoke AI, persist stage output;
8. create run #3 (`PROTOTYPE_PRODUCTION`), invoke AI, persist stage output;
9. merge/validate `concept-package.v1`; insert `PROPOSED` package + exactly
   4 contributions; link run/package FKs; job `SUCCEEDED`;
10. audit throughout.

Any AI stage failure or validation failure: mark the **current** run
`FAILED` (if created), mark job `FAILED`, keep prior successful run
receipts and stage JSON already stored, create **no** package and **no**
contributions. No partial proposed packages.

## Failure saga (normative)

| Condition | Agent runs | Package | Job |
| --- | --- | --- | --- |
| AI stage N fails | prior SUCCEEDED kept; current `FAILED`; later not created | none | `FAILED` |
| Validation/canonicalize fails after a stage | current run `FAILED` (or failed without succeeding); priors kept | none | `FAILED` |
| Idempotent SUCCEEDED replay | unchanged (exactly 3 runs) | unchanged | return existing; **no** calls |
| Idempotent FAILED replay | unchanged | none | return existing; **never** retry |

Successful path evidence: exactly **3** agent runs with token/cost fields
populated, exactly **1** `PROPOSED` package, exactly **4** role
contributions. Provider/AI transport failures surface as **502** after
durable failure persistence when appropriate; validation/auth errors
remain 400/401/403/404.

## API

Phase 1–4 routes remain unchanged. Add under `/api/wedding-planner`:

- `POST /workspaces/{id}/workshop-jobs`
- `GET /workspaces/{id}/workshop-jobs`
- `GET /workshop-jobs/{id}`
- `GET /workspaces/{id}/concept-packages`
- `GET /concept-packages/{id}`
- `GET /concept-packages/{id}/contributions`
- `GET /concept-packages/{id}/agent-runs`
- `POST /concept-packages/{id}/decisions`
- `GET /workspaces/{id}/agent-runs` (workspace-scoped list; includes
  workshop + prior phase runs)

No image-generation endpoint. No URL-fetch endpoint for creative assets.
No PUT/PATCH/DELETE for package or contribution payloads. No endpoint that
mutates selected concept onto the package row.

### Decision body

```json
{
  "decision": "APPROVE",
  "rationale": "Direction 1 best fits the brief.",
  "selectedConceptId": "concept_1",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

```json
{
  "decision": "REJECT",
  "rationale": "None of the directions fit constraints.",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

| Decision | `selectedConceptId` |
| --- | --- |
| `APPROVE` | required; must be `concept_1` \| `concept_2` \| `concept_3` |
| `REJECT` | forbidden (null/omitted only) |

## Failure and audit

| Condition | Behavior |
| --- | --- |
| Anonymous (OIDC on) | 401 |
| Viewer / no write authority | 403 on job create and decisions |
| Missing or cross-tenant id | 404 |
| Invalid brief, forbidden fields, empty rationale, illegal status transition, missing any of the three approved prerequisites, Local AI in Production guard, APPROVE without/invalid selection, REJECT with selection | 400 (or documented 503 for misconfigured production provider) |
| Idempotent replay | existing job/decision; no second AI/package rows; audit replay |

Audit actions (append-only): workshop job started/succeeded/failed/
replayed; each workshop agent run start/success/failure/replay; concept
package proposed/approved/rejected/superseded/replayed. Events carry
actor, request id, workspace/advertiser, job/package/run ids, and on
approve the `selectedConceptId`. Secrets are never stored.

Cost evidence: persist token/cost fields on each of the three agent runs.
Package DTO/list views may surface summed estimated USD for operators
without exposing secrets.

## UI requirements

### Public (`frontend/public`)

- Show Concept Workshop only when an advertiser-capable session can access
  the workspace and all three prerequisites exist (Brand DNA, Color
  Profile, Research Report) — or clearly disable submit with prerequisites
  stated.
- Controls: brief form (objective, campaignGoal text, audienceFocus,
  channelFormat select, 1–6 deliverables, CTA, constraints), submit job,
  list jobs/packages, inspect three concepts + four contributions + linked
  agent runs, safe low-fi prototype preview, approve/reject with required
  rationale and concept selection on approve only.
- Display the concept-direction disclaimer **verbatim**.
- When Local/synthetic path was used, show `SYNTHETIC DEVELOPMENT
  PROTOTYPE` conspicuously.
- Show asset placeholders only — never `<img>`.
- Do not imply research re-approval, legal clearance, campaign-ready,
  matching changes, asset finalization, or QA.
- Preserve Phase 2–4 Concierge / Brand DNA / Color / Curator surfaces.

### Operations (`frontend/operations`)

- List workshop jobs, packages, contributions, decisions, and agent runs
  for the selected workspace.
- Expose job create and approve/reject for operator/admin.
- Show provider/profile/prompt versions, input SHA-256, all three
  provenance ids/version numbers, costs/tokens, selectedConceptId on
  decisions, and current-approved marker.
- Local/synthetic packages must remain visually conspicuous in Dev.

## Tests (exhaustive minimum)

**Brief / prerequisites**

- Job requires approved Brand DNA, Color Profile, and Research Report;
  missing any → 400.
- ChannelFormat enum accepted; unknown format → 400.
- Canvas derived exactly per format table; mismatched client canvas → 400.
- Forbidden match/campaign/placement/inventory ids and readiness flags →
  400.
- Deliverables outside 1–6 → 400; empty required strings → 400.

**Schema / merge**

- Strategy accepts only Brand Strategist; extras/missing fail.
- Creative accepts only Art Director + Copywriter; concept ids must match
  strategy.
- Production accepts only Production Artist; prototype-spec only.
- Exactly `concept_1`–`concept_3`; extras/gaps fail.
- `copy.kind` must be `CREATIVE_NON_FACTUAL`.
- factualClaims with unknown/empty/provenance source ids fail.
- paletteRoleRefs unknown to pinned color profile fail.
- prototype-spec: format/canvas/template/region count/types/bounds/
  textRef/palette/assetPlaceholder rules; forbidden keys fail.
- Merge requires exactly 3 concepts, 4 contributions, and the exact
  disclaimer.
- Local path requires conspicuous `SYNTHETIC DEVELOPMENT PROTOTYPE`.

**API / persistence / saga**

- Success path: 1 job SUCCEEDED, 3 agent runs SUCCEEDED with token/cost
  evidence, 1 PROPOSED package, 4 contributions; never 4 agent runs.
- Mid-stage AI fail → current run FAILED, priors kept, no package.
- Validation fail → no partial proposed package.
- SUCCEEDED replay → no AI calls; same ids.
- FAILED replay → no retry; same failed job.
- APPROVE requires valid `selectedConceptId`; sets pointer; selection
  stored on decision only; package DocumentJson unchanged.
- Later APPROVE supersedes prior current without payload mutation.
- REJECT forbids selection; never sets pointer; empty rationale 400;
  non-PROPOSED decision 400.
- Advertiser A cannot access B's jobs/packages/contributions/runs (404).
- Anonymous 401; viewer 403 on writes.
- Production guard rejects Local AI when enabled.

**Safe UI / architecture**

- Preview renderer uses fixed templates + escaped text + placeholders;
  does not emit `<img>` or execute arbitrary HTML/CSS.
- Existing Bliss matching/review/placement and Wedding Planner Phase 1–4
  tests remain green.
- Architecture tests prove Phase 5 workshop sources do not reference
  deterministic match evaluation, Alpha Auto, or n8n authority; do not
  implement image generation/providers; do not call asset upload
  pipelines; and do not treat Brand DNA/color ids as factual sources.

## Exact disclaimer (locked)

The following string must appear verbatim in `concept-package.v1`
`DocumentJson.disclaimer`:

> Approval of this package is concept-direction approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, asset, QA, or production-artwork approval. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim cites source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Prototypes are structured low-fi specs only; no images are generated.

## Explicit exclusions

- Phase 6 asset / revision / mature creative department
- Phase 7 Chaperone / QA AI / escalation
- Campaign-ready state and Bliss handshake
- Matching, review, or placement changes
- Measurement / learning
- Alpha Auto
- n8n authority / GHL coupling
- Image generation, image providers, asset binary pipelines
- App-side URL fetch for creative media
- Executing AI-authored HTML/CSS/SVG/script
- Treating Brand DNA or color profiles as factual evidence
- Four fake per-role agent runs
- Mutating selected concept onto the package document
- Research report mutation or re-approval via workshop
- Legal / compliance / accessibility certification products

## Known residual risks (accepted for Phase 5)

1. **Claim risk:** Models may still phrase creative copy like a fact even
   when `copy.kind` is `CREATIVE_NON_FACTUAL`. Mitigation: disclaimer,
   optional `factualClaims` gate with pinned research source ids only,
   UI labeling of creative vs cited claims. Residual: humans can still
   misread tone as certified fact.
2. **Preview risk:** Low-fi placeholder rendering can be mistaken for
   finished creative. Mitigation: placeholders only (no `<img>`),
   template-bound layout, Local `SYNTHETIC DEVELOPMENT PROTOTYPE` marker,
   concept-direction-only disclaimer. Residual: screenshots of the
   preview may still be forwarded out of context.
3. **Source-id honesty risk:** Valid source ids prove the id exists in the
   pinned report catalog; they do not re-verify live URL content (Phase 4
   still does not fetch citations). Residual: cited statements can be
   wrong even with a valid id.
4. **Selection vs package drift risk (process):** Humans approve a
   direction id without mutating the package; later readers must join
   decision → selectedConceptId. Residual: UI that fails to surface the
   decision selection could show all three concepts as equally current.

These residuals do not expand Phase 5 scope into QA, legal review, or
asset production.

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 5 does not auto-advance to Phase 6. Phases 1–4 behavior
and all Bliss matching boundaries remain preserved.
