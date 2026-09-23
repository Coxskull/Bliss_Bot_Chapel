# Wedding Planner Phase 9 — Measurement / Learning Engineering Contract

Status: authorized for implementation by the user's request to proceed to Phase 9.

## Objective

Turn bounded, human-attested aggregate campaign observations into an immutable,
reviewable learning report without inventing delivery telemetry or causal
attribution.

Phase 9 adds exactly three logical intelligence roles on exactly two executable
worker profiles:

| Executable profile | Durable logical-role contributions |
| --- | --- |
| `PERFORMANCE_ANALYSIS_V1` | `PERFORMANCE_ANALYST` |
| `LEARNING_SYNTHESIS_V1` | `LEARNING_SYNTHESIZER`, `OPTIMIZATION_ADVISOR` |

A successful job creates exactly two `WeddingPlannerAgentRun` rows and exactly
three immutable role-contribution rows. It does not create three fake model
calls.

## Boundary correction

The repository has no campaign-delivery or event-tracking subsystem. Phase 8
creates a `PLANNED` placement and explicitly does not activate, schedule,
deliver, publish, reserve inventory, or measure it. Phase 9 therefore:

- accepts only operator/admin-entered aggregate observations with an explicit
  source label and attestation;
- never treats the `PLANNED` placement as proof that delivery happened;
- never collects event-level data, cookies, device identifiers, URLs, or PII;
- never mutates campaign, placement, placement-run, slot, match, handshake,
  creative, or QA state;
- never claims causal attribution, statistical significance, incrementality,
  legal compliance, or platform reconciliation;
- produces recommendations only; it cannot revise creative, activate a
  campaign, change spend, reserve inventory, or write to external systems;
- does not invent inventory prices, quotes, or compensation splits (see
  `docs/bliss-economics/FUTURE-BOUNDED-CONTEXT.md`).

## Authority

- Operator/admin only may create measurement-learning jobs and submit report
  decisions.
- Advertisers may read their own jobs, reports, contributions, decisions, and
  agent runs.
- Reviewer/operator/admin may read across workspaces under a dedicated Phase 9
  read policy.
- Advertisers, reviewers, and viewers cannot create jobs or decisions.
- Cross-tenant resources return 404. Wrong-role writes return 403. Anonymous
  protected requests return 401 when OIDC is enabled.
- AI may analyze and recommend. Only an operator/admin may `ACCEPT` or `REJECT`
  a proposed learning report.

## Human job input

`POST /api/wedding-planner/workspaces/{workspaceId}/measurement-learning-jobs`

Required:

- `campaignReadinessHandshakeVersionId`
- `observationStart`, `observationEnd` (UTC, end strictly after start, no future
  end, maximum 366 days)
- `sourceLabel` (1–128 characters)
- `sourceSystem` (1–64, normalized upper)
- `idempotencyKey` (bounded so derived stage/report keys fit 128 characters)
- `attestationAcknowledged = true`
- integer aggregate counts: `impressions`, `clicks`, `conversions` (all >= 0;
  clicks <= impressions)
- `spend` and optional `revenue` (>= 0, decimal)
- ISO 4217-like three-letter uppercase `currencyCode`
- optional bounded `notes`

The attestation states verbatim:

> These are human-supplied aggregate observations from the named source. Bliss did not collect or verify delivery events. The linked placement remains PLANNED and does not prove activation or delivery. Metrics show association only, not causation or incrementality. No event-level data, personal data, external URLs, or platform credentials are included. AI output is advisory and cannot change creative, campaign, placement, inventory, spend, or external systems.

Forbidden input includes event arrays, user/device/contact identifiers, cookies,
IP addresses, URLs, credentials, pixels, scripts, markup, base64, raw provider
payloads, client-computed rates, confidence/significance claims, attribution
models, status overrides, AI/provider configuration, or instructions to mutate
upstream records.

## Prerequisites and pins

The selected handshake must:

- exist in the same advertiser/workspace scope;
- have been created by Phase 8 with a valid placement and placement-run pin;
- pin the existing same-scope QA/package/asset/lineage chain;
- have `Status` either `CAMPAIGN_READY` or `REVOKED`.

Historical/revoked handshakes are measurable because observations may arrive
after readiness was revoked. The report records the handshake status snapshot
and whether it was current at job start. Neither status is interpreted as proof
of delivery.

Server pins handshake, placement, placement run, match, campaign, content,
slot, QA report, creative package, selected variant, selected asset, concept,
Brand DNA, color, and research ids. Client pin overrides are rejected.

## Deterministic measurement rules

`measurement-rules.v1` runs before any AI. Every code appears exactly once with
`PASS` or `BLOCK`; no `WARN`. Any block fails the job before AI calls or report
creation:

1. `ML_HANDSHAKE_SCOPE`
2. `ML_HANDSHAKE_PLACEMENT_LINK`
3. `ML_PLACEMENT_STILL_PLANNED`
4. `ML_OBSERVATION_WINDOW`
5. `ML_SOURCE_ATTESTED`
6. `ML_AGGREGATE_COUNTS`
7. `ML_FINANCIAL_VALUES`
8. `ML_CURRENCY_CODE`
9. `ML_NO_EVENT_LEVEL_DATA`
10. `ML_PROVENANCE_CHAIN`

The server alone computes decimal metrics with documented null semantics and
fixed six-decimal rounding:

- CTR = clicks / impressions when impressions > 0
- conversion rate = conversions / clicks when clicks > 0
- CPM = spend * 1000 / impressions when impressions > 0
- CPC = spend / clicks when clicks > 0
- CPA = spend / conversions when conversions > 0
- ROAS = revenue / spend when revenue is supplied and spend > 0

