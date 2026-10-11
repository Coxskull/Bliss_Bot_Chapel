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


## Scope correction — Creator Fleet (2026-10-11)

The current coordinated engineering program includes **Creator Fleet** as part of the Bliss Bot Chapel project scope, alongside Bliss Chapel and The Wedding Planner. The canonical repository to use for this coordinated in-repo work is `Coxskull/Bliss_Bot_Chapel`.

The links `Moddaveel/creator-fleet-hq` and `Moddaveel/creator-fleet-processor` are external references provided in chat; their source trees were not found nested in this repository's recursive `main` tree. Do not treat these separate repositories as the required write targets for this scope. First map the Creator Fleet product requirements to this repository's actual files, current architecture, and approved contracts. Existing Mission 001 notes describe platform adapters, identity resolution, deduplication, snapshots, freshness, and verification missions as missing/planned components; confirm each gap against current source before implementation.

Preserve existing bounded contexts: do not duplicate Creator entities, create a second Campaign model, merge Alpha Auto order finance into Bliss media finance, or split the Wedding Planner module without an architecture decision. Keep `CampaignReady=false` and `Delivery=NOT_SENT` until separate release approval.

Evidence ID for this clarification: `ALPHA-EV-20261011-P2-CREATOR-FLEET-SCOPE`.


## Alpha four-system scope — owner clarification (2026-10-11)

The four systems in the coordinated Alpha engineering program are:

1. **Alpha Auto** — canonical backend `Coxskull/alpha-backend`.
2. **Bliss Bot Chapel** — implemented in `Coxskull/Bliss_Bot_Chapel`.
3. **Creator Academy** — implemented as an in-repository system within `Coxskull/Bliss_Bot_Chapel`, including `Bliss.Api/Controllers/CreativeAcademyController.cs`, `Bliss.Api/Operations/CreativeAcademyService.cs`, `Bliss.Domain/CreativeAcademy/`, persistence entities/configurations/migrations, tests, and the `docs/architecture/contracts/CREATIVE-ACADEMY-CONTRACT.md` contract.
4. **The Wedding Planner** — implemented as an in-repository module within `Coxskull/Bliss_Bot_Chapel`.

Therefore, **Bliss Bot Chapel, Creator Academy, and The Wedding Planner share the Bliss Bot Chapel repository**. They are distinct product/system workstreams within that repository, not separate repositories. Creator Fleet is not one of the four named systems in this program scope; do not substitute it for Creator Academy. Treat any Creator Fleet/Fishing Fleet references in older architecture documents as separate planned capabilities unless the owner explicitly expands the scope.

Keep the work organized by system and evidence ID while preserving shared repository architecture, tests, and migrations. Do not split these systems into repositories or conflate their data domains without an approved architecture decision. Maintain `CampaignReady=false` and `Delivery=NOT_SENT` where the release guard applies.

Evidence ID: `ALPHA-EV-20261011-SCOPE-FOUR-SYSTEMS`.
