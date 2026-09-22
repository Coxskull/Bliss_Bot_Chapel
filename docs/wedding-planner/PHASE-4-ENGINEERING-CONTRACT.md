# Wedding Planner Phase 4 — Engineering Contract

Status: authorized for implementation by the request to proceed to Phase 4.

## Objective

Add **The Curator** as Wedding Planner research software:

1. an authenticated human submits a research brief for an authorized
   workspace that already has an approved Brand DNA version;
2. source acquisition runs through a vendor-neutral
   `IWeddingPlannerResearchProvider`, then **exactly three** executable AI
   profiles synthesize role contributions; and
3. the server merges a canonical immutable `research-report.v1`. Only a
   human `APPROVE` or `REJECT` decision may change current-approved report
   pointer metadata.

PostgreSQL remains permanent memory. Phase 4 adds **8** logical Curator
roles and **3** executable worker profiles (not eight fake agent runs).
AI generation never equals research approval. Brand DNA may frame queries
but is never itself evidence. The app never fetches citation URLs.

## Assumptions

1. Phases 1–3 remain accepted and unchanged in behavior except for additive
   research tables/pointers, agent-run columns, routes, UI surfaces, tests,
   audit action names, and provider/config registration.
2. A workspace may start a research job only when
   `CurrentApprovedBrandDnaVersionId` is non-null. That Brand DNA id is
   required provenance. DocumentJson/Summary may inform query framing only
   and must never be treated as a cited source or finding evidence.
3. `CurrentApprovedColorProfileVersionId` is optional provenance only when
   present. Color profiles are never evidence and are never required.
4. Research approval is **research approval only**. It is not creative,
   campaign, claim, legal, matching, accessibility, or compliance approval.
5. The allowlist constrains **accepted result URLs**, not necessarily
   research-vendor egress. Vendors may contact their own APIs; the app
   still rejects non-allowlisted citation/result URLs at validation.
6. Source verifier checks metadata and internal consistency only. It does
   **not** fetch or read live URL content.
7. Bliss matching, review, placement, Alpha Auto, and n8n authority remain
   out of scope and untouched.

## Workforce allocation

| Item | Phase 4 allocation |
| --- | --- |
| New logical AI roles | 8 (listed below) |
| Executable profiles / successful calls | exactly 3 |
| Prompt packs | `wp-phase4.curator-research.v1`, `wp-phase4.curator-evidence.v1`, `wp-phase4.curator-synthesis-risk.v1` |
| AI provider | reuse `IWeddingPlannerAiProvider`; shared provider/model allowed; profiles remain separately versioned |
| Research provider | new `IWeddingPlannerResearchProvider` source-acquisition abstraction |
| Agent runs on success | exactly 3 `WeddingPlannerAgentRun` rows |
| Durable role evidence | exactly 8 immutable `ResearchRoleContribution` rows |
| Human authority | humans alone approve/reject research reports |

### Logical roles (exactly 8)

| Order | Logical role | Stage profile |
| --- | --- | --- |
| 1 | `MARKET_LANDSCAPE_RESEARCHER` | `CURATOR_RESEARCH_V1` |
| 2 | `AUDIENCE_CONTEXT_RESEARCHER` | `CURATOR_RESEARCH_V1` |
| 3 | `COMPETITOR_SIGNALS_RESEARCHER` | `CURATOR_RESEARCH_V1` |
| 4 | `CHANNEL_FORMAT_RESEARCHER` | `CURATOR_RESEARCH_V1` |
| 5 | `EVIDENCE_ANALYST` | `CURATOR_EVIDENCE_V1` |
| 6 | `SOURCE_VERIFIER` | `CURATOR_EVIDENCE_V1` |
| 7 | `CLAIMS_RISK_REVIEWER` | `CURATOR_SYNTHESIS_RISK_V1` |
| 8 | `RESEARCH_SYNTHESIZER` | `CURATOR_SYNTHESIS_RISK_V1` |

### Executable profiles (exactly 3)

