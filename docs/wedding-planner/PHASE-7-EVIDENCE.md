# Wedding Planner Phase 7 — Evidence

## Result

Phase 7 Chaperone / QA / Escalation Control Department is implemented
against `PHASE-7-ENGINEERING-CONTRACT.md`.

New logical control roles: **3** —
`CREATIVE_CHAPERONE`, `QA_INSPECTOR`, `HUMAN_ESCALATION_STEWARD`.
Executable AI profiles / runs per successful job: **exactly 2** —
`CHAPERONE_REVIEW_V1` and `QA_INSPECTION_V1`. Durable contributions on
success: **exactly 3** with sources `AI` / `AI` / `RULES_HUMAN`.

`HUMAN_ESCALATION_STEWARD` is server-built from versioned rules routing
plus required human authority metadata. Steward
`ProducingAgentRunId` is **null**. Contribution source is `RULES_HUMAN`
only. There is **no** third fake agent run and no Steward AI profile.

Phase 6's thirteen creative contributions, package document, assets, and
APPROVE decision remain pinned provenance only (0 Phase 6 re-runs).

## Workforce proof

| Executable profile | Durable logical contribution | Contribution source |
| --- | --- | --- |
| `CHAPERONE_REVIEW_V1` | `CREATIVE_CHAPERONE` | `AI` |
| `QA_INSPECTION_V1` | `QA_INSPECTOR` | `AI` |
| *(none — rules + humans)* | `HUMAN_ESCALATION_STEWARD` | `RULES_HUMAN` |

Each AI profile has its own prompt pack, assigned-role JSON, provider
receipt, tokens, cost, and terminal status. Profiles may share a
provider/model without erasing separately versioned authority contracts.
Deterministic `qa-rules.v1` and Steward materialization are **not** AI
workers and do **not** create agent-run rows.

## Deterministic `qa-rules.v1` (authoritative, pre-AI)

Rules run **before** either AI profile. Overall severity =
**BLOCK > WARN > PASS**. AI **cannot** downgrade rules severity. BLOCK
is authoritative for ACCEPT path (ACCEPT forbidden; AI cannot emit
`PASS_RECOMMENDED` under BLOCK). Even under BLOCK, both AI profiles still
run for human context unless provider failure occurs.

Exact locked codes (15):

| Code | Severity when failing / applicable |
| --- | --- |
| `QA_CURRENT_PACKAGE_PIN` | BLOCK |
| `QA_CURRENT_APPROVE_DECISION` | BLOCK |
| `QA_SCHEMA_VERSION` | BLOCK |
| `QA_DISCLAIMER_EXACT` | BLOCK |
| `QA_PROVENANCE_CHAIN` | BLOCK |
| `QA_CONTRIBUTION_COUNT_13` | BLOCK |
| `QA_CREATIVE_RUNS_6` | BLOCK |
| `QA_VARIANT_REFS` | BLOCK |
| `QA_CLAIM_PRESERVATION` | BLOCK |
| `QA_SELECTED_ASSET_META` | BLOCK |
| `QA_PNG_REVALIDATE` | BLOCK |
| `QA_FORBIDDEN_MARKUP_MEDIA` | BLOCK |
| `QA_COPY_NON_EMPTY` | BLOCK |
| `QA_COPY_LENGTH_WARN` | WARN when applicable |
| `QA_LOCAL_SYNTHETIC_MARKER` | WARN when applicable |

`QA_PNG_REVALIDATE` revalidates **stored** PNG bytes with the Phase 6
validator (signature / structural checks), recomputes hash/size/dims, and
matches stored + package refs — no provider regenerate or fetch.
Provenance checks include the 13 Phase 6 contributions, 6 Phase 6 runs,
claim preservation, palette/provenance pins, and copy non-empty /
length-warn checks.

Rules are **structural** integrity / provenance checks only. They do
**not** certify semantic truth, visual safety, legal clearance,
accessibility, or campaign readiness.

## Prerequisites and evidence boundary

A QA review job requires a current-approved Phase 6 creative package
whose latest `APPROVE` decision carries a valid `SelectedVariantId`, and
that selected variant has **exactly one** linked PNG asset. Package id +
DocumentJson SHA, decision id, selected variant, asset id/hash/meta, and
pinned concept / Brand DNA / Color Profile / Research Report provenance
are required pins. Missing prerequisites return 400. Sibling variants are
provenance context only.

AI stages receive structured text, asset **metadata**, and rules findings
only — **never** image bytes, base64, content URLs, or pixels. Humans
must view the selected PNG via the existing same-origin authenticated
`GET /api/wedding-planner/creative-assets/{id}/content` endpoint and
attest visual review on ACCEPT.

QA review is **control review only**. It is not research, claim, legal,
matching, accessibility, compliance, campaign-ready, final
production-artwork, or Bliss handshake approval.

## Durable saga

Migration:
`20260922154734_WeddingPlannerPhase7QaChaperone`

The complete migration chain was applied successfully to empty PostgreSQL
database `bliss_phase7_verify` through Phase 7 (latest migration as
above).

New / extended durable records:

- `WeddingPlannerQaReviewJobs`
- `WeddingPlannerQaReviewReportVersions`
- `WeddingPlannerQaRoleContributions`
- `WeddingPlannerQaReviewDecisions`
- `WeddingPlannerQaEscalationCases`
- `WeddingPlannerQaEscalationResolutions`
- nullable `CurrentAcceptedQaReviewReportVersionId` on the workspace
- QA profile / assigned-role / output-report fields on agent runs

