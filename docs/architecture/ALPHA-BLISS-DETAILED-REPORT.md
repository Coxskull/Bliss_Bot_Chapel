# Alpha + Bliss detailed progress and state report

**As of:** 2026-10-03  
**Repository baseline:** `ed8b8f1` (main, after pull request #97)  
**Scope:** Alpha acquisition Phases 4–29, priority contracts 1–16, Bliss
Chapel, Economics, Wedding Planner, live local readings, verification,
limitations, and recommended next work.

This report describes stored evidence only. It invents no census, traffic,
price, revenue, cost, or hosted deployment claim. Delivery remains
`NOT_SENT`. Green does not send.

## 1. Executive summary

Alpha and Bliss run as one ASP.NET Core application with one PostgreSQL
system of record and one operations console. The acquisition tracker records
Phases 4–29 as verified local proofs. Since the previous current-state report
(baseline `60e0913`, 347 tests), fifteen priority contracts have been built,
tested, recorded in a browser, and documented. Priority 3, the formal
next-contract planning review, has no row in the tracker.

| Measure | Previous report | This report |
| --- | --- | --- |
| Full test suite | 347 passed | 441 passed, 0 failed, 0 skipped |
| Acquisition phases verified | 4–29 | 4–29 (unchanged) |
| Priority contracts closed | none | 1, 2, 4–16 |
| Evidence PDFs in the repository | 26 acquisition PDFs | 42 PDFs, plus this report |
| EF Core migrations | — | 30 |
| Operations console views | — | 26 |
| Merged pull requests | — | 97 |

The main strength is still the integrity of the boundaries. A public contact
is not permission to send. A missing person, price, count, market, or model
stays missing. Suppression stops the prohibited action and keeps the
business. Every new reading in this cycle refuses an invented value with an
HTTP 400 and states that nothing was sent.

The platform is not finished. Hosted acceptance is not claimed, live sending
has no authorized contract, revenue is not recorded, and the seven grooming
models are not configured.

## 2. Architecture

### 2.1 Application boundary

| Project | Responsibility |
| --- | --- |
| `Bliss.Api` | REST API, public Alpha and Bliss pages, the operations console, rate limiting, write authorization |
| `Bliss.Domain` | Pure deterministic deciders. Illegal states throw `InvalidOperationException` |
| `Bliss.Infrastructure` | EF Core 8 and Npgsql mappings, migrations, seed data, persistence |
| `Bliss.Tests` | xUnit tests for architecture, persistence, authorization, API, frontend contracts, and each demonstration |

### 2.2 Authority boundary

| Decision | Authority |
| --- | --- |
| Creator and advertiser compatibility | `DeterministicRuleEvaluator` |
| Price, quote, negotiation envelope | Economics (the only price authority) |
| Campaign workspace | Wedding Planner |
| Durable business state | PostgreSQL database `bliss_chapel` |
| Routine compositing and QR | Deterministic software; recipe `overlay-1` |
| Human authority | Creative approval, binding negotiation, authorization, close |

### 2.3 The pattern every priority contract follows

1. A static decider in `Bliss.Domain/Demonstrations` validates the input and
   refuses invented values, blank keys, and missing history.
2. An idempotent key (8–80 characters, letters, digits, hyphens) makes a
   repeated submission return the stored record with `duplicate: true` and
   `written: false`. It does not overwrite or raise counts.
3. A controller turns a refusal into HTTP 400 with `delivery: "NOT_SENT"`,
   `greenMeansSend: false`, and the safety flags for that reading.
4. An EF Core migration adds one append-only table.
5. One view on the existing operations console shows the reading and a
   refused action. No second console is created.
6. Evidence is a markdown record, a contract, a browser recording reviewed
   frame by frame, screenshots, and a letter-size PDF checked with `pypdf`.

## 3. Alpha acquisition Phases 4–29

All twenty-six phases are verified local proofs. Accepted Bliss, Economics,
and Wedding Planner work stays on its own acceptance path.

| Phases | Capability |
| --- | --- |
| 4–5 | Source media library, duplicates, fuel; one software demonstration with QR, disclosure, and an unsent preview |
| 6–9 | Discovery at score 100; public decision-maker evidence; preserving a business below 100; contact roads and suppression |
| 10–12 | Recipe runtime and overlay; factory QA and batch manifest; acquisition events from observable behavior |
| 13–16 | Ask Alpha (one voice, answer then advance); price answers from Economics; negotiation inside Economics; eligible delivery that stays `NOT_SENT` |
| 17–20 | Prospect memory in PostgreSQL; Bliss rematch; Wedding Planner wake; conversation laboratory (10 scenarios) |
| 21–25 | Spend and pause controls; grooming and external research; scale proofs at 100, 1,000, and 10,000 in-memory checks; green/yellow/red progression; flow control |
| 26–29 | Independent fleet lanes; marketplace balance signals; balanced creative inventory (pairs of 2, 4, or 6); rotation abundance |

## 4. Priority contracts completed in this cycle

| Priority | Capability | What the proof stores | What it refuses |
| --- | --- | --- | --- |
| 1 | Bliss hosted acceptance reading | The process posture: Development, gates not applied, no hosted database, no verified server certificate, no HTTPS identity provider, no backup declared | Claiming hosted acceptance; claiming an identity provider was contacted or a backup drill was run |
| 2 | Economics Phase 9 owner acceptance | Accepted by the owner on 2026-10-03. Actuals stay append-only. Zero placements and zero campaigns are on file | Repricing, settlement, rewriting a recommendation |
| 4 | Subscription ledger and factory budget | Stored service rows; operational ceilings that degrade only their own scope | Inventing a cost; treating a review as a purchase |
| 5 | Deterministic media runtime | The player is the served picture. QA matches the QR and the disclosure | Requiring a permanent per-prospect composite |
| 6 | Source media coverage | Coverage counts qualified slices with a fingerprint and provenance | Counting a duplicate as coverage |
| 7 | Bounded advertiser discovery | One stored prospect per public source URL | A duplicate source, a blank name, a crawler, a census |
| 8 | Contact route audit | One explicit transmission request recorded once | Routing through suppression, stale evidence, or a missing source; transmitting |
| 9 | Local batch measurement | The clock and the process working set | Claiming an invoice, hosted acceptance, or the 15-minute target |
| 10 | Marketplace handoff | A qualified advertiser and a stored creator handed to `DeterministicRuleEvaluator` once | A preserved advertiser; another tenant's row; declaring a win |
| 11 | Human creative approval | A human decision inside an open workspace | Opening a discovered business; marking campaign ready; inventing a price |
| 12 | Research ledger | A note appended only after the laboratory graduates | Changing production; showing another prospect's note |
| 13 | Later rotation period | Measured open slots and the creator decision | Filling a theoretical slot; rewriting stored slots |
| 14 | Coverage week | One week of measured coverage | Adding a missing market; claiming a census |
| 15 | Marketplace metrics | Measured advertiser, creator, and slot counts; zero revenue rows | Recording a revenue amount; adding a slot |
| 16 | Closed models | Seven grooming models are not configured; configured models stay at zero | Configuring a model; claiming authorized traffic or an engagement count |

## 5. Live local readings

Read from the running API at `http://127.0.0.1:5001` on 2026-10-03 against
local PostgreSQL. These are counts of stored rows, not a census.

### 5.1 Hosted posture (`/api/operations/hosted`)

| Field | Value |
| --- | --- |
| Environment | Development |
| Production gates applied | false |
| Hosted database configured | false |
| Database server certificate verified | false |
| Identity provider over HTTPS | false |
| Backup declared | false |
| Secret material external | true |
| Role claims distinct | true |
| Hosted acceptance claimed | false |

### 5.2 Economics Phase 9 (`/api/operations/acceptance`)

Accepted: true. Placements on file: 0. Campaigns on file: 0. "No historical
actual is on file. None was invented." Repricing and settlement are not
authorized.

### 5.3 Fuel and coverage (`/api/operations/fuel`, `/api/operations/week`)

| Field | Value |
| --- | --- |
| Daily target | 20 |
| Submitted | 2 |
| Qualified unique | 1 |
| Duplicates | 1 |
| Daily remaining | 19 |
| Fuel status | SHORTAGE |
| Markets with coverage | 1 (Panama City, Panama: 1 qualified slice) |
| Stored week | `coverage-week-1` |

### 5.4 Discovery (`/api/operations/discovery`)

Three stored prospects with a public source URL. Two scored, one preserved.
One submission was withheld because it had no public http or https source.

| Business | Market | Score | State |
| --- | --- | --- | --- |
| Casa Verde | Panama City | 100 | DEMONSTRATION_PREPARED |
| Mesa Norte | Panama City | 100 | DEMONSTRATION_PREPARED |
| Puerto Azul | Quito | 75 | PRESERVED |

### 5.5 Marketplace (`/api/operations/metrics`, `/api/operations/rotation`)

| Field | Value |
| --- | --- |
| Stored advertisers | 4 (Harbor Audio Labs, Sunrise Wellness Co., TEST Dental Manila, TEST Restaurant Santo Domingo) |
| Stored creators | 3 (Test Creator, Test Creator Brazil, Unknown Demographics Creator) |
| Stored slots | 8 |
| Revenue rows | 0 |
| Pressure | Stored advertiser pressure is ahead of stored creator pressure |
| Later periods | `later-period-1` (creator approved), `later-period-withheld` (creator not approved); 6 theoretical slots, 4 placed, 2 open |

### 5.6 Learning and models (`/api/operations/learning`, `/api/operations/models`)

| Field | Value |
| --- | --- |
| Laboratory | Passed 10 of 10 scenarios |
| Research notes | 1 (Casa Verde) |
| Configured grooming models | 0 of 7 |
| Model calls | 0 |
| Authorized traffic | false |
| Production changed | false |

### 5.7 Subscription ledger (`/api/operations/ledger`)

Bliss Bot Chapel is classified `FREE_SELF_HOSTED`. PostgreSQL and the OpenID
Connect identity service are observed, with owner, plan, and cost
unrecorded. "Cost is not recorded. None was invented."

## 6. Bliss Chapel, Economics, and Wedding Planner

**Bliss Chapel.** Phases 1–20 cover deterministic matching, eligibility,
review, placement planning, operator workflows, audit exports, verification
receipts, OIDC, runtime hardening, observability, and fail-closed production
posture. The hosted reading in Priority 1 shows those gates are not yet
applied to a real hosted environment. Hosted deployment is not declared
finished.

**Economics.** Phases 1–9 are implemented. The owner accepted Phase 9 on
2026-10-03. Economics is the only price authority. Operational ceilings,
stored counts, pair sizes, and rotation slots are not prices. Historical
actuals do not change recommendations or quotes and create no settlement,
invoice, payment, or payout.

**Wedding Planner.** Phase 1 provides workspaces, sessions, messages, audit,
authorization, and tenancy, with zero AI. Alpha Phase 19 lets an accepted
Economics result open an existing workspace. Priority 11 records a human
creative decision inside an open workspace. Phases 2–9 are not started.

## 7. Verification

```text
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
Passed!  - Failed: 0, Passed: 441, Skipped: 0, Total: 441
```

Each priority contract in this cycle was also checked by:

- a scan of the new domain code for currency signs and provider clients
  (`$`, `OpenAI`, `Smtp`, `HttpClient`), with none found;
- applying its migration to local PostgreSQL and reading the API;
- a browser screen recording reviewed frame by frame;
- a letter-size PDF checked with `pypdf` for page count and exact text.

How to read these results:

- passing tests prove the bounded local behavior they name;
- local PostgreSQL does not prove hosted capacity;
- in-memory scale checks do not prove stored prospect volume;
- a prepared preview does not authorize delivery;
- fixture people, businesses, and prices are not claims about real ones.

## 8. Known limitations and open gaps

1. Hosted acceptance is not claimed. No hosted database, server
   certificate, HTTPS identity provider, or backup drill is on file.
2. Live sending has no authorized Engineering Contract. Delivery stays
   `NOT_SENT`.
3. Source media is short: 1 qualified slice against a daily target of 20.
   Only one market has coverage.
4. Discovery holds three stored prospects. Crawlers and enrichment
   purchases are out of scope.
5. Revenue and inventory are not recorded. Economics holds zero actuals.
6. The seven grooming models are not configured, and no authorized traffic
   exists to learn from.
7. Subscription owners, plans, costs, and renewal dates are unrecorded until
   an operator supplies billing evidence.
8. Wedding Planner Phases 2–9 are not started.
9. Ask Alpha is not yet a queued multi-tenant conversation service.
10. Scale is measured in memory and in one local batch, not on a hosted
    system.

## 9. Recommended next work

Every remaining gap needs a real-world input that the code cannot invent.

| Order | Next step | Input it needs |
| --- | --- | --- |
| 1 | Run hosted acceptance for real | A hosted PostgreSQL endpoint with `VerifyFull`, an organizational OIDC issuer, a platform secret store, and an approved backup and restore drill |
| 2 | Fill the source-media fuel gap | Licensed source clips with provenance for each target market |
| 3 | Record subscription costs | Billing evidence from the operator |
| 4 | Contract the first authorized send | An Engineering Contract naming the channel, consent, opt-out, and audit, with explicit owner authorization |
| 5 | Grow discovery within bounds | Public sources reviewed for terms of use; any paid source passes the budget controls first |
| 6 | Advance Wedding Planner Phase 2 | Its own contract, cost ceiling, and isolation tests |
| 7 | Configure learning and models | Authorized traffic and production events that justify a model |

## 10. Conclusion

The local proof chain now runs from source media to prospects, evidence,
conversation, Economics, memory, rematch, planner wake, operations, flow,
fleets, inventory, rotation, coverage, marketplace metrics, and closed
models. Each step stores what was measured and refuses what was not. The
remaining work needs real hosted infrastructure, real media, real billing
evidence, and explicit authorization before anything is sent.

This report does not authorize live sending, crawlers, enrichment purchases,
new subscriptions, new AI workers, or another phase.
