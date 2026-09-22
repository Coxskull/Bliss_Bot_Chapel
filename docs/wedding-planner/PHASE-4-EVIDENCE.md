# Wedding Planner Phase 4 — Evidence

## Result

Phase 4 The Curator is implemented against
`PHASE-4-ENGINEERING-CONTRACT.md`.

New logical AI roles: **8**. New executable worker profiles per successful
job: **3**.

The implementation does not create eight fake model calls. A successful job
creates exactly three `WeddingPlannerAgentRun` receipts and eight immutable
role contributions.

## Workforce proof

| Executable profile | Durable logical contributions |
| --- | --- |
| `CURATOR_RESEARCH_V1` | market landscape, audience context, competitor signals, channel/format |
| `CURATOR_EVIDENCE_V1` | evidence analyst, source verifier |
| `CURATOR_SYNTHESIS_RISK_V1` | claims-risk reviewer, research synthesizer |

Each profile has its own prompt pack, assigned-role JSON, provider receipt,
tokens, cost, and terminal status. Profiles may share a provider/model
without erasing their separately versioned authority contracts.

## Evidence boundary

Source acquisition is behind `IWeddingPlannerResearchProvider`.

- Development/CI uses a deterministic local adapter whose titles, publishers,
  findings, and URLs are visibly `SYNTHETIC`.
- Local citation hosts use reserved `.invalid` names and never represent live
  research.
- Production requires `RemoteHttp`; the local provider is rejected outside
  Development.
- The remote adapter uses a configured backend endpoint, bounded JSON,
  timeout, bearer secret handling, and a strict response contract.
- The application never fetches, crawls, resolves, or downloads returned
  citation URLs.

Stored citation URLs accept only bounded absolute HTTP(S) metadata. Userinfo,
fragments, localhost, loopback, private, link-local, metadata, unsafe literal
IP ranges, and allowed-domain bypasses are rejected without DNS/network I/O.

The source verifier checks metadata and internal consistency, not live page
content.

## Citation and report integrity

The server, not the client or AI provider, enforces:

- exactly eight known, non-empty role contributions;
- no missing or extra roles;
- finding types `FACT`, `INFERENCE`, `GAP`, or `RISK`;
- confidence values in `[0,1]`;
- known source citations for every FACT, INFERENCE, and RISK;
- GAP findings may be uncited;
- no dangling source identifiers;
- canonical `research-report.v1` output;
- the exact research-approval disclaimer.

Current-approved Brand DNA is required as framing provenance but is never a
citable source. Current-approved Color Intelligence is optional provenance
only.

## Durable saga

Migration:
`20260922101851_WeddingPlannerPhase4Curator`

The complete migration chain was applied successfully to an empty PostgreSQL
database through Phase 4.

New durable records:

- `WeddingPlannerResearchJobs`
- `WeddingPlannerResearchReportVersions`
- `WeddingPlannerResearchRoleContributions`
- `WeddingPlannerResearchReportDecisions`
- nullable current-approved research report pointer on the workspace
- Curator profile/assigned-role/report-output fields on agent runs

Research jobs remember canonical input/SHA-256, source catalog, provider
receipt/cost, three stage outputs, three run ids, errors, status, timestamps,
and report linkage.

Failure behavior is append-oriented:

- research-provider failure → FAILED job, zero Curator agent runs, no report;
- AI/validation failure → current run and job FAILED, prior receipts kept, no
  partial report;
- successful replay → no provider or AI call;
- failed replay → same failed job, no retry.

## API and authority

Phase 4 adds job, report, contribution, report-run, workspace-run, and human
decision routes under `/api/wedding-planner`.

Controls preserve Phases 1–3:

- authenticated advertiser is bound to its `advertiser_id`;
- cross-tenant jobs/reports/contributions/runs/decisions return 404;
- anonymous protected requests return 401;
- viewers cannot create jobs or decisions;
- human APPROVE/REJECT requires rationale;
- later approval supersedes the prior current report without mutating either
  report document;
- no PUT/PATCH/DELETE or citation-fetch route exists.

Research approval is not creative, campaign, claim, legal, matching,
accessibility, or compliance approval.

## Automated verification

Command:

`dotnet test Bliss.Tests/Bliss.Tests.csproj --nologo`

Result:

- passed: 209
- failed: 0
- skipped: 0

Coverage includes:

- brief/domain/URL/allowlist validation;
- source-catalog and worker-output strict schemas;
- citations, confidence, exact role/profile mapping, report merge;
- local synthetic evidence guarantees;
- remote adapter request/response, size, timeout, HTTP, invalid payload, and
  secret-safe errors;
- happy path exact 3 calls/runs and 8 mapped contributions;
- provider and stage failure sagas;
- succeeded/failed idempotent replay;
- approved Brand DNA prerequisite and optional color provenance;
- decisions, supersession, immutability, tenancy, anonymous/viewer controls;
- EF foreign keys, cycles, unique indexes, and defaults;
- architecture guards against returned-URL fetching, Bliss matching,
  Alpha Auto, and n8n authority;
- public/operations server-document-only rendering.

Both JavaScript bundles pass syntax checks.

## PostgreSQL workflow smoke test

A real PostgreSQL workflow completed:

1. open workspace and session;
2. create and approve Brand DNA;
3. submit a Curator research job;
4. acquire three synthetic fixture sources;
5. execute the three Curator profiles;
6. persist eight role contributions and one PROPOSED report;
7. approve the report with human rationale.

Observed:

- job `SUCCEEDED`;
- research provider `local-deterministic-research`;
- source catalog visibly `SYNTHETIC` and `.invalid`;
- three runs, all `SUCCEEDED`, one for each expected profile;
- eight contributions in the authoritative role order;
- report schema `research-report.v1`;
- exact research-only disclaimer;
- report became `APPROVED` and current.

## Browser verification

Chrome exercised the PostgreSQL-backed public and operations interfaces at a
1,440 × 1,000 desktop viewport.

Public:

- Phase 4 heading, 8→3 mapping, no-live/no-fetch/synthetic copy, full brief
  form, and disabled advertiser prerequisite rendered without horizontal
  overflow;
- empty report/decision UI remained hidden.

Operations:

- TEST Dental Manila loaded provider, input SHA, provenance, source catalog,
  contributions, synthesis, exact three run receipts, costs, and disclaimer;
- synthetic evidence was visibly marked as fixture-only;
- a second job, `Browser Curator validation`, completed through the UI;
- human approval moved report v2 to `APPROVED · CURRENT APPROVED` and v1 to
  `SUPERSEDED`;
- the report showed exactly eight contributions and three worker receipts;
- an initial long run-receipt row exposed horizontal overflow. The grid was
  corrected with a zero-minimum content column and safe wrapping, then
  reverified with no horizontal overflow.

## Preserved boundaries

Phase 4 does not add concepts, prototypes, assets, creative generation,
Chaperone/QA, campaign-ready state, Bliss handshake, measurement learning,
GHL coupling, Alpha Auto, n8n authority, or any change to deterministic Bliss
evaluation, formation, review, or placement.

Phase 4 does not auto-advance to Phase 5.
