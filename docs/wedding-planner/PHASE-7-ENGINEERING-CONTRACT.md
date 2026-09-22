# Wedding Planner Phase 7 — Engineering Contract

Status: authorized for implementation by the user's request to proceed to
Phase 7.

## Objective

Add the **Chaperone / QA / Escalation Control Department** as Wedding
Planner control-review software:

1. an authenticated human submits a QA review brief for an authorized
   workspace that already has a current-approved Phase 6 creative package
   whose latest `APPROVE` decision carries a `SelectedVariantId`, and
   that selected variant has exactly one selected PNG asset;
2. the server runs authoritative deterministic `qa-rules.v1` **before**
   any AI call and records PASS / WARN / BLOCK findings;
3. **exactly two** executable AI profiles synthesize **exactly two** AI
   role contributions (`CREATIVE_CHAPERONE`, `QA_INSPECTOR`) for **only**
   the pinned selected variant;
4. the server deterministically materializes the **third** logical control
   role contribution (`HUMAN_ESCALATION_STEWARD`) from versioned rules
   routing + required human authority metadata — **never** from an AI
   profile or agent run; and
5. only a human `ACCEPT` / `RETURN_FOR_REVISION` / `ESCALATE` decision
   (plus immutable escalation resolution) may change
   current-accepted QA-report pointer metadata. Escalation create/resolve
   authority is human-only.

PostgreSQL remains permanent memory. Phase 7 adds **exactly 3 NEW**
logical control roles locked onto **exactly 2** AI calls/runs **plus**
deterministic rules and humans (not three fake agent runs). Phase 6's
thirteen creative contributions, package document, assets, and APPROVE
decision remain **pinned provenance** and are **not** re-run or
re-authored. AI control roles never receive image bytes and cannot
approve, waive, create, or resolve escalations. Deterministic rules check
package / decision / variant / asset integrity / provenance only; they do
**not** certify semantic truth, visual safety, legal clearance, or
campaign readiness. Human Escalation Steward is a **rules-first human
authority**, not an AI worker.

### Inconsistencies resolved (design corrections)

| Prior / ambiguous design | Locked Phase 7 correction |
| --- | --- |
| Blueprint role 9 ("dedicated model optional") could be read as a third AI | **No AI profile/run** for `HUMAN_ESCALATION_STEWARD`. Source = `RULES_HUMAN` only. |
| "~1–2 AI plus rules/humans" could be read as variable | Locked to **exactly 2** AI profiles/calls/runs and **exactly 3** durable contributions on success. |
| Impersonating Steward via a third AI contribution | Forbidden. Server creates Steward contribution from deterministic routing; `ProducingAgentRunId` is **null** only for Steward. |
| AI proposing escalation as authority | AI may recommend risks / `HUMAN_ESCALATION` as a **proposedOutcome** only. AI cannot create `QaEscalationCase` or resolve it. |
| Visual AI / OCR / pixel inspection | Forbidden. AI receives structured text + asset **metadata** only. Humans must view same-origin PNG and attest visual review. |
| Rules vs AI severity conflict | Rules severity is authoritative. AI **cannot** downgrade rules severity. Overall = BLOCK > WARN > PASS. |

Blueprint Phase 7 starting allocation (“3 control on ~1–2 AI plus
rules/humans”) and blueprint roles 7–9 (Creative Chaperone, QA Inspector,
Human Escalation Steward) are hereby locked for V1 as above.

## Assumptions

1. Phases 1–6 remain accepted and unchanged in behavior except for
   additive QA tables/pointers, agent-run columns, routes, UI surfaces,
   tests, audit action names, prompt-pack registration, and the Phase 7
   review-policy / escalation surfaces documented below. Phase 6
   same-origin asset-content streaming remains the **only** way humans
   view selected PNG bytes.
2. A workspace may start a QA review job only when
   `CurrentApprovedCreativePackageVersionId` is non-null **and** the
   latest `APPROVE` decision for that package carries a valid
   `SelectedVariantId` present in that package **and** that variant has
   exactly one linked PNG `CreativeAsset`. The approved package id +
   DocumentJson SHA, decision id, selected variant id, asset id/hash/meta,
   and the package's pinned selected concept + Brand DNA / Color Profile /
   Research Report provenance are pinned onto the job and QA report as
   required provenance.
3. Phase 6's thirteen role contributions, creative package
   `DocumentJson`, assets, and APPROVE decision remain immutable
   provenance. Phase 7 never re-invokes Phase 6 profiles, never rewrites
   creative contributions/assets, and never re-approves the creative
   package.
4. QA review is **control review only**. It is not research, claim,
   legal, matching, accessibility, compliance, campaign-ready, final
   production-artwork, or Bliss handshake approval.
5. AI stages receive structured package/variant text, asset metadata, and
   rules findings only — **never** image bytes, base64, pixel arrays, or
   asset content URLs. The UI may render `<img>` **only** against the
   existing same-origin authenticated
   `GET /creative-assets/{id}/content` endpoint for human visual review.
6. Bliss matching, review, placement, Alpha Auto, and n8n authority
   remain out of scope and untouched.
7. Phase 8 handshake / campaign-ready is out of scope. Phase 7 does not
   auto-advance to Phase 8. Phase 8 may independently distinguish clean
   `ACCEPTED` from `ACCEPTED_WITH_EXCEPTION`.

## Workforce allocation

| Item | Phase 7 allocation |
| --- | --- |
| New logical control roles | **exactly 3** (listed below) |
| Executable AI profiles / successful calls | **exactly 2** |
| Prompt packs | `wp-phase7.chaperone-review.v1`, `wp-phase7.qa-inspection.v1` |
| AI provider | reuse `IWeddingPlannerAiProvider`; shared provider/model allowed; profiles remain separately versioned |
| Deterministic rules engine | `qa-rules.v1` — **not** an AI worker/run |
| Human Escalation Steward | rules-first human authority — **not** an AI worker/run |
| Agent runs on success | exactly 2 `WeddingPlannerAgentRun` rows |
| Durable role evidence | exactly 3 immutable `QaRoleContribution` rows |
| Contribution sources | `AI` (roles 1–2) \| `RULES_HUMAN` (role 3) |
| Phase 6 roles / runs | remain pinned creative provenance; **0** Phase 6 re-runs |
| Human authority | humans alone ACCEPT / RETURN_FOR_REVISION / ESCALATE and resolve escalation cases |

### Logical roles (exactly 3 NEW)

| Order | Logical role | Realization |
| --- | --- | --- |
| 1 | `CREATIVE_CHAPERONE` | AI profile `CHAPERONE_REVIEW_V1` |
| 2 | `QA_INSPECTOR` | AI profile `QA_INSPECTION_V1` |
| 3 | `HUMAN_ESCALATION_STEWARD` | `RULES_HUMAN` — versioned deterministic routing + reviewer/operator decisions/resolutions; **no** AI profile |

