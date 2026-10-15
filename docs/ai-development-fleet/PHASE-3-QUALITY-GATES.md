# ALPHA Four-System Phase 3 — Test & Quality Gates

**Program:** `ALPHA-ENG-20261011-01`  
**Evidence ID:** `ALPHA-EV-20261011-P3QG01`  
**Status:** IN PROGRESS — gate definition established; per-change execution evidence remains required.  
**Scope:** Alpha Auto, Bliss Bot Chapel, Creator Academy, The Wedding Planner.

This document defines the coordinated program's Phase 3 gates. It does not replace a system's engineering contract or mark any hosted integration accepted. Bliss Bot Chapel, Creator Academy, and The Wedding Planner remain separate system lanes within the same repository.

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

A queued or running check is **PENDING**, not PASS. A green CI result proves tested commit/build behavior, not live production integration acceptance.

## System-specific gates

### 1. Alpha Auto — `Coxskull/alpha-backend`

- [x] Dependency remediation and fail-closed NuGet audit are present on current main.
- [x] Swagger is gated in non-development environments behind explicit configuration.
- [ ] Verify checkout/payment/order lifecycle contracts and invalid-state handling with automated tests.
- [ ] Verify hosted PostgreSQL TLS using the provider CA and authorized credentials; keep this separate from CI-only evidence.
- [ ] Review older/stacked PR ancestry and document owner-approved disposition.
- [ ] Review remaining nullable-property warnings and confirm current Railway configuration without changing production settings.

Current main: `5d18d226caa4ffaef8e150c1a82e72242039556e`; [main CI run #38134621480 passed](https://github.com/Coxskull/alpha-backend/actions/runs/38134621480). Dependency remediation PR #10 and Swagger gate PR #12 are merged. Older PRs [#7](https://github.com/Coxskull/alpha-backend/pull/7) and [#8](https://github.com/Coxskull/alpha-backend/pull/8) are stale/diverged from main; see tracker evidence `ALPHA-EV-20261011-A7A8ST`. Do not merge either as-is.

### 2. Bliss Bot Chapel — matching, operations and shared infrastructure

- [ ] Full .NET test suite and production-container build on each latest relevant head.
- [ ] Verify matching history remains append-only and re-evaluation does not corrupt another match's current rows/history.
- [ ] Verify role/fallback policy, session/CSRF boundaries, write rate limits, audit events, and safe error responses.
- [ ] Verify migration compatibility and TEST-fixture boundaries; no production truth inferred from synthetic data.
- [ ] Keep provider/network integrations out of scope unless a separate contract explicitly authorizes them.

Current main: `ff55546813ce9e72d952368e5c160f07040e51fa`; [CI run #38135001053 passed](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/38135001053). Historical matching evidence in `docs/bliss/PHASE-3-EVIDENCE.md` is scoped to its dated contract and does not replace current exact-head CI.

### 3. Creator Academy — within `Coxskull/Bliss_Bot_Chapel`

- [x] Remove `[AllowAnonymous]` from the four previously anonymous operations endpoints and add a viewer-403 regression test; PR #162 is merged.
- [ ] Continue testing unauthenticated versus authenticated HTTP requests with OIDC enabled in the test host.
- [ ] Verify invalid/missing visual evidence is refused before acceptance-voyage lookup or mutation.
- [ ] Verify copied prototype identity, malformed text, unknown visual benchmarks, and unsupported queued niches fail closed.
- [ ] Assert model calls remain zero unless a separately configured and authorized provider path is actually invoked.
- [ ] Preserve `CampaignReady=false` and `Delivery=NOT_SENT`.

Evidence: [PR #162](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/162), merged; [main CI run #38134639678 passed](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/38134639678). This confirms the merged code's CI result, not live OIDC/provider acceptance.

### 4. The Wedding Planner — within `Coxskull/Bliss_Bot_Chapel`

- [ ] Verify anonymous reads/writes are rejected when OIDC is enabled.
- [ ] Verify advertiser A cannot read, write, or infer advertiser B's workspace/session/message/economics records.
- [ ] Verify invalid/empty advertiser claims do not grant tenant access.
- [x] Add PostgreSQL concurrency coverage for primary-workspace creation; PR #163 is merged and exact-head CI passed.
- [x] Add PostgreSQL-backed concurrent message-append coverage for same-key replay, distinct keys, sequence ordering, audit count, and session timestamp consistency; exact-head CI passed on PR #165.
- [ ] Complete independent review of PR #165 before any merge-readiness decision.
- [ ] Verify audit events preserve action, actor, outcome, request correlation, and chronology.
- [ ] Verify only an accepted economics quote wakes the planner; suppression and unaccepted quotes keep it asleep.
- [ ] Preserve `CampaignReady=false` and `Delivery=NOT_SENT`; no campaign, pricing, AI, or outbound delivery is inferred from a planning state.

Current append-concurrency evidence: `ALPHA-EV-20261011-WP165A`, [PR #165](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/165), head `031dc4ced7b9a3f31549c679ee24c8cd1f2a074b`; [CI run #38135422161 passed](https://github.com/Coxskull/Bliss_Bot_Chapel/actions/runs/38135422161), with 593 tests passed, 0 failed, 0 skipped, and production-container build passed. The PR remains draft and independent review is pending.

## Current evidence ledger

| Evidence | What it supports | Current limitation |
| --- | --- | --- |
| `ALPHA-EV-20261011-SCOPE-FOUR-SYSTEMS` | Four-system scope taxonomy | Scope record only |
| `ALPHA-EV-20261011-A7A8ST` | Alpha Auto PR #7/#8 compared with current main; both stale/diverged | Owner must choose close-as-superseded or rebase/retest |
| `ALPHA-EV-20261011-B15960` | Bliss PR #159/#160 are 37 commits behind current main | Rebase and rerun relevant checks before review |
| `ALPHA-EV-20261011-WP165A` | Wedding Planner concurrent append implementation and exact-head CI | Draft; independent review pending |
| Alpha Auto main CI #38134621480 | Current main build/test passed at recorded SHA | Not live PostgreSQL TLS acceptance |
| Bliss main CI #38135001053 | Current main build/test/container passed at recorded SHA | Does not replace PR #165's independent review |
| Bliss PR #165 CI #38135422161 | 593 tests passed; production-container build passed | CI is not production acceptance |

## Phase 3 exit criteria

- [ ] Latest exact-head CI is green for active changes.
- [ ] Required test suites for all four system lanes have current commit/run evidence.
- [ ] Security regressions have HTTP-level tests where possible, plus focused unit tests.
- [ ] Open High/Critical findings have a documented disposition, owner, and reviewer.
- [ ] Live hosted acceptance is either verified using authorized access or explicitly left pending.
- [ ] Independent review confirms diffs and merge order; no automatic merge follows a green build.
- [ ] Stale draft PRs have an owner-approved disposition.

**Release guardrails:** this phase is test and quality work. It does not authorize merge, deploy, production data mutation, live provider calls, campaign readiness, or delivery.
