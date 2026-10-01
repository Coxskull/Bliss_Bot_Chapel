# Alpha Acquisition Architecture — Cost-Engineered Reference

**Status:** long-term architectural reference. Not an implementation
authorization.

**Supersedes:** earlier combined Wedding Planner / Fishing Fleet /
Autonomous Demo Factory blueprints, for future Engineering Contracts.

**Does not supersede:** accepted Bliss Chapel contracts, Wedding Planner
Phase 1, or Economics contracts already accepted. Those remain in force
until their own acceptance and any later contract that explicitly
changes them.

Bliss Chapel engineering continues. This document is the reference those
later contracts must satisfy. It is not permission to build every
component now.

## 1. What the architecture must satisfy

1. Maximum practical automation and acquisition capability.
2. Minimum practical recurring operating cost.
3. Measurable human and AI productivity.
4. Verified business intelligence.
5. Strong anti-duplication and anti-false-productivity controls.
6. Controlled subscription and external-service spending.

Governing rule:

- Reuse before generate.
- Software before AI.
- Cheap suitable model before expensive model.
- Spend more only after engagement.
- Cache what Alpha already knows.
- No new subscription without a capability-gap review.

## 2. Governing system

```
FISHING FLEET
  → OPPORTUNITY INTELLIGENCE
  → DECISION-MAKER & CONTACT INTELLIGENCE
  → REUSABLE MARKET + PROSPECT INTELLIGENCE
  → WEDDING PLANNER
  → CREATIVE DNA HELIX
  → SOURCE MEDIA LIBRARY
  → MEDIA SUPPLY & PRODUCTIVITY CONTROL
  → SOURCE MEDIA INTEGRITY
  → DETERMINISTIC COMPOSITION + QR + DISCLOSURE
  → PROSPECT DEMONSTRATION ENGINE
  → DYNAMIC PERSONALIZED LANDING PAGE
  → COMMUNICATION ROUTER
  → UNIFIED CONVERSATION LEDGER
  → PROSPECT ENGAGEMENT BRAIN
  → AI BUSINESS DEVELOPMENT
  → ADVERTISER DEMAND MAP
  → DEMAND-DRIVEN CREATOR DISCOVERY
  → BLISS MATCHING
  → HUMAN CLOSING AUTHORITY
```

Supporting controls sit beside that flow rather than inside one bot:
Private Creative Factory, Autonomous Demo Lab, Media Fuel Gauge,
Intelligence Cache, Reusable Creative Component Library, Factory Budget
Controller, and the Subscription & Infrastructure Ledger.

Alpha should eventually be able to discover advertisers and creators,
identify the advertising decision-maker, find a verified public business
contact route, preserve evidence and confidence, research markets, score
opportunities before expensive production, create original advertising
concepts, reuse approved source media, detect duplicate submissions,
measure source-media productivity, composite advertisements into source
video, generate real QR codes without AI, build prospect-specific
demonstrations, populate personalized landing pages, communicate through
approved channels, keep one conversation across channels, measure
prospect behavior, increase personalization only when engagement
justifies it, qualify commercial opportunities, recruit creator supply
in response to advertiser demand, match advertisers and creators through
Bliss, and escalate qualified opportunities to humans.

## 3. Primary business objective

Alpha should eventually serve large numbers of advertisers and creators
without a proportional staff of prospectors, account representatives,
researchers, designers, editors, campaign planners, qualification
clerks, landing-page builders, or outreach coordinators.

Human labor should preferentially create reusable infrastructure:
source media, libraries, automation, and better systems. One-time
production of an individual prospect campaign is the factory's job once
that factory is contracted and accepted.

Erwin's production time should preferentially create reusable
ingredients and improve Alpha infrastructure. Mark should not have to
count Drive folders to know whether source-media production is healthy,
and neither Mark nor Erwin should have to reconstruct who to contact
once Decision-Maker and Contact Intelligence are accepted.

## 4. Formal cost-engineering requirement

Cost efficiency is an engineering requirement. Every future Engineering
Contract must identify:

- which operations require AI;
- which operations can use deterministic software;
- which information can be cached;
- which assets can be reused;
- which model or provider tier is appropriate;
- expected usage cost and maximum permitted cost;
- the existing Alpha capability;
- subscription impact;
- fallback behavior;
- budget-threshold behavior.