### Executable profiles (exactly 2)

| Profile key | Prompt pack | Assigned roles | Agent-run `LogicalRole` (stage) |
| --- | --- | --- | --- |
| `CHAPERONE_REVIEW_V1` | `wp-phase7.chaperone-review.v1` | role 1 | `CHAPERONE_REVIEW` |
| `QA_INSPECTION_V1` | `wp-phase7.qa-inspection.v1` | role 2 | `QA_INSPECTION` |

Do **not** create a third fake agent run for the Steward. Durable
authority for the three roles is `QaRoleContribution` (+
`AssignedRolesJson` on each AI run; Steward has `ContributionSource =
RULES_HUMAN` and `ProducingAgentRunId = null`). Stage `LogicalRole`
values exist for Phase 2-compatible run listing only.

Deterministic `qa-rules.v1` execution before AI and server materialization
of the Steward contribution are **not** AI workers and do **not** create
agent-run rows.

Phase 2 Concierge / Brand DNA Interpreter, Phase 3 Color Intelligence,
Phase 4 Curator, Phase 5 Concept Workshop, and Phase 6 Mature Creative
remain as allocated. Phase 7 does not extend their tools into
campaign-ready marking, matching writes, legal certification, or Phase 8
handshake. Phase 6 profiles are never invoked by Phase 7 jobs.

## Authority

- Advertisers act only inside the `advertiser_id`-bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- Advertiser or operator/admin may **request** a QA review job for their
  own authorized workspace and may **read** own jobs/reports/cases.
- Advertisers **cannot** record QA decisions or escalation resolutions.
- Reviewer / operator / admin may record `ACCEPT` /
  `RETURN_FOR_REVISION` / `ESCALATE` under the dedicated Phase 7 review
  policy (below). Do **not** widen generic WeddingPlanner `CanWrite` to
  grant advertisers decision authority.
- Escalation resolution `RETURN_FOR_REVISION` may be recorded by
  reviewer / operator / admin. `WAIVE_AND_ACCEPT` is **operator/admin
  only**.
- AI stages may propose boundary/provenance/brand/claim risk notes,
  deterministic-alignment notes, uncertainties, and a
  `proposedOutcome`. They cannot accept reports, waive blockers, create
  or resolve escalation cases, write matching/review/placement records,
  invent live facts when Local AI is active, mutate Brand DNA / color /
  research / concept / creative-package documents, or receive/emit image
  bytes.
- Deterministic rules may emit PASS / WARN / BLOCK findings and Steward
  routing metadata. They cannot certify semantics, visual safety, legal
  clearance, accessibility, or campaign readiness.
- Cross-tenant resource lookup returns **404**, consistent with
  Phases 1–6.
- Anonymous protected reads/writes return **401** when OIDC is enabled.
- Authenticated identities without review-write authority return **403**
  on decision / resolution writes (advertisers included for those
  writes). Job create by advertiser remains allowed for own workspace.

### Dedicated review policy (normative)

| Action | Advertiser | Reviewer | Operator | Admin | Viewer |
| --- | --- | --- | --- | --- | --- |
| Create QA review job (own workspace / assigned review scope) | yes | yes | yes | yes | no |
| Read own jobs/reports/cases | yes | yes | yes | yes | read-only if granted |
| `ACCEPT` / `RETURN_FOR_REVISION` / `ESCALATE` | **no** | yes | yes | yes | no |
| Resolve `RETURN_FOR_REVISION` | **no** | yes | yes | yes | no |
| Resolve `WAIVE_AND_ACCEPT` | **no** | **no** | yes | yes | no |

Reviewer create/read authority is limited to Phase 7 QA routes under the
dedicated review policy. It does **not** widen generic Wedding Planner
write authority or permit reviewer writes to Phase 1–6 resources.

## Inputs (human review brief)

`POST .../qa-review-jobs` body:

| Field | Required | Rules |
| --- | --- | --- |
| `ReviewObjective` | yes | non-empty trimmed string; max length **4000** |
| `FocusAreas` | yes | array of **1–6 unique** values drawn only from: `COPY`, `VISUAL`, `PROVENANCE`, `CLAIMS`, `FORMAT`, `ASSET_INTEGRITY`; duplicates → **400**; unknown → **400** |
| `Notes` | no | optional trimmed string; max length **4000** when present; empty/omitted OK |
| `SourceSystem` | yes | Phase 1–6 idempotency pattern |
| `IdempotencyKey` | yes | unique with `SourceSystem` for the **job** |

### Forbidden brief fields

The request body **must reject** (400) any of the following if present
(including aliases / nested objects):

- package / variant / asset override ids (`creativePackageVersionId`,
  `selectedVariantId`, `creativeAssetId`, etc.)
- match / matching ids
- campaign ids
- placement ids
- inventory ids
- readiness / QA / legal flags (`campaignReady`, `qaApproved`,
  `blissReady`, `legalCleared`, `qaReady`, etc.)
- provider configuration (AI endpoint/keys/models)
- HTML, CSS, SVG, script, `src`, `url`, `href`, `base64`, image bytes, or
  markup

Server also binds, without client override:

- required `ApprovedCreativePackageVersionId` = workspace
  `CurrentApprovedCreativePackageVersionId`;
- required package `DocumentJson` SHA-256 (recomputed/stored lowercase
  hex at pin time);
- required `CreativePackageDecisionId` = latest `APPROVE` decision for
  that package;
- required `SelectedVariantId` = that decision's `SelectedVariantId`;
- required `SelectedCreativeAssetId` + hash/meta from the exactly-one
  asset linked to that variant;
- required selected concept id and Brand DNA / Color Profile / Research
  Report provenance ids (+ version numbers) copied from the pinned
  creative package pins (not re-derived from possibly-drifted workspace
  pointers alone — creative package pins win).

If current-approved creative package is null, or no latest APPROVE
decision with valid `SelectedVariantId` exists, or selected variant lacks
exactly one PNG asset → **400**.

Review scope is **selected variant only**. Sibling variants in the
package are provenance context at most (ids/count) and must not be
treated as under review.

Canonical input JSON for hashing is a stable sorted-key object of the
canonical brief fields (`reviewObjective`, `focusAreas`, optional
`notes`), the pinned creative package id + DocumentJson SHA, decision
id, `selectedVariantId`, selected asset id + sha256, selected concept id,
the three DNA/color/research provenance version ids from the creative
package pins, schema version `qa-review-brief.v1`, and AI/`qa-rules`
contract versions used. Persist both the canonical JSON string and its
lowercase hex SHA-256 on the job. Actor labels are excluded from the
hash.

## Deterministic rules — `qa-rules.v1` (authoritative, pre-AI)

Rules run **before** either AI profile. Findings are durable on the job
and are inputs to both AI stages and Steward routing. Overall severity =
**BLOCK > WARN > PASS**. AI **cannot** downgrade rules severity.