| Profile key | Prompt pack | Assigned roles | Agent-run `LogicalRole` (stage) |
| --- | --- | --- | --- |
| `CURATOR_RESEARCH_V1` | `wp-phase4.curator-research.v1` | roles 1–4 | `CURATOR_RESEARCH` |
| `CURATOR_EVIDENCE_V1` | `wp-phase4.curator-evidence.v1` | roles 5–6 | `CURATOR_EVIDENCE` |
| `CURATOR_SYNTHESIS_RISK_V1` | `wp-phase4.curator-synthesis-risk.v1` | roles 7–8 | `CURATOR_SYNTHESIS_RISK` |

Do **not** create eight fake agent runs. Durable authority for the eight
roles is `ResearchRoleContribution` (+ `AssignedRolesJson` on each run).
Stage `LogicalRole` values exist for Phase 2-compatible run listing only.

Phase 2 Concierge / Brand DNA Interpreter and Phase 3 Color Intelligence
remain as allocated. Phase 4 does not extend their tools into Curator
outputs, creative generation, or matching writes.

## Authority

- Advertisers act only inside the `advertiser_id`-bound workspace.
- Operators/admins may act across advertisers; viewers cannot write.
- Only an authenticated advertiser or chapel operator/admin may create a
  research job or record an `APPROVE` / `REJECT` decision.
- AI stages may propose findings and role contributions. They cannot
  approve reports, write matching/review/placement records, fetch citation
  URLs, or invent live facts when the local research provider is active.
- Cross-tenant resource lookup returns **404**, consistent with Phases 1–3.
- Anonymous protected reads/writes return **401** when OIDC is enabled.
- Authenticated identities without write authority (e.g. viewer) return
  **403** on job create and decision writes.

## Inputs (human brief)

`POST .../research-jobs` body:

| Field | Required | Rules |
| --- | --- | --- |
| `Topic` | yes | non-empty trimmed string; max length bounded (recommend 200) |
| `Objective` / brief | yes | non-empty trimmed string; max length bounded (recommend 4000) |
| `Questions` | yes | array of 1–8 non-empty trimmed strings |
| `Geography` | yes | non-empty trimmed string |
| `Language` | yes | non-empty trimmed BCP-47-ish tag / language label; max length bounded |
| `AllowedDomains` | yes | 0–10 exact hostnames (see URL validation); empty = no host allowlist |
| `SourceSystem` | yes | Phase 1–3 idempotency pattern |
| `IdempotencyKey` | yes | unique with `SourceSystem` for the **job** |

Server also binds, without client override:

- required `ApprovedBrandDnaVersionId` = workspace current approved Brand DNA;
- optional `ApprovedColorProfileVersionId` = workspace current approved color
  profile when present (provenance only).

Canonical input JSON for hashing is a stable sorted-key object of the
canonical brief fields (topic, objective, questions, geography, language,
normalized allowed domains), the approved Brand DNA version id, optional
color profile version id when present, schema version
`research-brief.v1`, and research/provider contract versions used.
Persist both the canonical JSON string and its lowercase hex SHA-256 on the
job. Actor labels are excluded from the hash.

## URL validation

Accepted citation / catalog URLs must satisfy **all** of:

1. absolute `http` or `https` only;
2. no userinfo (`user:pass@`);
3. no fragment (`#...`);
4. host is not `localhost`, `*.localhost`, or a DNS name resolving policy
   equivalent used by the validator for loopback labels;
5. host/IP is not loopback, private, link-local, or cloud metadata
   (`127.0.0.0/8`, `::1`, `10/8`, `172.16/12`, `192.168/16`, `169.254/16`,
   `fc00::/7`, `fe80::/10`, `169.254.169.254`, etc.);
6. max URL length and max host length bounded (recommend 2048 / 253);
7. when `AllowedDomains` is non-empty, the URL host must equal an allowed
   hostname **or** be a subdomain of one (exact label boundary; no
   `evil.example.com.attacker.tld` tricks).