Classify every external dependency:

| Class | Meaning |
| --- | --- |
| `FREE_SELF_HOSTED` | Ordinary software Alpha runs itself |
| `EXISTING_ALPHA_SUBSCRIPTION` | A subscription Alpha already pays for |
| `USAGE_BASED` | Pay for metered use, no new recurring seat |
| `FREE_OR_DEVELOPMENT_TIER` | Free or development tier, with a known ceiling |
| `NEW_PAID_SUBSCRIPTION` | A new recurring charge |
| `DUPLICATIVE_REJECTED` | Rejected because Alpha or an existing service already covers it |

Do not introduce a paid SaaS dependency when existing Alpha
infrastructure or inexpensive deterministic software can reliably
perform the function.

## 5. Capability-gap review

Before any new paid external service, answer in this order:

1. Can existing Alpha infrastructure do it?
2. Can .NET, PostgreSQL, FFmpeg, or ordinary software do it?
3. Can an existing Alpha subscription do it?
4. Can a free or usage-based service provide only the missing capability?
5. Only then consider a new recurring subscription.

A proposal for a new paid service must name the required capability, the
Engineering Contract, the provider, the monthly subscription price, the
usage charges, the alternatives considered, the build-or-self-host
alternative, why existing Alpha infrastructure is insufficient, the
estimated monthly production cost, the required date, and whether it is
required or optional.

Purchasing a prospecting suite, enrichment suite, personalized-video
suite, landing-page suite, chatbot suite, extra CRM, extra workflow
engine, extra video editor, or extra messaging system is not the
default. See `SUBSCRIPTION-LEDGER.md`.

Specific questions that must be answered before purchase:

- Can Alpha do it?
- Can .NET do it?
- Can PostgreSQL do it?
- Can FFmpeg do it?
- Can n8n already orchestrate it, without becoming the system of record?
- Can an existing CRM already provide it?
- Can an existing provider do it?
- Can a low-cost usage API fill only the gap?

## 6. Fishing Fleet

Fishing Fleet is the two-sided marketplace discovery infrastructure.

- Mission A: advertiser discovery.
- Mission B: creator discovery.

Discovery uses approved public and business information and stays
inexpensive. Expensive production stays selective.

### Target advertiser profile

The primary prospect is an established local or regional business with
an identifiable commercial brand, a legitimate business presence, a
plausible advertising or marketing budget, a suitable consumer audience,
a reasonable ability to purchase advertising, and a plausible benefit
from creator or social exposure.

Examples: dental offices, medical offices and clinics, pharmacies,
restaurants and restaurant groups, coffee shops, shopping centers and
malls, new- and used-car dealerships, motorcycle and moped dealerships,
auto-parts businesses, tire and service centers, real-estate companies,
gyms, beauty and aesthetic clinics, optical stores, furniture and
appliance retailers, hotels, vocational schools, and local financial or
insurance businesses.

Huge multinationals are not the default target. Informal street
vendors, hobby sellers, and businesses without an identifiable
commercial identity are not the primary target.

The niche catalog stays configurable. The existing 50-niche catalog is
preserved as data. Niches are not hardcoded. The initial markets are
Santo Domingo, Panama City, Medellín, Manila, Kuala Lumpur, and
Jakarta. Additional markets are configuration, not new code paths.

## 7. Opportunity Intelligence

Before meaningful creative spending:

discover → research lightly → score → prioritize → decide the
production investment.

Score factors may include market, industry, business legitimacy,
commercial identity, available business contact, decision-maker
availability, advertising relevance, creator inventory, demand
potential, public-information quality, geographic priority, engagement,
and production cost.

Opportunity Score is separate from Decision-Maker Confidence. Cold
prospects do not receive equal premium production.

## 8. Decision-maker and contact intelligence

Finding a business is not sufficient. The workflow is:

business found → business verified → likely buying role identified →
named person searched → person and role verified → business contact
searched → contact verified → confidence scored → outreach eligibility
→ personalized demonstration delivered.

Do not default every search to CEO. Select the role most relevant to
advertising or marketing authority for that industry, size, market, and
organizational structure. The role map is configuration.