### Finding severity

| Severity | Meaning |
| --- | --- |
| `PASS` | Check satisfied |
| `WARN` | Non-blocking caution; human must still review |
| `BLOCK` | Hard failure for ACCEPT path; cannot yield AI `PASS_RECOMMENDED` |

### Exact codes (locked)

| Code | Severity when failing / applicable | What it asserts |
| --- | --- | --- |
| `QA_CURRENT_PACKAGE_PIN` | **BLOCK** | Workspace current-approved creative package pin matches job pin and package exists |
| `QA_CURRENT_APPROVE_DECISION` | **BLOCK** | Latest APPROVE decision for pinned package exists; `SelectedVariantId` matches pin |
| `QA_SCHEMA_VERSION` | **BLOCK** | Package `schemaVersion` is exactly `creative-package.v1` |
| `QA_DISCLAIMER_EXACT` | **BLOCK** | Package disclaimer string equals Phase 6 locked disclaimer verbatim |
| `QA_PROVENANCE_CHAIN` | **BLOCK** | Package pins for concept + DNA/color/research are present and consistent with job pins |
| `QA_CONTRIBUTION_COUNT_13` | **BLOCK** | Exactly 13 `CreativeRoleContribution` rows for package |
| `QA_CREATIVE_RUNS_6` | **BLOCK** | Exactly 6 successful Phase 6 agent runs linked to the producing creative job |
| `QA_VARIANT_REFS` | **BLOCK** | Selected variant id exists; refs/canvas/format consistent with package JSON |
| `QA_CLAIM_PRESERVATION` | **BLOCK** | Selected-variant `factualClaims` (if any) preserve exact statement + source IDs from pinned selected concept boundary |
| `QA_SELECTED_ASSET_META` | **BLOCK** | Exactly one selected asset; contentType/size/dims/sha meta match package asset refs |
| `QA_PNG_REVALIDATE` | **BLOCK** | Revalidate **stored** PNG bytes with Phase 6 PNG validator; recompute hash/size/dims and match stored + package refs — **do not** fetch or provider-regenerate |
| `QA_FORBIDDEN_MARKUP_MEDIA` | **BLOCK** | Package/selected-variant structured text contains no forbidden markup/media keys / URLs / base64 / bytes |
| `QA_COPY_NON_EMPTY` | **BLOCK** | Selected-variant headline/body/cta non-empty trimmed |
| `QA_COPY_LENGTH_WARN` | **WARN** when applicable | Selected-variant headline > **120**, body > **800**, or CTA > **40** characters (warn only) |
| `QA_LOCAL_SYNTHETIC_MARKER` | **WARN** when applicable | Local/synthetic creative package marker `SYNTHETIC DEVELOPMENT CREATIVE PACKAGE` is present (expected on Local creative path) |

Rules **cannot** certify: semantic truth of copy, visual safety of pixels,
legal/compliance/accessibility clearance, or campaign readiness. They
must not invent such certifications in finding messages.

Rules findings shape (illustrative):

```json
{
  "schemaVersion": "qa-rules.v1",
  "overallSeverity": "PASS",
  "findings": [
    {
      "code": "QA_CURRENT_PACKAGE_PIN",
      "severity": "PASS",
      "message": "Current-approved creative package pin matches job pin."
    }
  ]
}
```

`overallSeverity` is the max severity across findings (BLOCK > WARN >
PASS). Empty findings array is forbidden — every listed code that applies
must appear; WARN codes appear with PASS when not applicable **or** are
omitted only when the applicability gate is false (implementation must
document which WARN codes are conditional). BLOCK codes always appear.

## Provider contracts

### `IWeddingPlannerAiProvider` (reuse)

Each of the two profiles calls `CompleteAsync` once with:

- stage `LogicalRole` (`CHAPERONE_REVIEW` / `QA_INSPECTION`);
- the stage prompt pack;
- JSON response format;
- context built only from same-tenant durable brief, pinned creative
  package **structured text** (selected variant + package summary /
  provenance / contribution summaries — not sibling variant full bodies
  unless needed for count/provenance), selected asset **metadata only**
  (id, contentType, byteSize, sha256, width, height — **never** bytes /
  base64 / content URL / pixels), `qa-rules.v1` findings, and prior
  Phase 7 stage output already persisted on the job (QA stage may see
  chaperone output).

Shared provider/model is allowed. Profile prompt packs remain separately
versioned. Provider failures fail the current run and job; they do not
fabricate reports or create Steward contributions / escalation cases.

### Local / Development AI output

When the Local deterministic AI provider is active, every stage output and
the merged report must include a conspicuous marker string:

`SYNTHETIC DEVELOPMENT QA REVIEW`

Local outputs must never claim live market facts, live brand legal
clearance, campaign-ready status, visual pixel certification, or final
production artwork. Dev/CI may use Local freely.

### OpenAI / remote stage instructions (strict)

Remote stage system instructions must:

1. require strict JSON matching the stage schema only;
2. forbid unknown fields;
3. forbid `html`, `css`, `svg`, `script`, `src`, `url`, `href`, `base64`,
   image bytes, pixel arrays, and any request for image content;
4. forbid approving, waiving, creating/resolving escalations, or claiming
   human-steward authority;
5. forbid downgrading `qa-rules.v1` severity or contradicting BLOCK as
   if it were PASS;
6. review **only** the pinned `SelectedVariantId` — never select a
   different variant or override package pins;
7. for Chaperone: emit boundary / provenance / brand / claim **risk
   notes** only — no approval claims and no pixel/visual-certification
   claims;
8. for QA Inspector: emit deterministic-alignment / format / copy /
   asset-metadata notes, uncertainties, and
   `proposedOutcome` ∈ `PASS_RECOMMENDED` | `RETURN_FOR_REVISION` |
   `HUMAN_ESCALATION`; when rules `overallSeverity` is `BLOCK`,
   `proposedOutcome` **must not** be `PASS_RECOMMENDED`;
9. never emit a `HUMAN_ESCALATION_STEWARD` contribution or third worker
   profile output.

### Production guard (AI)

When the host environment is Production (or an explicit
`RequireRemoteAiProvider` / Phase 2-equivalent flag is set), starting a
QA review job with the Local AI provider is rejected (**400/503** as
implemented consistently with Phase 2–6 production expectations).

### Forbidden providers / tools in Phase 7

- Sending image bytes / base64 / content URLs / pixels to AI
- Visual AI, OCR, embedding, or pixel-diff providers
- App-side arbitrary URL fetch for creative media
- HTML/CSS/SVG render sandboxes that execute AI-authored markup
- Treating Local AI as Production-safe without the remote requirement
  flag
- A third AI profile impersonating Human Escalation Steward

## Durable records

### Extend `WeddingPlannerAgentRun`

Add nullable (reuse Phase 4–6 patterns where compatible):