Zero denominators produce null, never zero, infinity, NaN, or an invented rate.
AI receives server-computed aggregates/rates plus bounded provenance metadata;
it never receives asset bytes, external URLs, raw events, or PII.

## Workflow and immutability

1. Authorize and validate the body.
2. Resolve scoped handshake and immutable provenance.
3. Run `measurement-rules.v1` and compute canonical metrics.
4. Persist a `RUNNING` job and audit event.
5. Invoke `PERFORMANCE_ANALYSIS_V1`.
6. Invoke `LEARNING_SYNTHESIS_V1`.
7. Validate both structured outputs fail-closed, including exact profile/role
   mapping and prohibited causal/autonomous claims.
8. Atomically create one immutable `measurement-learning-report.v1`, exactly
   three role contributions, link both successful agent runs, and complete the
   job.
9. On provider/output failure, retain the failed job/run receipts but create no
   partial report or contributions.

Exact replay returns the existing terminal job and writes/calls nothing.
Derived idempotency keys are:

- `{jobKey}:PERFORMANCE_ANALYSIS`
- `{jobKey}:LEARNING_SYNTHESIS`
- `{jobKey}:REPORT`

## Durable records

### `WeddingPlannerMeasurementLearningJob`

Append-oriented orchestration receipt with scoped pins, canonical input JSON and
SHA-256, aggregate observations, server metrics, deterministic findings, exactly
two run ids, output report id, status/error, actor, timestamps, and unique
`(SourceSystem, IdempotencyKey)`.

### `WeddingPlannerMeasurementLearningReportVersion`

Immutable payload with monotonic workspace version, schema version
`measurement-learning-report.v1`, canonical `DocumentJson`, summary, all
provenance pins, observation snapshot, deterministic findings, two producing
run ids, status `PROPOSED | ACCEPTED | REJECTED`, actor/source metadata, and
unique workspace/version and source/key constraints. Document payload is never
edited.

### `WeddingPlannerMeasurementLearningRoleContribution`

Exactly three immutable rows per successful report. Each has one of the locked
logical roles and references the mapped producing run.

### `WeddingPlannerMeasurementLearningDecision`

Immutable operator/admin `ACCEPT | REJECT` row with rationale, actor,
source/idempotency, and timestamp. A report may receive only one terminal
decision. `ACCEPT` sets
`WeddingPlannerWorkspace.CurrentAcceptedMeasurementLearningReportVersionId`;
`REJECT` does not. Accepting a later report supersedes the previous accepted
report status and moves the pointer; historical payloads remain immutable.

## Output constraints

The canonical report separates:

- observed aggregates (human supplied);
- derived metrics (deterministic);
- descriptive patterns (AI advisory);
- limitations and data gaps;
- learning hypotheses;
- recommended future tests requiring human action.

Required language: association, observation, hypothesis, recommendation.
Forbidden language: proved, caused, guaranteed, statistically significant
without an implemented method, certified, autonomously applied, delivered by
Bliss, or attribution certainty.

Development local-provider output is marked
`SYNTHETIC DEVELOPMENT MEASUREMENT LEARNING`. Non-Development continues to
require the configured remote AI provider. Synthetic reports cannot masquerade
as production measurement.

## API

- `POST /workspaces/{id}/measurement-learning-jobs`
- `GET /workspaces/{id}/measurement-learning-jobs`
- `GET /measurement-learning-jobs/{id}`
- `GET /workspaces/{id}/measurement-learning-reports`
- `GET /measurement-learning-reports/{id}`
- `GET /measurement-learning-reports/{id}/contributions`
- `GET /measurement-learning-reports/{id}/agent-runs`
- `GET /measurement-learning-reports/{id}/decisions`
- `POST /measurement-learning-reports/{id}/decisions`

No PUT/PATCH/DELETE. No ingestion webhook, tracking pixel, delivery endpoint,
optimization execution endpoint, or upstream mutation endpoint.

## UI

Public advertiser UI is read-only. It shows reports, observed versus derived
metrics, provenance, limitations, contributions, decisions, and conspicuous
labels: `HUMAN-SUPPLIED AGGREGATES`, `ASSOCIATION — NOT CAUSATION`,
`ADVISORY ONLY`, and synthetic markers where applicable.

Operations UI adds a gated aggregate-entry form, report inspection, and
accept/reject controls. It must show the attestation verbatim and never label a
planned placement as activated or delivered.

## Minimum verification

- all ten deterministic blocker codes;
- arithmetic, null denominators, fixed rounding, date/currency bounds;
- exactly 3 roles mapped to exactly 2 runs;
- no AI call before deterministic validation;
- replay produces no writes/calls;
- malformed/prohibited AI output fails without partial report;
- immutable pins and cross-tenant negatives;
- auth matrix and advertiser read-only UI;
- decision/pointer/supersession behavior;
- zero mutation of Phase 8 and Bliss placement records;
- empty PostgreSQL migration and live workflow;
- browser evidence for operations creation/inspection and public read-only gate.

## Explicit exclusions

Delivery, activation, scheduling, publication, slot reservation, tracking
pixels, event collection, user-level analytics, identity resolution,
cross-device tracking, external platform APIs, automatic attribution,
incrementality, statistical experimentation engine, budget/bid changes,
automatic creative revision, campaign optimization execution, Bliss match
recompute, legal/compliance certification, payment, Alpha Auto, n8n/GHL
authority, and mutation of any Phase 1–8 artifact.

## Acceptance gate

Contract → implementation → automated tests → PostgreSQL/browser evidence →
human review. Phase 9 is the final phase listed in Master Blueprint V1.1; it
does not silently invent a Phase 10.
