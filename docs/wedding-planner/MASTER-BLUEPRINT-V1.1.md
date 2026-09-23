# Wedding Planner™ Master Blueprint — Version 1.1

Authoritative product and workforce architecture for Bliss Chapel's
AI-assisted advertiser creative and campaign planning workspace.

Status: accepted Bliss Phases 1–19 remain the matching foundation.
Wedding Planner is the next bounded subsystem. It must not redesign
deterministic Bliss matching, invent extra Bliss matching phases, or
couple to Alpha Auto.

## Product purpose

Wedding Planner allows Alpha to support many advertisers without one
human account representative or graphic designer per advertiser.

The advertiser experiences one primary representative: The Wedding
Planner. Behind that personality Alpha may operate AI workers,
deterministic services, asynchronous jobs, durable data, and human
authority.

AI may converse, ask, research, interpret, recommend, generate, revise,
organize, analyze, QA, and retrieve Alpha-approved history.

Humans retain authority to approve, reject, override, and escalate.

AI generation never equals human approval.

## What a bot means

A bot or agent is a logical AI worker role: instructions, tools,
permissions, context, responsibilities, outputs, and audit.

Logical agent count is not physical model count and is not subscription
count. Allocations are starting architecture targets. Changes must be
documented.

## System boundary

Implement as a distinct bounded module, conceptually `Bliss.WeddingPlanner`.

Wedding Planner is not Bliss matching, Fishing Fleet, Alpha Auto, GHL
CRM logic, n8n permanent memory, an image generator, a single LLM
prompt, or a mutable chatbot with no durable state.

Technical model:

GHL (acquisition) → authenticated Alpha experience → Wedding Planner UI
→ Alpha .NET API → Wedding Planner application layer → agent
orchestration plus deterministic Alpha services → n8n for async jobs
where appropriate → AI/image/research providers → PostgreSQL/Supabase
as permanent truth.

.NET enforces application authority. PostgreSQL is durable state. n8n
must not become authoritative state. AI providers are interchangeable
workers. Alpha Color Intelligence is deterministic software, not a bot.

## Departments and logical roles

1. Wedding Planner / Concierge — 1 logical AI role (customer-facing).
2. The Curator — 8 logical research roles; about 3 initial workers.
3. Brand Strategist — 1 logical AI role.
4. Art Director — 1 logical AI role.
5. Copywriter — 1 logical AI role.
6. Production Artist — 1 primary logical AI role; provider-neutral.
7. Creative Chaperone — AI-assisted plus deterministic rules.
8. QA Inspector — AI-assisted plus deterministic validation.
9. Human Escalation Steward — rules-first; dedicated model optional.
10. Intelligent router — deterministic first; AI only when needed.
11. Alpha Color Intelligence — 0 AI bots.

Authority contracts for executable agents must eventually describe role,
purpose, inputs, data, tools, actions, prohibitions, outputs, human
approval, failure behavior, audit, and version.

## Phased workforce allocation

| Phase | Focus | New logical AI | Initial executable workers |
| --- | --- | --- | --- |
| 1 | Foundation | 0 | 0 |
| 2 | Conversation + Brand DNA | 2 | 1–2 |
| 3 | Color Intelligence | 0 | 0 new; deterministic service |
| 4 | Curator | 8 | ~3 |
| 5 | Concept/prototype workshop | 4 | ~2–4 |
| 6 | Mature creative department | ~13 primary | ~5–7 |
| 7 | Chaperone, QA, escalation | 3 control | ~1–2 AI plus rules/humans |
| 8 | Bliss handshake | 0 | deterministic integration |
| 9 | Measurement/learning | 3 | ~1–2 |

Do not add these numbers into 33 independent bots. Phases are
cumulative. Reuse workers where safe.

## Domain, memory, and security

Candidate entities include Advertiser, WeddingPlannerWorkspace,
PlanningSession, ConversationMessage, Brand DNA versions, research,
concepts, assets, approvals, agent runs, QA, and human escalation.

Nothing important is silently overwritten. AI providers are not customer
memory. Alpha's database remembers.

Authority: authenticated user → authorized advertiser → authorized
workspace → authorized resource. Cross-tenant negative tests are
mandatory.

Creative state machine forbids DRAFT → CAMPAIGN READY, AI → advertiser
approved, Production Artist → campaign ready, and advertiser → Alpha QA
approved.

Phase 8 handshake is campaign-ready creative + approved Bliss match +
compatible inventory → planned placement.

Wedding Planner must not invent advertising prices. Rate ranges, quotes,
and compensation illustrations are owned by the future Bliss Economics
& Rate Intelligence Engine (`docs/economics/`). When quotes are in
scope, Wedding Planner asks that .NET service after matching identifies
compatible inventory. That engine is not part of Wedding Planner
Phases 1–9 and must not be implemented inside the current WP sequence.

## Critical phase rule

Each phase: engineering contract → implementation → automated tests →
evidence → review → acceptance. Do not auto-advance.

## Out of scope until later contracts

Phase 2 conversation AI, Color Intelligence, Curator, concepts,
multi-agent department, chaperone/QA AI, Bliss handshake, measurement
learning, GHL coupling, n8n authority, Alpha Auto, and the Economics
& Rate Intelligence Engine (separate future bounded context).