- `WorkerProfileVersion` — e.g. `CHAPERONE_REVIEW_V1` (already used by
  Phase 6; reuse column)
- `AssignedRolesJson` — JSON array of the logical roles assigned to that
  run (reuse)
- `OutputQaReviewReportVersionId` — set on the final successful QA
  inspection run when a report is created; null on earlier stage runs,
  failures, and all pre-Phase-7 runs

Existing Concierge / Brand DNA / Curator / Workshop / Creative-
production runs leave `OutputQaReviewReportVersionId` null. QA stage
`LogicalRole` uses the two stage values above for backward-compatible
listing; the three durable role contributions remain authoritative.

Internal run idempotency keys are the job key with a stable suffix, e.g.
`{jobKey}:CHAPERONE_REVIEW`, `:QA_INSPECTION` (exact suffix strings
locked in implementation constants). Each successful job therefore owns
exactly **two** run rows with tokens, cost, status,
worker/prompt/provider/model/adapter evidence. **No** Steward run row.

### `WeddingPlannerQaReviewJob`

Statuses `RUNNING` | `SUCCEEDED` | `FAILED`:

- advertiser/workspace scope
- canonical `InputJson`, `InputSha256`
- brief fields (`ReviewObjective`, `FocusAreasJson`, optional `Notes`)
- pinned `ApprovedCreativePackageVersionId`, package DocumentJson
  SHA-256, `CreativePackageDecisionId`, `SelectedVariantId`,
  `SelectedCreativeAssetId` (+ asset sha256/meta snapshot)
- pinned selected concept id + Brand DNA / Color Profile / Research
  Report provenance ids (+ version numbers from creative package pins)
- `RulesFindingsJson` (`qa-rules.v1`) — required before AI; persisted
  even if later AI fails
- two stage output JSON fields (raw/canonical per-stage worker outputs)
- two agent-run FKs (nullable until created):
  `ChaperoneReviewAgentRunId`, `QaInspectionAgentRunId`
- `OutputQaReviewReportVersionId` (null unless SUCCEEDED)
- status, bounded error code/message, timestamps, actor metadata
- `SourceSystem`, `IdempotencyKey`

Unique `(SourceSystem, IdempotencyKey)` is the **job** idempotency
boundary. Replaying a `FAILED` job returns the failed job and **never**
retries rules or AI. Replaying a `SUCCEEDED` job returns the existing
job/report linkage with **no** rules re-run and **no** AI calls.

### `WeddingPlannerQaReviewReportVersion`

Immutable snapshot:

- advertiser/workspace scope; monotonic `VersionNumber` per workspace
- `SchemaVersion` = `qa-review-report.v1`
- `DocumentJson`, `Summary`
- producing job id; producing QA-inspection agent-run id (chaperone run
  also linked via contributions)
- all pins listed on the job
- `Status`: `PROPOSED` | `ACCEPTED` | `RETURNED_FOR_REVISION` |
  `ESCALATED` | `ACCEPTED_WITH_EXCEPTION`
- source/idempotency: unique `(SourceSystem, IdempotencyKey)` on
  report with `{jobKey}:REPORT`
- created-at / actor metadata

No PUT/PATCH of payload fields after insert. Report JSON references
package / decision / variant / asset **ids and metadata only** — never
bytes or content URLs.

### `WeddingPlannerQaRoleContribution`

Exactly **3** immutable rows per successful report:

| Logical role | `ContributionSource` | `ProducingAgentRunId` |
| --- | --- | --- |
| `CREATIVE_CHAPERONE` | `AI` | required (chaperone run) |
| `QA_INSPECTOR` | `AI` | required (QA inspection run) |
| `HUMAN_ESCALATION_STEWARD` | `RULES_HUMAN` | **must be null** |

`ContributionSource` enum (locked): `AI` | `RULES_HUMAN`.

- advertiser/workspace/report/job scope
- `LogicalRole` (one of the three; unique per report)
- contribution payload JSON (role-owned fields only)
- created-at

No extras. No missing roles. Contributions are never edited in place.
Never create three fake agent runs to “match” these three rows.
`ProducingAgentRunId` is nullable **only** for Steward; AI contributions
with null run id fail closed.

### `WeddingPlannerQaReviewDecision`

Immutable human decision:

- `Decision`: `ACCEPT` | `RETURN_FOR_REVISION` | `ESCALATE`
- required non-empty rationale
- required exact `SelectedVariantId` echo matching the report/job pin
- required confirmation booleans (all must be `true` on `ACCEPT`):
  - `VisualReviewConfirmed`
  - `CopyReviewConfirmed`
  - `ProvenanceReviewConfirmed`
- `SyntheticMarkerAcknowledged`: required `true` on `ACCEPT` only when
  the report carries Local marker `SYNTHETIC DEVELOPMENT QA REVIEW`;
  for non-Local reports it may be null/false and is not an acceptance
  prerequisite
- on `ESCALATE`: required `EscalationCategory` ∈
  `VISUAL_UNCERTAINTY` | `CLAIM_BOUNDARY` | `PROVENANCE` |
  `ASSET_INTEGRITY` | `COPY_QUALITY` | `POLICY_OTHER`
- actor, source/idempotency, timestamp
- Unique `(SourceSystem, IdempotencyKey)`

Decisions never mutate `DocumentJson` or contribution rows.

### `WeddingPlannerQaEscalationCase`

Immutable-open case created only by human `ESCALATE` decision:

- advertiser/workspace/report/decision scope
- `Category` (from decision)
- `Status`: `OPEN` | `RESOLVED`
- rationale snapshot, pins snapshot
- created-at / actor
- source/idempotency unique

AI cannot insert this row. Rules cannot insert this row. Only the
decision write path creates it when `Decision = ESCALATE`.

### `WeddingPlannerQaEscalationResolution`

Immutable resolution (one successful resolution per case):

- case/report scope
- `Resolution`: `RETURN_FOR_REVISION` | `WAIVE_AND_ACCEPT`
- required non-empty rationale
- on `WAIVE_AND_ACCEPT`:
  - operator/admin only
  - required explicit exception rationale
  - required acknowledgment boolean(s)
  - required exact set of **all** rules blocker codes present on the
    report acknowledged (array must match exactly the BLOCK finding
    codes; extras/missing → **400**)
- actor, source/idempotency, timestamp
- Unique `(SourceSystem, IdempotencyKey)`

On success: case `OPEN` → `RESOLVED`; report status becomes
`RETURNED_FOR_REVISION` or `ACCEPTED_WITH_EXCEPTION` accordingly;
`WAIVE_AND_ACCEPT` sets workspace
`CurrentAcceptedQaReviewReportVersionId`.

### Workspace pointer

Add nullable `CurrentAcceptedQaReviewReportVersionId` on
`WeddingPlannerWorkspace`.