Illustrative defaults, all overridable by that configuration:

| Kind of business | Likely buying roles |
| --- | --- |
| Dental or medical practice | Owner/practitioner, practice administrator, practice manager, marketing manager |
| Car or motorcycle dealership | Dealer principal, general manager, marketing director, digital marketing manager |
| Shopping center or mall | Marketing director, marketing manager, commercial manager |
| Restaurant or restaurant group | Owner, general manager, marketing manager, brand manager |
| Pharmacy or retail | Owner, general manager, commercial manager, marketing manager or director |

### Research and evidence

Search approved public and business sources: the official website,
management or team pages, official business profiles, public
professional profiles, press releases, business news, trade directories,
professional directories, public company announcements, and public
business contact pages.

An AI-generated name is not verified evidence. If the person cannot be
established confidently, the decision-maker status is `UNVERIFIED`.
Do not fabricate a person, title, or contact.

### Decision-maker record

Persist at least: decision maker, role, company, business id, country,
market, confidence, evidence, evidence source, last verified, business
contact, contact type, contact verification, and freshness status.

### Confidence

Confidence is its own classification:

| Confidence | Meaning |
| --- | --- |
| `HIGH` | Person and role are supported by strong current evidence, and a useful business contact exists |
| `MEDIUM` | Person and role are supported, but only a general company contact exists or the evidence is incomplete |
| `LOW` | Person or role remains uncertain |
| `UNVERIFIED` | Do not personalize outreach using the person's name or title as established fact |

Records expire. Reverification is configurable. A stale record is not
silently treated as current.

### Contact routes

Finding the person and finding the contact route are separate
operations. Preferred legitimate business routes:

- verified named business email;
- published business messaging or WhatsApp;
- company marketing address;
- company contact route directed to the department or person;
- public professional or business social contact;
- general company contact.

A missing direct personal business email does not by itself disqualify
the prospect. A verified company marketing contact remains a valid
route.

### Contact fallback

| Tier | Route |
| --- | --- |
| 1 | Named advertising decision-maker and a direct verified business contact |
| 2 | Named decision-maker and a general verified company contact |
| 3 | No verified named person, and a verified marketing or business-development contact |
| 4 | Verified general business contact only |

Personalization matches the evidence. Tier 4 outreach does not pretend
a named person was verified.

### Provider abstraction

Contact intelligence is an abstraction (`IContactIntelligenceProvider`
or the equivalent already used for provider-neutral Bliss adapters).
Possible later implementations include public web and business
research, existing Alpha capabilities, and an approved external
enrichment provider. Multiple providers are allowed only where the
economics justify them.

The existence of the interface is not a reason to buy an enrichment
subscription. Measure cost per verified decision-maker, then
contactability, response, qualified opportunity, and customer. Pay for
enrichment only when that chain justifies it.

## 9. Progressive personalization

| Level | Meaning |
| --- | --- |
| 0 | Prospect discovered |
| 1 | Opportunity passes initial screening |
| 2 | One strong personalized demonstration |
| 3 | Prospect engages; additional concepts may be generated |
| 4 | Prospect returns, asks questions, or requests revisions; increase personalization |
| 5 | Qualified commercial opportunity; human escalation where configured |

Do not automatically create four expensive demonstrations for every
cold prospect.

## 10. Prospect state machine

`DISCOVERED` → `LIGHTLY_RESEARCHED` → `OPPORTUNITY_SCORED` →
`DECISION_MAKER_RESEARCHED` → `CONTACT_VERIFIED_OR_FALLBACK` →
`RESEARCHED` → `DEMONSTRATION_PREPARED` → `OUTREACH_ELIGIBLE` →
`CONTACTED` → `LANDING_PAGE_VIEWED` → `ENGAGED` →
`ADDITIONAL_PERSONALIZATION_ELIGIBLE` → `INTEREST_DETECTED` →
`QUALIFICATION` → `QUALIFIED_OPPORTUNITY` → `HUMAN_HANDOFF` →
`NEGOTIATION` → `WON` / `LOST` / `NURTURE` / `DO_NOT_CONTACT`.

AI cannot manufacture `WON`. Authorization for terminal commercial
states stays with humans. The state machine itself is deterministic
software.

