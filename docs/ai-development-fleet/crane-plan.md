# Crane plan

Bounded missions. High-risk work needs explicit approval (`security-findings.md`, finance, auth, destructive migrations).

Risk: 🟢 low · 🟡 medium · 🔴 high

## Parallel workstreams (safe now)

| Stream | Risk | May do without Mission 002 approval |
| --- | --- | --- |
| Docs / matrices / diagrams | 🟢 | Yes |
| Additional Bliss tests that do not change domain | 🟢 | Yes |
| Secret rotation (human ops) | 🔴 process | **Required** but is ops, not a code crane in Bliss |
| n8n export if credentials exist outside git | 🟢 analysis | Yes |

Do **not** in parallel: live payments, auth redesign, dropping tables, merging Auto+Bliss schemas.

---

## CRANE 0 — Secret incident response (Alpha Auto)

| Field | Content |
| --- | --- |
| MISSION | Rotate leaked DB/JWT secrets; stop using committed appsettings secrets |
| INPUT | Operator access to Supabase/Railway/GitHub |
| OUTPUT | Rotated credentials; env-based config; confirmation no WhatsApp dump of secrets |
| DEPENDENCIES | None |
| FILES | Alpha Auto `appsettings.json` (those repos, not Bliss) |
| DATABASE | Password rotation; no schema drop |
| TESTS | Smoke login after rotation |
| SECURITY | 🔴 |
| PARALLEL | Isolated from Bliss |
| MERGE ORDER | Immediate ops; then code PR removing secrets |
| ACCEPTANCE | Secrets gone from default branch; apps still boot via env vars |
| CAN RUN IN PARALLEL? | Yes vs Bliss docs |

## CRANE 1 — Bliss verification tests

| Field | Content |
| --- | --- |
| MISSION | Add WebApplicationFactory and/or Testcontainers proofs |
| INPUT | Phase 1 contract |
| OUTPUT | Tests proving HTTP 200 of seed-like graph |
| DEPENDENCIES | P1 |
| FILES | `Bliss.Tests` only |
| DATABASE | Testcontainers or InMemory; not production |
| SECURITY | 🟢 |
| PARALLEL | Yes |
| MERGE ORDER | Anytime |
| ACCEPTANCE | CI green; no domain rename |

## CRANE 2 — Phase 2 controlled test data (only after Phase 2 **contract**)

| Field | Content |
| --- | --- |
| MISSION | Expand fictional graph without engines |
| INPUT | Phase 2 engineering contract |
| OUTPUT | More creators/opportunities/slots; still no scoring |
| DEPENDENCIES | Contract + P1 |
| FILES | `Phase1DataSeeder` or Phase2 seeder |
| DATABASE | Additive seed; no destructive migration |
| SECURITY | 🟡 |
| PARALLEL | After contract only |
| MERGE ORDER | After Crane 1 optional |
| ACCEPTANCE | Tests for cardinality; no Chaperone logic |

## CRANE 3 — RuleVersion contract (schema only)

| Field | Content |
| --- | --- |
| MISSION | Document JSON for eligibility + weights; store on RuleVersion without executing |
| INPUT | Product rules list |
| OUTPUT | Versioned document; historical matches still point at old version |
| DEPENDENCIES | P1 |
| DATABASE | Additive nullable column possibly |
| SECURITY | 🟡 |
| PARALLEL | Yes vs Fishing Fleet **adapters** after identity doc |
| ACCEPTANCE | No scores computed; immutability tests still pass |

## CRANE 4 — Chaperone (deterministic)

| Field | Content |
| --- | --- |
| MISSION | Write EligibilityCheck results APPROVED/REVIEW_REQUIRED/INELIGIBLE + reason codes |
| INPUT | Crane 3 document |
| OUTPUT | Deterministic function + tests |
| DEPENDENCIES | Test data + rules |
| SECURITY | 🟡 |
| PARALLEL | With Officiant **only if** both consume same RuleVersion without coupling |
| MERGE ORDER | Before campaign auto-placement |
| ACCEPTANCE | Golden tests for GEO_NOT_ELIGIBLE etc.; no AI |

## CRANE 5 — Officiant (deterministic scoring)

| Field | Content |
| --- | --- |
| MISSION | Fill MatchScoreComponent; OverallScore vs ConfidenceScore |
| INPUT | Crane 3 |
| OUTPUT | Stored components; AI not authority |
| DEPENDENCIES | Crane 3 |
| SECURITY | 🟡 |
| MERGE ORDER | After or parallel Crane 4 with contract |
| ACCEPTANCE | Replay same inputs → same scores; history unchanged |

## CRANE 6 — Fishing Fleet ingest

| Field | Content |
| --- | --- |
| MISSION | Provider adapters → Creator/Platform/Provenance |
| INPUT | Identity contract |
| OUTPUT | Ingest API or jobs |
| DEPENDENCIES | Canonical IDs |
| SECURITY | 🟡 |
| PARALLEL | After identity contract; not before |
| ACCEPTANCE | Global schema; country as data; dedup tests |
| MERGE ORDER | After identity; can parallel UI |

## CRANE 7 — Campaign engine extension

| Field | Content |
| --- | --- |
| MISSION | Additive FKs: Campaign↔Opportunity/Match; statuses; dates |
| INPUT | Do not fork a second Campaign table |
| OUTPUT | APIs + tests |
| DEPENDENCIES | Human review policy |
| DATABASE | Additive migration 🟡 |
| SECURITY | 🟡 |
| MERGE ORDER | After chaperone if auto-eligibility used |
| ACCEPTANCE | Placement still many-to-one content; no 1:1 advertiser lock |

## CRANE 8 — Ad delivery / measurement / ledger

| Field | Content |
| --- | --- |
| MISSION | Separate cranes later: renderer, events, ledger |
| DEPENDENCIES | Campaign |
| SECURITY | measurement 🟡; ledger/payout 🔴 |
| PARALLEL | Renderer vs measurement after event contract |
| MERGE ORDER | Ledger last; payouts E-blocked |
| ACCEPTANCE | Provider adapters; Alpha ledger owns meaning |

## Merge order (summary)

0 (ops) → 1 (tests) → 2 (data, contracted) → 3 (rules doc) → 4/5 (engines) → 6 (fleet) → 7 (campaign) → 8 (delivery/measure/ledger)

---

## AI change report for Mission 001 (this PR)

| Area | Result |
| --- | --- |
| FILES CREATED | `docs/ai-development-fleet/*` |
| FILES MODIFIED | `README.md` (index link) |
| FILES DELETED | None |
| DATABASE CHANGES | None |
| API CHANGES | None |
| CONFIGURATION CHANGES | None |
| TESTS ADDED/MODIFIED | None |
| COMMANDS RUN | `dotnet build`, `dotnet test`, `gh repo view`, read-only clones under `/tmp/alpha-audit` |
| TEST RESULTS | Bliss 23/23 passed |
| ASSUMPTIONS | Public GitHub clones equal “Alpha”; hosted n8n not in git |
| KNOWN LIMITATIONS | Alpha Auto not compiled; Copilot/ChatGPT not in this environment |
| SECURITY | Critical leaked secrets documented, values redacted |
| FOLLOW-UP | Mission 002 proposal — **do not start until approved** |
