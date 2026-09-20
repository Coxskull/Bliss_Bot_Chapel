# Alpha AI Development Fleet — Mission 001

Durable investigation artifacts. These documents survive the chat session.

**Mission:** Map, understand, verify, document, plan.  
**Not this mission:** Rewrite Alpha, implement Phase 2, integrate payments, merge repositories.

## Stop condition (read first)

Production-style credentials were found **committed** in Alpha Auto repositories. Values are **not** reproduced here. See `security-findings.md`.

**Do not implement Mission 002 until those credentials are rotated and removed from git history as appropriate.**

## Deliverable index

| # | Deliverable | File |
| --- | --- | --- |
| 1 | Repository inventory | `mission-001-repository-inventory.md` |
| 2 | Baseline build/test report | `mission-001-baseline-tests.md` |
| 3 | Current architecture map | `current-architecture.md` |
| 4 | Current database/entity map | (same file, plus inventory § data model) |
| 5 | Bliss requirements matrix | `implementation-matrix.md` |
| 6 | Fishing Fleet gap | `implementation-matrix.md` § Fishing Fleet |
| 7 | Campaign engine gap | `implementation-matrix.md` § Campaign |
| 8 | Ad delivery gap | `implementation-matrix.md` § Ad Delivery |
| 9 | Measurement gap | `implementation-matrix.md` § Measurement |
| 10 | Financial engine gap | `implementation-matrix.md` § Financial |
| 11 | Security findings | `security-findings.md` |
| 12 | n8n findings | `implementation-matrix.md` § n8n |
| 13 | Global-scalability findings | `implementation-matrix.md` § Global |
| 14 | Provider-coupling findings | `implementation-matrix.md` § Providers |
| 15 | Technical-debt list | `technical-debt.md` |
| 16 | Missing-test list | `technical-debt.md` § Missing tests |
| 17 | Dependency graph | `dependency-map.md` |
| 18 | Proposed crane plan | `crane-plan.md` |
| 19 | Safe parallel workstreams | `crane-plan.md` § Parallel |
| 20 | Recommended Mission 002 | `mission-002-proposal.md` |
| — | Target architecture | `target-architecture.md` |
| — | Point-in-time snapshots (git, tests, DB, API, screenshots) | `snapshots/` |

## Scope of this Cloud Agent workspace

Checked out and writable: **Coxskull/Bliss_Bot_Chapel** only.

Alpha Auto repositories were **cloned read-only** to `/tmp/alpha-audit/` for inspection. They were **not** copied into this repository.