| Event | Pointer behavior |
| --- | --- |
| `ACCEPT` | set to this report; report → `ACCEPTED` |
| `RETURN_FOR_REVISION` | unset / leave unset; report → `RETURNED_FOR_REVISION` |
| `ESCALATE` | unset / leave unset; report → `ESCALATED`; create OPEN case |
| `WAIVE_AND_ACCEPT` resolution | set to this report; report → `ACCEPTED_WITH_EXCEPTION` |
| Later Phase 6 creative `APPROVE` | **clear QA pointer only**; historical accepted report status/document/contributions **unchanged** |
| New QA `ACCEPT` / waive | moves pointer to newer report; **does not** rewrite old report |

Only `PROPOSED` reports accept a first decision; later attempts on
non-`PROPOSED` are **400**. Only `OPEN` cases accept a first resolution;
later attempts are **400**.

## Output schemas

### Stage ownership (normative)

| Stage | Role | May emit | Must not emit |
| --- | --- | --- | --- |
| `CHAPERONE_REVIEW_V1` | Creative Chaperone | boundary / provenance / brand / claim risk notes; uncertainties | approval claims; pixel/visual certification; waive/accept; escalation case create; Steward contribution; image bytes |
| `QA_INSPECTION_V1` | QA Inspector | deterministic alignment / format / copy / asset-metadata notes; uncertainties; `proposedOutcome` | downgrade of rules severity; `PASS_RECOMMENDED` when rules BLOCK; Steward contribution; image bytes; escalation case create/resolve |

### Chaperone — `chaperone-review-worker-output.v1`

Contribution role: `CREATIVE_CHAPERONE`.

```json
{
  "schemaVersion": "chaperone-review-worker-output.v1",
  "workerProfileVersion": "CHAPERONE_REVIEW_V1",
  "marker": "SYNTHETIC DEVELOPMENT QA REVIEW",
  "selectedVariantId": "variant_1",
  "contributions": [
    {
      "logicalRole": "CREATIVE_CHAPERONE",
      "summary": "Boundary and provenance risk notes for the selected variant.",
      "riskNotes": {
        "boundary": ["…"],
        "provenance": ["…"],
        "brand": ["…"],
        "claims": ["…"]
      },
      "uncertainties": ["Human must visually review the selected PNG."]
    }
  ]
}
```

Rules:

- `contributions` must include exactly `CREATIVE_CHAPERONE`.
- `selectedVariantId` must equal the job pin.
- No approval language, no pixel-pass claims, no Steward role.

### QA inspection — `qa-inspection-worker-output.v1`

Contribution role: `QA_INSPECTOR`.

```json
{
  "schemaVersion": "qa-inspection-worker-output.v1",
  "workerProfileVersion": "QA_INSPECTION_V1",
  "marker": "SYNTHETIC DEVELOPMENT QA REVIEW",
  "selectedVariantId": "variant_1",
  "rulesOverallSeverity": "PASS",
  "contributions": [
    {
      "logicalRole": "QA_INSPECTOR",
      "summary": "Deterministic alignment and format notes.",
      "alignmentNotes": {
        "format": ["…"],
        "copy": ["…"],
        "assetMetadata": ["…"]
      },
      "uncertainties": ["…"],
      "proposedOutcome": "PASS_RECOMMENDED"
    }
  ]
}
```

Rules:

- Exactly one contribution: `QA_INSPECTOR`.
- `proposedOutcome` ∈ `PASS_RECOMMENDED` | `RETURN_FOR_REVISION` |
  `HUMAN_ESCALATION`.
- When persisted rules `overallSeverity` is `BLOCK`,
  `proposedOutcome` **must not** be `PASS_RECOMMENDED` (fail closed).
- `rulesOverallSeverity` must echo the durable rules overall severity;
  mismatch → fail closed.
- No Steward contribution. No escalation case mutation.

### Steward contribution payload (server-built, `RULES_HUMAN`)

Server creates the third contribution after both AI stages succeed (or,
if product chooses fail-closed earlier on rules BLOCK before AI — see
flow). Steward payload is deterministic:

```json
{
  "logicalRole": "HUMAN_ESCALATION_STEWARD",
  "contributionSource": "RULES_HUMAN",
  "producingAgentRunId": null,
  "summary": "Rules-first routing for human authority.",
  "routing": {
    "rulesOverallSeverity": "WARN",
    "blockerCodes": [],
    "warnCodes": ["QA_COPY_LENGTH_WARN"],
    "proposedRouting": "HUMAN_REVIEW",
    "requiredHumanAuthority": [
      "ACCEPT_REQUIRES_REVIEWER_OR_ABOVE",
      "WAIVE_REQUIRES_OPERATOR_OR_ADMIN"
    ]
  }
}
```

`proposedRouting` is a server enum derived from rules severity + optional
QA `proposedOutcome` (e.g. `HUMAN_REVIEW` | `RETURN_SUGGESTED` |
`ESCALATION_SUGGESTED`) — advisory only. It does **not** create cases.

## Canonical report `qa-review-report.v1`

Server merges and canonicalizes after rules + both AI stages succeed,
then transactionally inserts report + exactly 3 contributions:

```json
{
  "schemaVersion": "qa-review-report.v1",
  "disclaimer": "Acceptance of this QA review report is control review only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, final production-artwork, or Bliss handshake approval. Deterministic rules check package, decision, variant, asset integrity, and provenance only; they do not certify semantic truth, visual safety, or campaign readiness. AI control roles never receive image bytes and cannot approve or waive. Humans must view the same-origin selected PNG before ACCEPT. The Human Escalation Steward is a rules-first human authority, not an AI worker. Phase 8 must independently define handshake and may distinguish clean acceptance from acceptance-with-exception.",
  "marker": "SYNTHETIC DEVELOPMENT QA REVIEW",
  "provenance": {
    "approvedCreativePackageVersionId": "<guid>",
    "creativePackageDocumentSha256": "<hex>",
    "creativePackageDecisionId": "<guid>",
    "selectedVariantId": "variant_1",
    "selectedCreativeAssetId": "<guid>",
    "selectedCreativeAssetSha256": "<hex>",
    "selectedCreativeAssetMeta": {
      "contentType": "image/png",
      "byteSize": 12345,
      "width": 1080,
      "height": 1080
    },
    "selectedConceptId": "concept_1",
    "approvedBrandDnaVersionId": "<guid>",
    "approvedBrandDnaVersionNumber": 1,
    "approvedColorProfileVersionId": "<guid>",
    "approvedColorProfileVersionNumber": 1,
    "approvedResearchReportVersionId": "<guid>",
    "approvedResearchReportVersionNumber": 1,
    "qaReviewJobId": "<guid>"
  },
  "brief": {
    "reviewObjective": "…",
    "focusAreas": ["COPY", "VISUAL", "PROVENANCE"],
    "notes": null
  },
  "rules": {
    "schemaVersion": "qa-rules.v1",
    "overallSeverity": "PASS",
    "findings": []
  },
  "selectedVariantSnapshot": {
    "id": "variant_1",
    "format": "STATIC_SOCIAL_SQUARE",
    "canvas": { "width": 1080, "height": 1080 },
    "copy": {
      "kind": "CREATIVE_NON_FACTUAL",
      "headline": "…",
      "body": "…",
      "cta": "…"
    },
    "factualClaims": [],
    "asset": {
      "creativeAssetId": "<guid>",
      "contentType": "image/png",
      "byteSize": 12345,
      "sha256": "…",
      "width": 1080,
      "height": 1080
    }
  },
  "contributions": [
    {
      "logicalRole": "CREATIVE_CHAPERONE",
      "contributionSource": "AI",
      "summary": "…"
    },
    {
      "logicalRole": "QA_INSPECTOR",
      "contributionSource": "AI",
      "summary": "…",
      "proposedOutcome": "PASS_RECOMMENDED"
    },
    {
      "logicalRole": "HUMAN_ESCALATION_STEWARD",
      "contributionSource": "RULES_HUMAN",
      "summary": "…",
      "routing": {
        "rulesOverallSeverity": "PASS",
        "blockerCodes": [],
        "warnCodes": [],
        "proposedRouting": "HUMAN_REVIEW",
        "requiredHumanAuthority": [
          "ACCEPT_REQUIRES_REVIEWER_OR_ABOVE",
          "WAIVE_REQUIRES_OPERATOR_OR_ADMIN"
        ]
      }
    }
  ]
}
```