## 11. Source Media Library

The Source Media Library is a core production input. The preferred
reusable component is about 15 seconds.

Metadata to preserve: source media id, country, market, language or
context, content category, duration, orientation, resolution, host
count, content-safe zones, inventory areas, QR-safe zones, source and
provenance, approval, version, date added, duplicate fingerprint,
similarity fingerprint, and quota eligibility.

Create or acquire once. Qualify once. Reuse many times.

## 12. Media supply and productivity control

Source media is production fuel. The initial Erwin production
benchmark is **20 qualified source-media clips per working day**, and
**100 qualified clips Monday through Friday**.

Submissions are not completions. Only clips that are unique, usable,
and qualified count.

Daily Operations and Mission Control expose: daily target, submitted,
pending QA, qualified unique, rejected, duplicates, replacement
required, daily remaining, weekly target, weekly qualified, weekly
remaining, expected cumulative progress, and market coverage.

Example shape:

| Field | Example |
| --- | --- |
| Today target | 20 |
| Submitted | 24 |
| Qualified unique | 18 |
| Duplicates | 3 |
| QA rejected | 2 |
| Pending | 1 |
| Quota | 18 / 20 |
| Replacement requirement | 2 |

Mission Control shows production health without a manual file count.
Conceptual bands are healthy, below expected production, and material
source-media shortage. Thresholds are configurable. A week at 73 of 100
qualified clips is 73 percent, with 27 remaining, until configuration
says otherwise.

Media Library QA updates Daily Operations. Operational fields include
date, assigned to, daily target, submitted, qualified, duplicate,
rejected, pending, replacement required, weekly progress, market
coverage, evidence or links, status, and next action.

Erwin does not manually certify his own quota. System evidence
determines qualified completion.

### Market coverage

One hundred clips from one country do not automatically satisfy
production needs. Track at least the Dominican Republic, Panama,
Colombia, the Philippines, Malaysia, and Indonesia.

As the system matures, media demand and market shortage set Erwin's
replenishment priority. Allocation responds to actual inventory and
Fishing Fleet demand. A priority such as Panama 6, Dominican Republic
4, Colombia 4, Philippines 2, Malaysia 2, Indonesia 2 is an illustration
of that allocation, not a hardcoded quota.

## 13. Source Media Integrity

Anti-duplication is mandatory. The 20-per-day rule must not be
vulnerable to duplicate submissions.

Every incoming asset is compared with current submissions and the
historical library, using:

1. exact file fingerprint;
2. perceptual video fingerprint;
3. representative-frame similarity;
4. source and provenance comparison;
5. visual-content similarity analysis.

Rules:

- One unique qualified source asset equals one quota credit.
- Renaming, re-encoding, muting, resolution change, minor cropping, and
  minor trimming are not a new asset.
- A duplicate or near-duplicate earns zero quota credit.
- A rejected duplicate creates a replacement obligation.

Two genuinely different sections of about 15 seconds from the same
longer source may both be useful. Same source does not automatically
mean duplicate. Common source provenance is recorded, and the system
judges whether the clips are sufficiently different production
material. A configurable maximum of quota-eligible clips from one
source episode may be introduced if needed. The objective is to prevent
artificial quota inflation while keeping genuinely useful material.

Reuse by the Creative Factory is a different act. Erwin may not
resubmit the same source as new productivity. The factory is encouraged
to reuse one approved asset across many campaigns. That reuse saves
cost and does not create additional Erwin quota credits.

## 14. Deterministic video, QR, and disclosure

Routine finished demonstration:

approved source video + AI-generated ad asset + deterministic QR +
inventory coordinates + call to action + disclosure.

Render with .NET plus FFmpeg, or an equivalent deterministic renderer.
Do not use generative video for ordinary compositing.

QR codes are generated by Alpha software. Routine QR generation makes
no AI call, no image-generation call, and no per-QR subscription. For
fictitious concept demonstrations, the QR is real technology pointing
at an approved Alpha-controlled non-production destination unless
another destination is authorized. A fictitious advertisement is not
fake technology.

Mechanical QA checks that the required QR exists, decodes, points at
the correct destination, has the correct size, quiet zone, and
contrast, is not distorted, sits in a safe placement, and does not
obstruct creator content or critical copy.