Allowed domain entries themselves must be exact hostnames (no scheme, path,
port, wildcard `*`, IP literals, or userinfo). Normalize by trim +
lowercase before persist/compare.

The remote research adapter response body must be size-bounded and schema-
strict. Unknown fields, oversized payloads, or invalid URLs fail closed.

## Provider contracts

### `IWeddingPlannerResearchProvider` (new)

Vendor-neutral source acquisition. Returns a bounded source catalog plus
provider/model/adapter identity, request id, and estimated cost. It does
**not** complete Curator AI stages and is not customer memory.

| Kind | Behavior |
| --- | --- |
| Local deterministic (default Dev/CI) | Emits conspicuous **SYNTHETIC** fixtures only; URLs/hosts use `.invalid`; never claims live facts |
| Remote HTTP JSON | Production adapter; config-driven BaseUrl/ApiKey/timeout/cost; strict bounded JSON response |

Configuration section (example): `WeddingPlannerResearch` with
`Provider` (`Local` \| `RemoteHttp`), `BaseUrl`, `ApiKey`, `TimeoutSeconds`,
optional `WorkerKey` / adapter version, and cost fields. Secrets are never
stored on jobs, runs, reports, or audit rows.

**Production guard:** when the host environment is Production (or an
explicit `RequireRemoteResearchProvider` flag is set), starting a research
job with the Local provider is rejected (**400/503** as implemented
consistently with Phase 2 AI production expectations). Dev/CI may use
Local freely.

### `IWeddingPlannerAiProvider` (reuse)

Each of the three profiles calls `CompleteAsync` once with:

- stage `LogicalRole` (`CURATOR_RESEARCH` / `CURATOR_EVIDENCE` /
  `CURATOR_SYNTHESIS_RISK`);
- the stage prompt pack;
- JSON response format;
- context built only from same-tenant durable brief, Brand DNA framing
  summary/id, approved catalog JSON, and prior stage outputs already
  persisted on the job.

Shared provider/model is allowed. Profile prompt packs remain separately
versioned. Provider failures fail the current run and job; they do not
fabricate reports.

## Durable records

### Extend `WeddingPlannerAgentRun`

Add nullable:

- `WorkerProfileVersion` — e.g. `CURATOR_RESEARCH_V1`
- `AssignedRolesJson` — JSON array of the logical roles assigned to that run
- `OutputResearchReportVersionId` — set on the final successful synthesis
  run when a report is created; null on earlier stage runs and failures

Existing Concierge / Brand DNA runs leave these null. Curator stage
`LogicalRole` uses the three stage values above for backward-compatible
listing; the eight durable role contributions remain authoritative.

Internal run idempotency keys are the job key with a stable suffix, e.g.
`{jobKey}:CURATOR_RESEARCH`, `:CURATOR_EVIDENCE`,
`:CURATOR_SYNTHESIS_RISK` (exact suffix strings locked in implementation
constants). Each successful job therefore owns exactly three run rows with
tokens, cost, status, worker/prompt/provider/model/adapter evidence.

### `WeddingPlannerResearchJob`

Durable orchestration row (statuses `RUNNING` | `SUCCEEDED` | `FAILED`):

- advertiser/workspace scope
- canonical `InputJson`, `InputSha256`
- brief fields + normalized `AllowedDomainsJson`
- required Brand DNA provenance id; optional color profile provenance id
- research provider key/adapter/request id and estimated acquisition cost
- `SourceCatalogJson` (accepted catalog after validation)
- three stage output JSON fields (raw/canonical per-stage worker outputs)
- `ResearchAgentRunId`, `EvidenceAgentRunId`, `SynthesisRiskAgentRunId`
  (nullable until created)
- `OutputResearchReportVersionId` (null unless SUCCEEDED)
- status, bounded error code/message, timestamps, actor metadata
- `SourceSystem`, `IdempotencyKey`