`Summary` is a short server-built string naming schema version,
selectedVariantId, rules overall severity, contribution count (3), agent
run count (2), and focus-area list.

Validation fail closed if merged output:

- lacks exactly **3** contributions in workforce order with correct
  sources;
- Steward has non-null producing run or source ≠ `RULES_HUMAN`;
- AI contributions have null producing run or source ≠ `AI`;
- omits the exact Phase 7 disclaimer string;
- contains unknown fields or forbidden markup/media keys / bytes / URLs;
- embeds image bytes or asset content URLs;
- was produced under Local AI without the conspicuous
  `SYNTHETIC DEVELOPMENT QA REVIEW` marker (required on Local path;
  recommended retained on merged document whenever Local AI was used);
- selected variant / asset pins diverge from job pins;
- AI `proposedOutcome` is `PASS_RECOMMENDED` while rules overall is
  `BLOCK`.

**No partial reports.** Transactional insert of report + 3 contributions
happens only after rules succeed in producing findings, both AI stages
succeed, and merge validation succeeds. Failure at any of those points
creates **no** report and **no** contributions (rules findings may remain
on the failed job for audit).

### Rules BLOCK and AI continuation (locked)

Even when rules `overallSeverity` is `BLOCK`, Phase 7 **still runs both
AI profiles** so humans receive chaperone/QA notes context — unless an
AI/provider failure occurs. BLOCK forbids ACCEPT and forbids AI
`PASS_RECOMMENDED`, but does not skip AI. Steward routing must surface
blocker codes. This preserves evidence without letting AI waive BLOCK.

## Flow

1. authorize workspace access for job create (advertiser own /
   operator/admin; not viewer);
2. require current-approved creative package + latest APPROVE
   `SelectedVariantId` + exactly one selected PNG asset; else **400**;
3. pin package version + DocumentJson SHA, decision, variant, asset
   id/hash/meta, selected concept, DNA/color/research lineage from
   creative package pins;
4. validate brief (`reviewObjective` ≤4000, focusAreas 1–6 unique from
   locked set, notes ≤4000, forbidden fields);
5. replay existing job when `(SourceSystem, IdempotencyKey)` exists
   (SUCCEEDED → return existing; FAILED → return existing, no retry);
6. insert `RUNNING` job with canonical input/SHA and pins;
7. run authoritative `qa-rules.v1` (including PNG revalidation of
   **stored** bytes); persist findings on job; on rules engine crash →
   job `FAILED` (no AI);
8. create run #1 (`CHAPERONE_REVIEW`), invoke AI with text/metadata/rules
   only, persist stage output;
9. create run #2 (`QA_INSPECTION`), invoke AI, persist stage output;
10. deterministic server build Steward contribution from rules routing;
11. merge/validate `qa-review-report.v1`;
12. transactional insert `PROPOSED` report + exactly 3 contributions;
    link run/report FKs; job `SUCCEEDED`;
13. audit throughout.

## Failure saga (normative)

| Condition | Agent runs | Report | Job |
| --- | --- | --- | --- |
| Rules engine crash before AI | none | none | `FAILED` (no findings or partial findings per implementation; prefer persist crash code) |
| AI stage 1 fails after rules | current `FAILED`; stage 2 not created | none | `FAILED` (rules findings kept on job) |
| AI stage 2 fails after stage 1 success | stage 1 SUCCEEDED kept; stage 2 `FAILED` | none | `FAILED` |
| Merge/validate fails after two successful runs | both SUCCEEDED kept | none | `FAILED` |
| Idempotent SUCCEEDED replay | unchanged (exactly 2 runs) | unchanged | return existing; **no** rules/AI calls |
| Idempotent FAILED replay | unchanged | none | return existing; **never** retry |

Successful path evidence: exactly **2** agent runs with token/cost
fields, exactly **1** `PROPOSED` report, exactly **3** role contributions
(2× `AI` + 1× `RULES_HUMAN`), Steward `ProducingAgentRunId` null.
Provider/AI transport failures surface as **502** after durable failure
persistence when appropriate; validation/auth errors remain
400/401/403/404.

## Decision and escalation semantics

### `ACCEPT`

Allowed only when **all** of:

- report status is `PROPOSED`;
- rules `overallSeverity` is **not** `BLOCK`;
- actor is reviewer / operator / admin;
- body includes exact `selectedVariantId` echo matching pin;
- non-empty rationale;
- `visualReviewConfirmed`, `copyReviewConfirmed`,
  `provenanceReviewConfirmed` all `true`;
- `syntheticMarkerAcknowledged` is `true`;
- no forbidden override fields.

Effects: report → `ACCEPTED`; set
`CurrentAcceptedQaReviewReportVersionId`; DocumentJson unchanged.

### `RETURN_FOR_REVISION`

Allowed on `PROPOSED` for reviewer / operator / admin with rationale +
exact selectedVariantId echo.

Effects: report → `RETURNED_FOR_REVISION`; pointer **unset** (cleared if
it somehow pointed here — it should not for PROPOSED).

### `ESCALATE`

Allowed on `PROPOSED` for reviewer / operator / admin with rationale +
exact selectedVariantId echo + `escalationCategory`.

Effects: report → `ESCALATED`; create immutable `OPEN`
`QaEscalationCase`; pointer unset.

### Resolution `RETURN_FOR_REVISION`

Reviewer / operator / admin; case must be `OPEN`.

Effects: case → `RESOLVED`; report → `RETURNED_FOR_REVISION`; pointer
unset.