Disclosure is an automated production component. Approved disclosure
versions are stored and applied by the pipeline. Disclosure does not
depend on a person remembering to insert it.

## 15. Reuse of intelligence and creative components

Research once where appropriate, then reuse. Panama City × pharmacy is
reusable market intelligence. ABC Pharmacy research is that package
plus ABC-specific verified public information. Do not pay AI again to
rediscover the same market.

The Alpha Intelligence Cache holds market intelligence, prospect facts,
creator facts, decision-maker evidence, contact intelligence, Creative
DNA, Brand DNA, approved translations, disclosures, inventory
specifications, safe zones, research, approved outreach structures, and
creative components. Freshness is part of the cache. Stale records are
reverified according to policy rather than treated as free truth.

The reusable creative-component library stores transitions, disclosure
screens, CTA treatments, QR frames, inventory masks, animations, layout
definitions, typography treatments, safe-zone definitions, and
rendering presets. Reuse the infrastructure. Do not create visual twins.

## 16. Creative DNA and quality

Same quality DNA. Different brand DNA. Reusable media does not mean
repetitive advertising.

Quality DNA to maintain: vibrant color, strong impact, saturation where
appropriate, strong contrast, realistic imagery, photorealistic people
where people are used, realistic products, crisp typography, strong
hierarchy, visual depth, professional advertising aesthetics, a clear
call to action, an integrated QR, and native inventory design.

Cost reduction must not become low-quality production.

Creative similarity rejects excessive duplication of layout, palette,
typography, model, composition, and superficial recoloring. Reuse
infrastructure. Do not reuse identity.

Wedding Planner remains the advertiser-facing planning personality
already begun in this repository. Future creative contracts consolidate
work into about six logical specialist responsibilities:

1. Curator / research.
2. Brand strategist.
3. Art director.
4. Copywriter.
5. Production creative worker.
6. Creative review / chaperone.

Six logical workers are not six API calls. Compatible operations are
consolidated where quality permits. Logical role count is not model
count and is not subscription count.

Model routing:

- Lower-cost suitable model: classification, extraction, formatting,
  routine summaries, basic translation, structured conversion, and
  basic scoring assistance.
- Higher-reasoning model: complex Creative DNA, difficult market
  synthesis, important strategy, difficult conversations, material
  exceptions, and complex creative reasoning.

Quality remains mandatory. A cheaper model that fails the quality bar
is not a saving.

## 17. Demonstration, sales room, and engagement

Prospect Demonstration Engine flow:

Fishing Fleet → Opportunity Intelligence → Decision-Maker Intelligence
→ reusable market intelligence → prospect research → Creative DNA →
ad creative → source-media selection → deterministic video composition
→ QR → disclosure → QA → personalized landing page → Communication
Router → verified or appropriate business contact.

One strong initial demonstration may be sufficient. Further
demonstrations are triggered by engagement and opportunity value.

Dynamic Personalized Landing Page Engine:

prospect id → database → verified prospect facts + creative + videos +
language + CTA + conversation = one personalized page.

ABC Pharmacy, XYZ Motors, and Bella Dental may share the same
application. Do not build a separate website per prospect.

The page should eventually allow a prospect to ask Alpha questions,
learn how Alpha works, review demonstrations, ask authorized pricing
questions, discuss creator markets, express interest, request changes,
and provide objectives, geography, audience, approximate budget, and
timing, or request a human. The page is demonstration, conversation,
qualification, and buying-signal collection. Pricing answers stay
inside authorized frameworks. Wedding Planner does not invent prices;
Economics remains the rate authority already defined in
`docs/economics/`.

Prospect Engagement Brain measures signals such as outreach delivered,
page visited, repeat visit, video started, video completion, additional
videos watched, CTA, QR interaction, AI conversation, pricing question,
campaign question, revision request, and meeting request. Those signals
drive follow-up, additional personalization, qualification, and human
escalation. The calculations are deterministic.

## 18. Communication and conversation

AI Business Development passes through communication policy, outreach
eligibility, the channel router, and an approved adapter.

Potential channels: business email, SMS, WhatsApp, business contact
forms, approved social-business messaging, and other supported business
channels.