Unique `(SourceSystem, IdempotencyKey)` is the **job** idempotency
boundary. Replaying a `FAILED` job returns the failed job and **never**
retries providers or AI. Replaying a `SUCCEEDED` job returns the existing
job/report linkage with **no** provider or AI calls.

### `WeddingPlannerResearchReportVersion`

Immutable snapshot:

- advertiser/workspace scope; monotonic `VersionNumber` per workspace
- `SchemaVersion` = `research-report.v1`
- `DocumentJson`, `Summary`
- producing job id; producing synthesis agent-run id
- Brand DNA (+ optional color) provenance ids
- `Status`: `PROPOSED` | `APPROVED` | `REJECTED` | `SUPERSEDED`
- source/idempotency (report-level key may derive from job key +
  `:REPORT` or equal the job pair if uniqueness is enforced only once—
  pick one approach and keep it consistent; recommend
  `(SourceSystem, IdempotencyKey)` unique on report with
  `{jobKey}:REPORT`)
- created-at / actor metadata

No PUT/PATCH of payload fields after insert.

### `WeddingPlannerResearchRoleContribution`

Exactly **8** immutable rows per successful report:

- advertiser/workspace/report/job scope
- `LogicalRole` (one of the eight; unique per report)
- `ProducingAgentRunId` (the stage run that emitted that role)
- contribution payload JSON (findings for that role)
- created-at

No extras. No missing roles. Contributions are never edited in place.

### `WeddingPlannerResearchReportDecision`

Immutable human decision: `APPROVE` | `REJECT`, required non-empty
rationale, actor, source/idempotency, timestamp. Unique
`(SourceSystem, IdempotencyKey)`.

### Workspace pointer

Add nullable `CurrentApprovedResearchReportVersionId` on
`WeddingPlannerWorkspace`. Approval sets it. Approving a later version
sets the prior current report to `SUPERSEDED` without mutating
`DocumentJson`. Rejected versions never become current. Only `PROPOSED`
versions accept a first decision; later attempts on non-`PROPOSED` are
**400**.

## Output schemas

### Per-stage AI output `curator-worker-output.v1`

Each AI stage returns strict JSON containing **only** its assigned role
contributions/findings. Shape:

```json
{
  "schemaVersion": "curator-worker-output.v1",
  "workerProfileVersion": "CURATOR_RESEARCH_V1",
  "contributions": [
    {
      "logicalRole": "MARKET_LANDSCAPE_RESEARCHER",
      "summary": "…",
      "findings": [
        {
          "type": "FACT",
          "statement": "…",
          "confidence": 0.0,
          "citationSourceIds": ["src_1"]
        }
      ]
    }
  ]
}
```

Rules:

- `contributions` must include exactly the assigned roles for that profile,
  each non-empty (at least one finding), and no extras.
- Finding `type`: `FACT` | `INFERENCE` | `GAP` | `RISK`.
- `confidence` ∈ `[0, 1]`.
- `FACT` / `INFERENCE` / `RISK` require one or more **known** catalog
  `citationSourceIds`.
- `GAP` may be uncited.
- Unknown or dangling citation ids fail closed.
- Local provider catalogs and findings must remain conspicuously synthetic
  (`.invalid` hosts / SYNTHETIC markers); production remote catalogs must
  still pass URL validation.

### Canonical report `research-report.v1`

Server merges and canonicalizes after all three stages succeed:

```json
{
  "schemaVersion": "research-report.v1",
  "disclaimer": "Approval of this report is research approval only. It is not creative, campaign, claim, legal, matching, accessibility, or compliance approval. Source verification checks metadata and internal consistency only; live URL content is not fetched or certified.",
  "provenance": {
    "approvedBrandDnaVersionId": "<guid>",
    "approvedBrandDnaVersionNumber": 1,
    "approvedColorProfileVersionId": null,
    "researchJobId": "<guid>"
  },
  "brief": {
    "topic": "…",
    "objective": "…",
    "questions": ["…"],
    "geography": "…",
    "language": "…",
    "allowedDomains": []
  },
  "sources": [
    {
      "id": "src_1",
      "title": "…",
      "url": "https://example.invalid/synthetic",
      "publisher": "…",
      "retrievedAt": "2026-01-01T00:00:00Z",
      "synthetic": true
    }
  ],
  "contributions": [ /* exactly 8 role objects, order as workforce table */ ],
  "synthesis": {
    "executiveSummary": "…",
    "openQuestions": ["…"],
    "risks": ["…"]
  }
}
```