### Resolution `WAIVE_AND_ACCEPT`

Operator / admin only; case must be `OPEN`; explicit exception rationale
+ acknowledgment; exact acknowledgment of **all** blocker codes on the
report (even if empty array when no blockers — must match exactly).

Effects: case → `RESOLVED`; report → `ACCEPTED_WITH_EXCEPTION`; set
pointer. Phase 8 may distinguish clean `ACCEPTED` vs
`ACCEPTED_WITH_EXCEPTION`; Phase 7 records both immutably.

### Later Phase 6 creative APPROVE

Clears `CurrentAcceptedQaReviewReportVersionId` only. Does **not** mutate
historical QA report status, DocumentJson, contributions, decisions, or
resolutions.

## API

Phase 1–6 routes remain unchanged. Add under `/api/wedding-planner`:

- `POST /workspaces/{id}/qa-review-jobs`
- `GET /workspaces/{id}/qa-review-jobs`
- `GET /qa-review-jobs/{id}`
- `GET /workspaces/{id}/qa-review-reports`
- `GET /qa-review-reports/{id}`
- `GET /qa-review-reports/{id}/contributions`
- `GET /qa-review-reports/{id}/agent-runs`
- `POST /qa-review-reports/{id}/decisions`
- `GET /workspaces/{id}/qa-escalation-cases`
- `GET /qa-escalation-cases/{id}`
- `POST /qa-escalation-cases/{id}/resolutions`
- `GET /workspaces/{id}/agent-runs` (workspace-scoped list; includes QA
  + prior phase runs)

No endpoint that sends asset bytes to AI. No PUT/PATCH/DELETE for
report, contribution, decision, case, or resolution payloads. No
endpoint that marks campaign-ready / legal / handshake. No endpoint that
lets advertisers decide or waive.

### Decision body examples

