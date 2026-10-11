# ALPHA Four-System Phase 3 — Test & Quality Gates

**Program:** `ALPHA-ENG-20261011-01`  
**Evidence ID:** `ALPHA-EV-20261011-P3-QA-GATES`  
**Status:** IN PROGRESS — gate definition established; per-change execution evidence remains required.  
**Scope:** Alpha Auto, Bliss Bot Chapel, Creator Academy, The Wedding Planner.

This document defines the coordinated program's Phase 3 gates. It does not replace a system's own engineering contract or mark any hosted integration accepted. The three Bliss-repository workstreams remain separate system lanes within the same repository.

## Shared merge gate

A change is not merge-ready unless all applicable checks are recorded against the exact proposed head SHA:

1. Restore and Release build succeed.
2. Full relevant automated test suite succeeds; new security findings have a regression test.
3. Production container build succeeds where the repository provides that job.
4. Dependency/security audit succeeds, with findings and exceptions documented.
5. API/authentication/authorization behavior is exercised through HTTP integration tests where applicable, not solely by checking attributes or source text.
6. Database changes are additive and migration-tested; tenant isolation, idempotency, and append-only audit/history are tested where applicable.
7. Evidence includes repository, paths, finding/test IDs, commit SHA, CI run URL and conclusion, reviewer status, and unresolved hosted-only checks.
8. No production deployment, production write, merge, paid provider call, or credential change occurs without explicit authorization.

A queued or running check is **PENDING**, not PASS. A green CI result proves the tested commit/build behavior, not live production integration acceptance.

## System-specific gates

### 1. Alpha Auto — `Coxskull/alpha-backend`

- [ ] Exact-head restore/build/test and security/dependency audit for each proposed PR.
- [ ] Verify checkout/payment/order lifecycle contracts and invalid-state handling with automated tests.
- [ ] Verify hosted PostgreSQL TLS using the provider CA and authorized credentials; keep this separate from CI-only evidence.
- [ ] Confirm Swagger is disabled in production unless explicitly enabled.
- [ ] Review stacked/overlapping PR ancestry before proposing merge order.
- [ ] Record remaining trust/configuration issues accurately; do not imply they are fixed without a corresponding commit and test.

Current references: [PR #10](https://github.com/Coxskull/alpha-backend/pull/10), [PR #12](https://github.com/Coxskull/alpha-backend/pull/12), [program tracker #11](https://github.com/Coxskull/alpha-backend/issues/11).

### 2. Bliss Bot Chapel — matching, operations and shared infrastructure

- [ ] Full .NET test suite and production-container build on the latest relevant head.
- [ ] Verify matching history remains append-only and re-evaluation does not corrupt another match's current rows/history.
- [ ] Verify role/fallback policy, session/CSRF boundaries, write rate limits, audit events, and safe error responses.
- [ ] Verify migration compatibility and TEST-fixture boundaries; no production truth inferred from synthetic data.
- [ ] Keep provider/network integrations out of scope unless a separate contract explicitly authorizes them.

Existing historical evidence: `docs/bliss/PHASE-3-EVIDENCE.md` reports 43/43 tests for its matching-history Phase 3 scope. That record is scoped to the dated contract/evidence; it does not replace current exact-head CI.

### 3. Creator Academy — within `Coxskull/Bliss_Bot_Chapel`

- [ ] Verify the four previously anonymous operations endpoints inherit fallback authorization.
- [ ] Exercise unauthenticated versus authenticated HTTP requests with OIDC enabled in the test host.
- [ ] Verify invalid/missing visual evidence is refused before acceptance-voyage lookup or mutation.
- [ ] Verify copied prototype identity, malformed text, unknown visual benchmarks, and unsupported queued niches fail closed.
- [ ] Assert model calls remain zero unless a separately configured and authorized provider path is actually invoked.
- [ ] Preserve `CampaignReady=false` and `Delivery=NOT_SENT`.

Active evidence: `ALPHA-EV-20261011-P2-CREATIVE-ACADEMY-AUTH`, [PR #162](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/162). Its added authenticated/anonymous HTTP test must pass on the exact current head before the finding is marked validated.

### 4. The Wedding Planner — within `Coxskull/Bliss_Bot_Chapel`

- [ ] Verify anonymous reads/writes are rejected when OIDC is enabled.
- [ ] Verify advertiser A cannot read, write, or infer advertiser B's workspace/session/message/economics records.
- [ ] Verify invalid/empty advertiser claims do not grant tenant access.
- [ ] Verify idempotent retries return the original workspace/session/message and do not duplicate audit or conversation rows.
- [ ] Verify audit events preserve action, actor, outcome, request correlation, and chronology.
- [ ] Verify only an accepted economics quote wakes the planner; suppression and unaccepted quotes keep it asleep.
- [ ] Preserve `CampaignReady=false` and `Delivery=NOT_SENT`; no campaign, pricing, AI, or outbound delivery is inferred from a planning state.

Existing tests include `Bliss.Tests/Api/WeddingPlannerPhase1ApiTests.cs`, `Bliss.Tests/Api/WeddingPlannerWakeApiTests.cs`, persistence tests, and `Bliss.Tests/WeddingPlanner/CreativeApprovalTests.cs`. Test presence is not a substitute for recording the latest suite result.

## Current evidence ledger

| Evidence | What it supports | Current limitation |
| --- | --- | --- |
| `ALPHA-EV-20261011-SCOPE-FOUR-SYSTEMS` | Owner-approved four-system taxonomy | Scope record only |
| `ALPHA-EV-20261011-P2-CREATIVE-ACADEMY-AUTH` | Academy authorization remediation and regression coverage | Exact-head CI for the latest added HTTP test must finish green |
| Alpha Auto CI run `38129623114` | Main build/test passed at its recorded SHA | Not live PostgreSQL TLS acceptance |
| Alpha Auto PR #10 CI run `38129761668` | Dependency PR exact-head CI passed at recorded SHA | PR remains draft/unmerged |
| Alpha Auto PR #12 CI run `38130493275` | Swagger gate exact-head CI passed at recorded SHA | PR remains draft/unmerged |
| Bliss CI run `38122505586` | Build/test/container passed at its recorded SHA | Predates subsequent docs and Academy PR changes |
| Bliss PR #162 latest CI | Must match latest head SHA | Do not mark pass while queued/running |

## Phase 3 exit criteria

- [ ] Latest exact-head CI is green for active security remediation.
- [ ] The required test suites for all four system lanes have current commit/run evidence.
- [ ] Security regressions have HTTP-level tests where possible, plus focused unit tests.
- [ ] Open High/Critical findings have a documented disposition, owner, and reviewer.
- [ ] Live hosted acceptance is either verified using authorized access or explicitly left pending.
- [ ] Independent review confirms diffs and merge order; no automatic merge follows a green build.

**Release guardrails:** this phase is test and quality work. It does not authorize merge, deploy, production data mutation, live provider calls, campaign readiness, or delivery.