`Summary` is a short server-built string naming schema version, topic, and
contribution/source counts. The disclaimer string above (or an exact
locked constant equal in meaning and enumerated exclusions) must appear
verbatim in `DocumentJson`.

Validation fail closed if merged output lacks exactly eight non-empty
contributions, contains extras, has dangling citations, or omits the
disclaimer.

## Flow

1. authorize workspace write access;
2. require current-approved Brand DNA; else **400**;
3. replay existing job when `(SourceSystem, IdempotencyKey)` exists
   (SUCCEEDED → return existing; FAILED → return existing, no retry);
4. insert `RUNNING` job with canonical input/SHA;
5. call `IWeddingPlannerResearchProvider`; on failure → job `FAILED`,
   **0** agent runs, no report;
6. validate/canonicalize source catalog onto the job;
7. create run #1 (`CURATOR_RESEARCH`), invoke AI, persist stage output;
8. create run #2 (`CURATOR_EVIDENCE`), invoke AI, persist stage output;
9. create run #3 (`CURATOR_SYNTHESIS_RISK`), invoke AI, persist stage output;
10. merge/validate `research-report.v1`; insert `PROPOSED` report + exactly
    8 contributions; link run/report FKs; job `SUCCEEDED`;
11. audit throughout.

Any AI stage failure or validation failure: mark the **current** run
`FAILED` (if created), mark job `FAILED`, keep prior successful run
receipts and stage JSON already stored, create **no** report and **no**
contributions. No partial proposed reports.

## Failure saga (normative)

| Condition | Agent runs | Report | Job |
| --- | --- | --- | --- |
| Research provider fails | 0 | none | `FAILED` |
| AI stage N fails | prior SUCCEEDED kept; current `FAILED`; later not created | none | `FAILED` |
| Validation/canonicalize fails after a stage | current run `FAILED` (or failed without succeeding); priors kept | none | `FAILED` |
| Idempotent SUCCEEDED replay | unchanged | unchanged | return existing; **no** calls |
| Idempotent FAILED replay | unchanged | none | return existing; **never** retry |

Provider/AI transport failures surface as **502** after durable failure
persistence when appropriate; validation/auth errors remain 400/401/403/404.

## API

Phase 1–3 routes remain unchanged. Add under `/api/wedding-planner`:

- `POST /workspaces/{id}/research-jobs`
- `GET /workspaces/{id}/research-jobs`
- `GET /research-jobs/{id}`
- `GET /workspaces/{id}/research-reports`
- `GET /research-reports/{id}`
- `GET /research-reports/{id}/contributions`
- `GET /research-reports/{id}/agent-runs`
- `POST /research-reports/{id}/decisions`
- `GET /workspaces/{id}/agent-runs` (workspace-scoped list; useful for
  Curator + prior Phase 2 runs)

No URL-fetch endpoint. No PUT/PATCH/DELETE for report or contribution
payloads.

## Failure and audit

| Condition | Behavior |
| --- | --- |
| Anonymous (OIDC on) | 401 |
| Viewer / no write authority | 403 on job create and decisions |
| Missing or cross-tenant id | 404 |
| Invalid brief, empty rationale, illegal status transition, no approved Brand DNA, Local provider in Production guard | 400 (or documented 503 for misconfigured production provider) |
| Idempotent replay | existing job/decision; no second provider/AI/report rows; audit replay |

Audit actions (append-only): research job started/succeeded/failed/replayed;
each curator agent run start/success/failure/replay; research report
proposed/approved/rejected/superseded/replayed. Events carry actor, request
id, workspace/advertiser, and job/report/run ids. Secrets are never stored.