The router respects provider rules, platform rules, jurisdiction,
suppression, opt-out, frequency, and authorization.

The Unified Conversation Ledger keeps one authoritative relationship
history. ABC Pharmacy remains one prospect when the conversation moves
from email to WhatsApp to the landing-page assistant to a human.

AI Business Development may introduce Alpha, deliver the presentation,
explain the demonstration, answer approved routine questions, discuss
authorized pricing frameworks, ask qualification questions, and record
objectives, audience, geography, approximate budget, timing, and
objections. It follows approved policy and escalates.

Humans retain contract execution, material custom pricing, binding
commitments, campaign authorization, material legal or compliance
exceptions, money movement, strategic negotiation, and configured
high-risk decisions.

Escalate when a prospect requests a human, shows strong buying intent,
crosses a budget threshold, needs custom pricing or a contract, creates
compliance uncertainty, is repeatedly misunderstood, makes a sensitive
complaint, is a high-value account, needs campaign authorization, or
when a material provider failure occurs. AI prepares a structured
handoff summary. AI cannot grant itself authority.

## 19. Advertiser demand and creator supply

The Advertiser Demand Map records market, advertiser, industry,
objective, audience, budget status, timing, required creator inventory,
existing inventory, and remaining demand.

Prospective interest is not authorized campaign budget. Keep those
states distinct.

Demand-driven creator fishing:

advertiser demand → creator inventory need → Fishing Fleet priority.

Do not recruit at random when stronger demand intelligence exists.
Acquisition then hands compatible advertisers and creators to Bliss
matching. Bliss remains the compatibility authority already implemented
in this repository. This architecture does not replace
`DeterministicRuleEvaluator`.

## 20. Deterministic services

These are software, not paid AI, whenever ordinary software can do them
reliably:

color intelligence, creative similarity, source-media fingerprinting,
duplicate detection, media quota calculation, productivity meter, media
fuel gauge, provider router, job queue, usage and cost metering, budget
policy, authorization, approval state machine, inventory specification,
QR generation, QR validation, mechanical QA, video compositing, media
catalog, cache and freshness, landing-page population, prospect state
machine, decision-maker confidence rules, contact routing, communication
routing, outreach eligibility, suppression and opt-out, engagement
calculations, human-escalation rules, demand calculations, persistence,
and audit.

Hybrid QA has three layers: deterministic QA, AI-assisted QA, and human
authority. AI is not paid to re-check a fact software can determine
exactly.

Security tests that remain mandatory as those surfaces appear:
unauthorized prospect access, cross-tenant access, unauthorized
creative approval, unauthorized campaign activation, unauthorized
pricing, unauthorized contract, unauthorized money movement, and
unauthorized publication.

## 21. Factory, lab, and budget

The Autonomous Demo Factory has two modes on the same cost rules:

- Generic factory: reusable fictitious inventory.
- Prospect engine: individualized B2B demonstrations.

Both use reusable research, reusable media, reusable components,
appropriate model routing, and deterministic software.

White Coat concurrency planning targets: normal 16 concurrent
pipelines, high 24, initial tested ceiling 32. Available concurrency
does not authorize spending. Budget, demand, provider health, and
legitimate work control production.

Vacancy-driven generic production keeps the planning target of 24
qualified private demo candidates per city × niche. Vacancy = target −
qualified. Vacancy creates work. No vacancy means no generation.

The lab may run while people are away only when legitimate work exists,
budget permits, the provider is healthy, policy permits, and reusable
solutions were checked first.

Mission Control budget controls: daily, monthly, provider, generation,
and prospect budgets. As budget tightens, raise the opportunity
threshold, reuse existing assets, reduce speculative generation, defer
low-priority work, use the appropriate lower-cost model, and preserve
resources for engaged prospects.

Measure cost per prospect discovered, decision-maker verified, contact
verified, demonstration, contacted prospect, visitor, engaged prospect,
qualification, qualified opportunity, and acquired advertiser, plus
revenue per advertiser. The factory should eventually answer what a
named prospect cost to acquire and what revenue the relationship
produced.

n8n coordinates. .NET executes high-volume deterministic work.
PostgreSQL remembers. Do not turn loops, calculations, resizing, QR,
fingerprinting, database operations, rendering steps, or state
transitions into paid workflow executions when .NET can do them.

