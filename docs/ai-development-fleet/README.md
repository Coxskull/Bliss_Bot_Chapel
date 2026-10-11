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
| — | Economics engine (future) | `../economics/ARCHITECTURE.md` |
| — | Point-in-time snapshots (git, tests, DB, API, screenshots) | `snapshots/` |

## Scope of this Cloud Agent workspace

Checked out and writable: **Coxskull/Bliss_Bot_Chapel** only.

Alpha Auto repositories were **cloned read-only** to `/tmp/alpha-audit/` for inspection. They were **not** copied into this repository.


## Alpha four-system scope — owner clarification (2026-10-11)

The four systems in the coordinated Alpha engineering program are:

1. **Alpha Auto** — canonical backend `Coxskull/alpha-backend`.
2. **Bliss Bot Chapel** — implemented in `Coxskull/Bliss_Bot_Chapel`.
3. **Creator Academy** — a distinct system implemented inside `Coxskull/Bliss_Bot_Chapel`, including its API controller/service, domain, persistence, migrations, tests, and contract.
4. **The Wedding Planner** — a distinct module implemented inside `Coxskull/Bliss_Bot_Chapel`.

Bliss Bot Chapel, Creator Academy, and The Wedding Planner share the Bliss Bot Chapel repository. Track them as separate system workstreams within that repository, not as separate repositories. **Creator Fleet is not one of the four systems named for this program** and must not replace Creator Academy in the scope. Treat older Creator/Fishing Fleet references as background/planned capabilities unless the owner explicitly expands the scope.

Keep system boundaries and shared architecture intact. Do not split these systems into repositories, duplicate creator/campaign models, or merge Alpha Auto order-finance data into Bliss media-finance concepts without an approved architecture decision. Preserve `CampaignReady=false` and `Delivery=NOT_SENT` where the release guard applies.

Evidence ID: `ALPHA-EV-20261011-SCOPE-FOUR-SYSTEMS`.