Cost evidence: persist research-provider estimated cost on the job and
token/cost fields on each of the three agent runs. Report DTO/list views
may surface summed estimated USD for operators without exposing secrets.

## UI requirements

### Public (`frontend/public`)

- Show Curator research only when an advertiser-capable session can access
  the workspace and a current-approved Brand DNA exists (or clearly disable
  submit with that prerequisite stated).
- Controls: brief form (topic, objective, 1–8 questions, geography,
  language, 0–10 allowed domains), submit job, list jobs/reports,
  inspect contributions and linked agent runs, approve/reject with required
  rationale.
- Display the research disclaimer verbatim. Do not imply live URL
  verification, legal approval, creative approval, or matching changes.
- Preserve Phase 2–3 Concierge / Brand DNA / Color surfaces.

### Operations (`frontend/operations`)

- List research jobs, reports, contributions, decisions, and agent runs for
  the selected workspace.
- Expose job create and approve/reject for operator/admin.
- Show provider/profile/prompt versions, input SHA-256, provenance Brand
  DNA (and color when present), costs/tokens, and current-approved marker.
- Local/synthetic catalogs must remain visually conspicuous in Dev.

## Tests (exhaustive minimum)

**URL / allowlist / catalog**

- Absolute HTTP(S) accepted; userinfo, fragment, localhost/private/link-
  local/metadata/loopback rejected.
- Allowlist exact host and subdomain pass; lookalike hosts fail.
- Empty allowlist does not require host match but still applies safety
  rules.
- Remote adapter oversized/invalid JSON fails closed.
- Local catalog uses `.invalid` / SYNTHETIC markers only.

**Schema / merge**

- Each stage output accepts only assigned roles; extras/missing fail.
- FACT/INFERENCE/RISK without known citations fail; GAP may be uncited.
- Confidence outside `[0,1]` fails.
- Merge requires exactly 8 contributions and the exact disclaimer.
- Dangling citations fail closed.

**API / persistence / saga**

- Job requires approved Brand DNA; otherwise 400.
- Success path: 1 job SUCCEEDED, 3 agent runs SUCCEEDED, 1 PROPOSED report,
  8 contributions; optional color provenance when present.
- Provider fail → FAILED job, 0 runs, 0 reports.
- Mid-stage AI fail → current run FAILED, priors kept, no report.
- Validation fail → no partial proposed report.
- SUCCEEDED replay → no provider/AI calls; same ids.
- FAILED replay → no retry; same failed job.
- Approve sets pointer; later approve supersedes without payload mutation.
- Reject never sets pointer; empty rationale 400; non-PROPOSED decision 400.
- Advertiser A cannot access B's jobs/reports/contributions/runs (404).
- Anonymous 401; viewer 403 on writes.
- Production guard rejects Local research provider when enabled.

**Architecture / regression**

- Existing Bliss matching/review/placement and Wedding Planner Phase 1–3
  tests remain green.
- Architecture tests prove Phase 4 Curator sources do not reference
  deterministic match evaluation, Alpha Auto, or n8n authority; do not
  implement app-side citation crawling; and do not call image/creative
  generators.

## Explicit exclusions

- Phase 5 concepts/prototypes
- Assets and image generation
- Creative department roles (strategist/art/copy/production)
- Chaperone / QA AI
- Campaign-ready state and Bliss handshake
- Matching, review, or placement changes
- Measurement / learning
- Alpha Auto
- n8n authority / GHL coupling
- App-side crawling or any URL-fetch endpoint
- Legal / compliance / accessibility certification products
- Treating Brand DNA or color profiles as research evidence
- Eight fake per-role agent runs

## Acceptance gate

Contract → implementation → automated tests → evidence → human review and
acceptance. Phase 4 does not auto-advance to Phase 5. Phases 1–3 behavior
and all Bliss matching boundaries remain preserved.