Provider resilience: do not depend on a personal ChatGPT account or on
one consumer subscription. Also do not pay for multiple providers
merely because adapters exist. Start with the minimum provider set that
reliably satisfies the requirement. Add a paid provider only when
reliability, economics, capability, or scale requires it.

## 22. Mission Control surfaces

Factory: status, pipelines, queue, vacancies, provider health, failures,
retries.

Source media: daily target, daily qualified, weekly target, weekly
qualified, duplicates, rejected, replacement requirement, market
coverage, media-fuel percentage, assets requiring replenishment.

Cost: AI cost, provider cost, cost per demonstration, cost per verified
contact, cost per engaged prospect, cost per qualified opportunity,
budget remaining.

Advertiser acquisition: prospects discovered, opportunity scored,
decision-makers researched, decision-makers verified, contacts verified,
demonstrations, outreach, pages viewed, videos played, AI conversations,
interested, qualified, human handoffs, won / lost / nurture.

Creator acquisition: creators discovered, creators contacted, pages
viewed, demonstrations watched, applications, qualified creators,
available inventory, outstanding demand.

## 23. System of record

PostgreSQL / Supabase remains authoritative. Preserve prospects,
creators, advertisers, decision-makers, contact evidence, confidence
and freshness, market intelligence, research, source media, media
fingerprints, media productivity, creative components, Creative DNA,
Brand DNA, demonstrations, QR, landing pages, engagement, outreach,
conversations, demand, campaigns, matches, approvals, provider usage,
costs, subscription and infrastructure records, and audit history.

.NET enforces application authority. AI providers are workers, not
memory and not an authorization source. n8n is not the system of record.

## 24. Acceptance obligations for later contracts

These tests are obligations on the future contracts that implement the
named behavior. They are not in scope for this reference.

Source media:

- 20 unique usable clips yield 20 quota credits after QA.
- The same file renamed yields 0 additional credit.
- The same video re-encoded is detected as a duplicate or near-duplicate.
- The same video muted does not earn a new quota credit.
- A minor crop or resolution change is detected as a near-duplicate.
- Two materially different sections of one source episode are judged
  for actual uniqueness and are not automatically both rejected.
- A rejected clip creates a replacement requirement.
- Weekly progress updates Daily Operations and Mission Control from
  system evidence.

Decision-maker:

- Discovering a dental prospect identifies a likely buying role.
- A name that exists only as unsupported AI output stays `UNVERIFIED`.
- Official evidence plus corroborating professional evidence raises
  confidence appropriately.
- A verified person with no direct contact still has the company
  marketing-contact fallback.
- No named decision-maker means no fabricated name.
- A stale record requires reverification under the freshness policy.
- Outreach personalization matches confidence.

Cost engineering:

- Existing reusable market intelligence is reused before new research.
- An approved source video is reused before unnecessary generation.
- A required QR causes zero AI calls.
- A routine overlay uses the deterministic renderer, not a generative
  video provider.
- Simple classification uses the appropriate lower-cost model.
- A proposed new SaaS cannot be approved without a capability-gap review.
- A monthly budget approaching its threshold makes the factory more
  selective.

## 25. Final acquisition flow

Business found → opportunity scored → buying role identified →
decision-maker searched → person and role verified where possible →
business contact verified or fallback selected → market and prospect
intelligence → Creative DNA → advertisement created → qualified
reusable source media selected → video composited → real QR generated
→ disclosure inserted → personalized landing page populated → approved
channel selected → presentation delivered → engagement observed → AI
Business Development converses → interest qualified → advertiser demand
recorded → creator supply prioritized → Bliss matches → human authority
negotiates, authorizes, and closes.

## 26. Working-economy principle

Human time costs money. AI costs money. Subscriptions cost money. Do
not waste any of them.

Humans preferentially create reusable source media, reusable
infrastructure, better systems, better libraries, and better automation.

AI preferentially performs intelligence, reasoning, research synthesis,
creative generation, personalization, and conversation.

Software preferentially performs rendering, QR, fingerprinting,
duplicate detection, calculations, state, authorization, caching,
storage, routing, validation, and metering.