Jobs retain rules findings and stage receipts while saga status advances
to a terminal state. Report `DocumentJson`, contributions, decisions,
case opening payloads, and resolutions are immutable after insert; case
status alone advances `OPEN` → `RESOLVED`. Later Phase 6 creative
`APPROVE` **clears the QA pointer only**; historical accepted report
status/document/contributions remain unchanged.

Failure after rules/AI/merge creates no report and no contributions
(rules findings may remain on a failed job). Succeeded/failed replay
returns the existing job without retry.

## API and authority

Phase 7 adds QA-job, report, contribution, report-run, decision,
escalation-case, and resolution routes under `/api/wedding-planner`.

Dedicated review policy (not generic WeddingPlanner `CanWrite`
widening):

- advertiser may create/read own QA jobs/reports/cases;
- advertisers **cannot** record decisions or resolutions;
- reviewer / operator / admin may `ACCEPT` / `RETURN_FOR_REVISION` /
  `ESCALATE` and resolve `RETURN_FOR_REVISION`;
- `WAIVE_AND_ACCEPT` is **operator/admin only** (exact blocker-code
  acknowledgment);
- viewers cannot write;
- cross-tenant resources return 404; anonymous protected requests return
  401;
- no PUT/PATCH/DELETE of durable QA documents;
- job / decision / resolution idempotency uses `(SourceSystem,
  IdempotencyKey)`.

## Automated verification

Command:

`node --check frontend/public/wedding-planner.js && node --check frontend/operations/app.js && dotnet test Bliss.Tests/Bliss.Tests.csproj --no-restore`

Result:

- passed: 279
- failed: 0
- skipped: 0
- duration: ~6 seconds (tests)

Migration chain applied to empty PostgreSQL `bliss_phase7_verify`
through Phase 7 as noted above.

## PostgreSQL workflow smoke test

Against `bliss_phase7_verify`, exact recorded result from
`/opt/cursor/artifacts/phase7_postgres_workflow.json`:

1. First QA: severity **WARN**; exactly **2** runs and **3**
   contribution sources `AI` / `AI` / `RULES_HUMAN`; same-origin PNG
   signature validated; human **ESCALATE** then
   **WAIVE_AND_ACCEPT** → report **ACCEPTED_WITH_EXCEPTION**;
2. Later Phase 6 creative approve **cleared** the QA pointer;
3. Second QA: severity **WARN**; clean human **ACCEPT** → report
   **ACCEPTED** and became current accepted
   (`CurrentAcceptedQaReviewReportVersionId`).

## Browser verification

Chrome exercised the PostgreSQL-backed public and operations interfaces.

Public:

- under auth-disabled / no bound advertiser, submit was correctly
  disabled;
- zero decision forms and zero images in the gate.

Operations:

- current accepted selected PNG rendered with naturalWidth **1080**;
- all **3** roles, **2** AI profiles, and `RULES_HUMAN` Steward source
  visible;
- prior escalation case shown resolved;
- walkthrough submitted a third QA → `PROPOSED`, same-origin image;
  human **ESCALATE** `VISUAL_UNCERTAINTY` → case **OPEN**; human
  **RETURN_FOR_REVISION** → case **RESOLVED** / report
  **RETURNED_FOR_REVISION** while the prior accepted pointer remained
  current;
- **3** reports / **2** cases observed;
- no horizontal overflow (`horizontalOverflow` false;
  `scrollWidth == clientWidth`).

## Artifacts

- `/opt/cursor/artifacts/phase7_public_qa_gate.png`
- `/opt/cursor/artifacts/phase7_current_accepted_qa.png`
- `/opt/cursor/artifacts/phase7_rules_roles_runs.png`
- `/opt/cursor/artifacts/phase7_escalation_open.png`
- `/opt/cursor/artifacts/phase7_escalation_returned.png`
- `/opt/cursor/artifacts/phase7_human_escalation_return_workflow.mp4`
- `/opt/cursor/artifacts/phase7_postgres_workflow.json`

`videoReview` verdict: **PASS** — temporally clear and suitable evidence
(minor auto outro after completion only). Recorded flow: third QA submit
→ `PROPOSED` with same-origin PNG → human `ESCALATE`
`VISUAL_UNCERTAINTY` → `OPEN` → human `RETURN_FOR_REVISION` →
`RESOLVED` / `RETURNED_FOR_REVISION` while old accepted pointer remained
current.

## State-truthfulness caveat

Jobs/status and workspace QA pointer advance; report `DocumentJson`,
contributions, decisions, cases, and resolutions remain immutable.
Clearing the QA pointer on later creative APPROVE does not rewrite
historical accepted reports. Human visual attestation is boolean
confirmation only (cannot prove attentive review). Rules PASS/WARN/BLOCK
are structural, not semantic/legal/visual certification. Local/synthetic
QA reports carry `SYNTHETIC DEVELOPMENT QA REVIEW`.

## Limitations / residual risks

Accepted Phase 7 residuals (do not expand scope):

1. Human visual-attestation rubber-stamp risk;
2. Rules false confidence mistaken for semantic/legal clearance;
3. AI recommendation drift / soft language around BLOCK;
4. Waiver misuse of `WAIVE_AND_ACCEPT`;
5. Pointer vs history confusion when creative APPROVE clears the pointer;
6. BLOCK-still-runs-AI cost for evidence completeness.

## Preserved boundaries

Phase 7 does **not** add Phase 8 handshake / campaign-ready, Bliss
matching/review/placement writes, visual AI / OCR / pixel inspection,
legal / compliance / accessibility certification, measurement learning,
Alpha Auto, or n8n/GHL authority.

Phase 7 does **not** auto-advance to Phase 8.