```json
{
  "decision": "ACCEPT",
  "rationale": "Selected variant passes control review after visual check.",
  "selectedVariantId": "variant_1",
  "visualReviewConfirmed": true,
  "copyReviewConfirmed": true,
  "provenanceReviewConfirmed": true,
  "syntheticMarkerAcknowledged": true,
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

```json
{
  "decision": "RETURN_FOR_REVISION",
  "rationale": "Copy needs revision before acceptance.",
  "selectedVariantId": "variant_1",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

```json
{
  "decision": "ESCALATE",
  "rationale": "Claim boundary uncertain; needs steward path.",
  "selectedVariantId": "variant_1",
  "escalationCategory": "CLAIM_BOUNDARY",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

### Resolution body examples

```json
{
  "resolution": "RETURN_FOR_REVISION",
  "rationale": "Return to creative for claim-safe rewrite.",
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

```json
{
  "resolution": "WAIVE_AND_ACCEPT",
  "rationale": "Operator exception after documented review.",
  "exceptionRationale": "Blockers acknowledged; temporary waiver for control path only.",
  "exceptionAcknowledged": true,
  "acknowledgedBlockerCodes": ["QA_COPY_NON_EMPTY"],
  "sourceSystem": "…",
  "idempotencyKey": "…"
}
```

## Failure and audit

| Condition | Behavior |
| --- | --- |
| Anonymous (OIDC on) | 401 |
| Viewer / no create authority | 403 on job create |
| Advertiser on decision/resolution | 403 |
| Reviewer on `WAIVE_AND_ACCEPT` | 403 |
| Missing or cross-tenant id | 404 |
| Invalid brief, forbidden fields, empty rationale, illegal status transition, missing creative prerequisites, Local AI in Production guard, ACCEPT with BLOCK rules, ACCEPT without confirmations/marker ack/variant echo, ESCALATE without category, waive without exact blocker ack | 400 (or documented 503 for misconfigured production provider) |
| Idempotent replay | existing job/decision/resolution; no second rules/AI/report rows; audit replay |

Audit actions (append-only): QA review job
started/succeeded/failed/replayed; rules findings recorded; each QA
agent run start/success/failure/replay; QA report
proposed/accepted/returned/escalated/accepted-with-exception/replayed;
escalation case opened/resolved; asset content read during human review
(optional bounded access audit, reuse Phase 6). Events carry actor,
request id, workspace/advertiser, job/report/run/case ids, and on
accept/escalate the `selectedVariantId` / category. Secrets are never
stored.

Cost evidence: persist token/cost fields on each of the two agent runs
only (no Steward run cost). Report DTO/list views may surface summed
estimated USD for operators without exposing secrets.

## UI requirements

### Public (`frontend/public`)

- Show QA Review only when an advertiser-capable session can access the
  workspace and a current-approved creative package with
  `SelectedVariantId` + selected asset exists — or clearly disable submit
  with prerequisites stated.
- Controls: review brief form (`reviewObjective`, multi-select 1–6
  focusAreas, optional notes), submit job, list jobs/reports, inspect
  selected-variant snapshot + rules findings + 3 contributions + 2
  linked agent runs.
- Advertisers may request and read; **must not** see enabled ACCEPT /
  RETURN / ESCALATE / resolve controls (hide or disable with policy
  message).
- Display the Phase 7 control-review disclaimer **verbatim**.
- When Local/synthetic AI path was used, show
  `SYNTHETIC DEVELOPMENT QA REVIEW` conspicuously.
- Human reviewers (when using operations or a reviewer-capable surface)
  must be able to open the selected PNG via same-origin
  `/creative-assets/{id}/content` and must attest visual review on
  ACCEPT.
- Do not imply research/concept/creative re-approval, legal clearance,
  campaign-ready, matching changes, or Phase 8 handshake.
- Preserve Phase 2–6 surfaces; do not send image bytes to any client-side
  “AI helper.”

### Operations (`frontend/operations`)

- List QA review jobs, reports, contributions, decisions, escalation
  cases/resolutions, and agent runs for the selected workspace.
- Expose job create for operator/admin; expose ACCEPT / RETURN /
  ESCALATE for reviewer/operator/admin; expose
  `WAIVE_AND_ACCEPT` for operator/admin only.
- Show provider/profile/prompt versions, input SHA-256, creative package
  + decision + variant + asset pins, rules overall severity + codes,
  costs/tokens (2 AI runs), current-accepted QA report marker, and
  clean vs exception acceptance status.
- Local/synthetic reports must remain visually conspicuous in Dev.
- Require same-origin selected PNG viewing before enabling ACCEPT
  confirmations in UI (UX gate; server still enforces boolean
  attestations).

## Tests (exhaustive minimum)

**Brief / prerequisites**

- Job requires current-approved creative package + latest APPROVE
  `SelectedVariantId` + exactly one selected PNG asset; missing → 400.
- `FocusAreas` must be 1–6 unique from locked set; duplicates/unknown →
  400.
- `ReviewObjective` required ≤4000; `Notes` optional ≤4000.
- Forbidden package/variant/asset overrides, match/campaign/placement/
  inventory ids, readiness/QA/legal flags, provider config,
  URLs/base64/markup → 400.
- Server pins package SHA, decision, variant, asset, concept,
  DNA/color/research; client overrides rejected.

**Rules / PNG**

- `qa-rules.v1` runs before AI; findings persisted on job.
- Each BLOCK code fails closed when assertion fails.
- `QA_PNG_REVALIDATE` uses stored bytes + Phase 6 validator; no
  provider regenerate/fetch.
- WARN codes `QA_COPY_LENGTH_WARN` and `QA_LOCAL_SYNTHETIC_MARKER`
  apply when applicable.
- Overall severity BLOCK > WARN > PASS.
- Rules cannot emit semantic/visual/legal/campaign certifications.

**Schema / AI / contributions**

- Exactly 2 agent runs on success; never 3.
- Exactly 3 contributions with sources AI, AI, RULES_HUMAN.
- Steward `ProducingAgentRunId` null; AI runs non-null.
- Chaperone output forbids approval/pixel claims.
- QA `proposedOutcome` cannot be `PASS_RECOMMENDED` under rules BLOCK.
- Local AI path requires conspicuous
  `SYNTHETIC DEVELOPMENT QA REVIEW`.
- Strict unknown fields / forbidden keys rejected.
- AI never receives image bytes/base64/URL/pixels.
- No Human Steward AI contribution / profile / run.

**API / persistence / saga / authority**

- Success path: 1 job SUCCEEDED, 2 agent runs SUCCEEDED with token/cost
  evidence, 1 PROPOSED report, 3 contributions; Phase 6 profiles not
  invoked.
- Mid-stage AI fail → current run FAILED, priors kept, no report.
- Merge fail after two runs → no report.
- SUCCEEDED replay → no rules/AI calls; same ids.
- FAILED replay → no retry; same failed job.
- Advertiser can create/read own; cannot decide/resolve (403).
- ACCEPT requires PROPOSED, non-BLOCK rules, exact variant echo,
  rationale, and three confirmations true; Local/synthetic reports also
  require `syntheticMarkerAcknowledged=true`; sets pointer; report
  ACCEPTED.
- ACCEPT under BLOCK → 400; pointer unchanged.
- RETURN → RETURNED_FOR_REVISION; pointer unset.
- ESCALATE → ESCALATED + OPEN case; pointer unset; categories validated.
- Resolution RETURN by reviewer/operator/admin; WAIVE operator/admin
  only with exact blocker ack; case RESOLVED; waive sets
  ACCEPTED_WITH_EXCEPTION + pointer.
- Later Phase 6 creative APPROVE clears QA pointer only; historical
  report unchanged.
- New ACCEPT moves pointer; does not rewrite old report.
- Cross-tenant 404; anonymous 401; viewer 403 on writes.
- Production guard rejects Local AI when enabled.

**Safe UI / architecture**

- UI `<img>` only to same-origin Phase 6 asset content endpoint for
  selected asset.
- Existing Bliss matching/review/placement and Wedding Planner Phase 1–6
  tests remain green.
- Architecture tests prove Phase 7 sources do not reference deterministic
  match evaluation, Alpha Auto, or n8n authority; do not implement Phase
  8 handshake/campaign-ready; do not send image bytes to AI; do not
  create a third Steward agent run; do not let AI create/resolve
  escalation; and do not widen advertiser generic `CanWrite` into QA
  decision authority.

## Exact disclaimer (locked)

The following string must appear verbatim in `qa-review-report.v1`
`DocumentJson.disclaimer`:

> Acceptance of this QA review report is control review only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, final production-artwork, or Bliss handshake approval. Deterministic rules check package, decision, variant, asset integrity, and provenance only; they do not certify semantic truth, visual safety, or campaign readiness. AI control roles never receive image bytes and cannot approve or waive. Humans must view the same-origin selected PNG before ACCEPT. The Human Escalation Steward is a rules-first human authority, not an AI worker. Phase 8 must independently define handshake and may distinguish clean acceptance from acceptance-with-exception.

## Explicit exclusions

- Phase 8 Bliss handshake / campaign-ready state / auto-advance
- Matching, review, or placement changes / Bliss writes
- Measurement / learning / Alpha Auto
- n8n authority / GHL coupling
- Visual AI / OCR / pixel-diff / embedding providers
- Legal / compliance / accessibility certification products
- Re-running or re-authoring Phase 6 creative roles/profiles/assets
- Three fake per-role agent runs / AI impersonation of Human Escalation
  Steward
- AI create/resolve of escalation cases
- AI receipt of image bytes / base64 / content URLs / pixels
- Provider regenerate/fetch during `QA_PNG_REVALIDATE`
- Widening generic WeddingPlanner `CanWrite` so advertisers can decide
  or waive
- Treating rules PASS as semantic/visual/legal/campaign certification
- Mutating historical accepted QA reports when pointer moves or when
  Phase 6 creative APPROVE clears the QA pointer

## Known residual risks (accepted for Phase 7)

1. **Human visual-attestation risk:** Server can only require boolean
   confirmations that the human viewed the same-origin PNG; it cannot
   prove attentive review. Mitigation: UX gate + required confirmations +
   disclaimer. Residual: rubber-stamp ACCEPT remains possible.
2. **Rules false confidence:** Structural PASS/WARN/BLOCK can be
   mistaken for semantic or legal clearance. Mitigation: locked
   disclaimer, explicit rules non-certification language, UI labeling.
   Residual: operators may over-trust green findings.
3. **AI recommendation drift:** Models may soft-language around BLOCK or
   imply pixel review they did not perform. Mitigation: fail-closed
   schema (`PASS_RECOMMENDED` forbidden under BLOCK), no image bytes to
   AI, chaperone prohibitions, Local marker. Residual: persuasive prose
   in notes fields.
4. **Waiver misuse:** `WAIVE_AND_ACCEPT` can normalize exceptions.
   Mitigation: operator/admin only, exact blocker-code acknowledgment,
   distinct `ACCEPTED_WITH_EXCEPTION` status for Phase 8. Residual:
   organizational pressure to waive.
5. **Pointer vs history confusion:** Clearing QA pointer on later
   creative APPROVE may look like “revoking” history. Mitigation:
   historical report/status/document immutable; UI must show historical
   accepted reports separately from current pointer. Residual: readers
   joining only the pointer miss prior exception acceptances.
6. **BLOCK-still-runs-AI cost:** Jobs with rules BLOCK still pay for two
   AI calls. Mitigation: evidence completeness for humans. Residual:
   higher cost on hard-fail packages (explicitly accepted).

These residuals do not expand Phase 7 scope into Phase 8 handshake,
Bliss writes, visual AI, or legal certification.

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 7 does not auto-advance to Phase 8. Phases 1–6 behavior
and all Bliss matching boundaries remain preserved.
