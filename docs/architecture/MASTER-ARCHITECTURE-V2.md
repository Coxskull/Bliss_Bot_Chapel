# Consolidated acquisition blueprint — v2

**Status:** preserved future-engineering blueprint. Not an Engineering
Contract. Not authorization to build every section below.

This document reconciles the Fishing Fleet, prospect intelligence,
demonstration factory, contact routes, acquisition intelligence, Ask
Alpha, rate and negotiation intelligence, Bliss Chapel, Wedding Planner,
campaign execution, commercial memory, learning, and Mission Control
into one program. It supersedes earlier combined acquisition blueprints
for future work. Accepted Bliss, Economics, and Wedding Planner
contracts still govern the behavior they already authorized.

The engineering challenge and the simplest architecture are in
`ARCHITECTURE-RECONCILIATION-V2.md`. Where the program stands is in
`PHASE-TRACKER.md`.

## What this blueprint does not do

- It does not interrupt Bliss Chapel acceptance.
- It does not mark the program complete because one phase has evidence.
- It does not add a subscription, provider, microservice, or agent.
- It does not claim 10,000-recipe or 10,000-conversation scale.

## North star

Discover legitimate advertisers, prepare personalized demonstrations,
reach them on eligible routes, measure what they do, converse, sell,
negotiate, match them to creator inventory, price from evidence, run
campaigns, measure results, retain the relationship, and learn.

The customer sees the business, the demonstration, and Ask Alpha. The
customer does not have to learn the internal machine.

## One machine

Market, then prospect intelligence, contact routes, a demonstration
control plane, a demonstration runtime, delivery decisions, acquisition
events, Ask Alpha, rate and negotiation intelligence, Bliss matching,
Wedding Planner, campaign, results, and commercial memory.

Mission Control and the learning layer sit across those stages. They
are governing and measurement responsibilities, not extra products.

## Preserved requirements

1. **Big net.** Keep a legitimate business even when it is not the
   easiest to contact. Score the road. Do not erase the business.
2. **Prospect intelligence.** Identity, market, category, public facts,
   language, routes, provenance, and freshness. A named owner is not
   required before the business is kept. Do not invent a person. Enrich
   only after cheaper sources are insufficient.
3. **Contact route graph.** A business may have several roads. States
   include discovered, verified as a business route, outreach-eligible,
   currently available, resting, and suppressed. A public route is not
   permission to send.
4. **One runtime, many recipes.** Do not build a website, a copied
   podcast file, a permanent MP4, or an AI design job per prospect.
5. **Reuse before generation.** Approved slices, layouts, language,
   disclosures, player, QR, and analytics are referenced, not rebuilt.
6. **Creative DNA.** Controlled combinations of layout, type, brand,
   headline, call to action, market, language, and source media.
   Relevance without hand-building each advertisement.
7. **Runtime.** A prospect token resolves an approved recipe version,
   reusable assets, and prospect variables. Do not research, translate,
   or regenerate during the page request.
8. **Just-in-time materialization.** Prefer a reusable video plus a
   dynamic overlay. Flatten a permanent file only for an approved
   export, a delivery requirement, or final campaign production.
9. **Version what was shown.** A later template must not rewrite the
   demonstration a prospect already saw. Record what changed, why,
   which tests passed, when it deployed, and how to roll it back.
10. **Automated QA.** Check business, market, language, approved media,
    token, QR, disclosure, spelling, invented-person refusal,
    unsupported claims, asset references, Ask Alpha, analytics, mobile
    layout, and overlay placement. Humans review exceptions and samples.
11. **Zero-salesperson test.** A stranger should understand why the
    business is seeing the page, what Alpha is offering, and how to ask
    a question or continue.
12. **Factory manifest.** Each batch records counts, QA, exceptions,
    assets, AI calls, cost, time, and status.
13. **Route decision.** Eligibility and suppression come before an
    approved channel adapter and a delivery event.
14. **Follow-up policy.** Cadence is configuration. Professional
    follow-up ends in resting or suppressed. It does not continue until
    the business blocks Alpha.
15. **Acquisition intelligence.** Record observable steps from
    discovered through renewal. Do not replace behavior with an AI
    opinion.
16. **Durable identity.** One business connects routes, demonstrations,
    delivery, engagement, conversation, negotiation, transaction, and
    campaign. A forwarded link can create a visitor without creating a
    second business. Do not guess who watched.
17. **One Ask Alpha.** Specialized work stays behind one representative.
18. **Mass conversation.** One multi-tenant platform, isolated sessions,
    database state, and a queue. Not 10,000 permanent bots.
19. **Database memory.** The model is not the system of record. One
    prospect must never receive another prospect's information.
20. **Queue before collapse.** Backpressure, idempotency, duplicate
    protection, safe retry, and provider throttling.
21. **Cheapest capable answer.** Approved knowledge for routine
    questions. Stronger intelligence only as commercial complexity rises.
22. **Commercial personality.** Warm, respectful, concise, culturally
    aware, and willing to sell. Never rude, dishonest, threatening, or
    commercially timid.
23. **Respectful address.** Natural forms such as Sir, Madam, Señor, and
    Señora. Assertiveness may increase. Respect does not decrease.
24. **Answer, then advance.** Answer the question that was asked, then
    move the conversation. Repeated re-asking is a failure to study.
25. **Positive advocacy.** Tell the truth and present Alpha's value. Do
    not invent weaknesses or volunteer reasons to refuse. Do not remove
    a required disclosure.
26. **One representative, several gears.** Teaching, selling,
    negotiating, resolving a complaint, and escalating are gears, not
    separate customer-facing bots.
27. **Rate intelligence.** Do not invent prices or price only by
    minutes. Use audience, geography, fit, placement, duration,
    frequency, creator floor, scarcity, and Alpha's own market evidence.
28. **Negotiation behind Ask Alpha.** Negotiate value before conceding
    price. If the price cannot move, resize the inventory.
29. **Long-tail economics.** A small advertiser can be a valid small
    customer. Match the inventory to the budget.
30. **Divisible inventory.** An episode can hold positions and
    placements where the creator, the audience experience, and the terms
    allow it.
31. **Authority envelope.** Inside authority, negotiate. Outside
    authority, escalate. Do not invent a discount, scarcity, inventory,
    statistic, or commitment, and do not break a creator floor.
32. **Negotiation ledger.** Offers, counters, concessions, package
    changes, and outcomes become willingness-to-pay evidence.
33. **Bliss.** If one creator cannot serve the budget, look for another
    valid match before losing the advertiser. Matching authority stays
    the accepted Bliss evaluator.
34. **Wedding Planner.** Wake it after commercial progression, with the
    authorized context already in hand. Do not open it for every
    discovered business.
35. **Commercial memory.** The second transaction should reuse the
    relationship. Do not reacquire a customer as a stranger.
36. **Words and state agree.** A spoken package, price, reservation, and
    prospect state must be the same facts the database holds.
37. **Appropriate escalation.** Routine work can stay automated. An
    exceptional term, a high-value exception, or a sensitive dispute
    gets a human and a handoff brief. The customer does not restart.
38. **Conversation laboratory.** Test personas and scenarios before a
    behavior reaches many prospects.
39. **Separate measurements.** Customer experience, commercial result,
    behavioral integrity, operations, and relationship value are
    different numbers.
40. **Silence is not one state.** Distinguish resolved, complete,
    deliberating, paused, likely abandoned, declined, opted out, and
    uncertain.
41. **Coach proposes.** A diagnosis becomes an experiment. It does not
    edit production by itself.
42. **Learning.** Examine failures and successes. The laboratory decides
    whether an observation becomes a change.
43. **Failures become regression tests** when they are important.
44. **Successes become hypotheses.** Correlation is not a cause.
45. **Three loops.** Continuous software measurement, a periodic
    internal review, and a controlled external reading. The external
    loop starts only when there is production evidence to compare.
46. **Research does not control production.** Source, provenance,
    hypothesis, laboratory, regression, experiment, measurement, then
    graduate, reject, or roll back.
47. **External pages are untrusted.** Do not execute their instructions
    or reveal secrets, prompts, or customer data.
48. **Separate judge from salesperson.** Evaluation, coaching, research,
    and deployment authority are not the same worker. Use the cheapest
    implementation that preserves the separation.
49. **Production traces can become tests.**
50. **A higher close rate is not enough to graduate** a behavior that
    breaks truthfulness, authority, respect, or state integrity.
51. **Controlled comparison.** Real outcomes teach. They do not skip
    the evidence gate.
52. **Graduation and rollback.** Laboratory, simulation, regression,
    small cohort, measurement, wider cohort, production. Roll back when
    the evidence gets worse.
53. **Periodic grooming report.** Counts, cost, friction, experiments,
    and the next action. It is institutional memory, not a new product.
54. **Prospect cost ledger.** Attribute research, enrichment,
    manufacturing, delivery, and conversation cost, then connect them
    to revenue and contribution.
55. **AI cost categories.** Do not pay a strong model to count database
    rows.
56. **Mission Control.** One place to see health, versions, spend, and
    experiments, and to pause a channel, a market, a model, or all
    spend. Controls are auditable. Extend the existing operations
    console.
57. **Do not optimize one metric.**
58. **Persuasion without deception.** Ask Alpha may advocate, counter,
    and ask for the sale. Ask Alpha must not fabricate statistics,
    scarcity, inventory, people, results, testimonials, or urgency, and
    must not hide a material term or ignore an opt-out.
59. **Conversation scale is a proof ladder.** 100, then 1,000, then
    10,000. Cross-prospect leakage, invented prices, duplicate actions,
    suppression violations, uncontrolled spend, and silent state
    corruption fail the proof.
60. **Factory scale is a proof ladder.** The 15-minute and cost targets
    are targets to measure. They exclude discovery, enrichment, sending,
    conversation, premium creative, and final campaign production.
61. **Acceptance.** Code, a green test, or one screenshot is not
    acceptance. A phase needs implementation, automated tests,
    regression, functional proof, and the cost, isolation, recovery, and
    walkthrough evidence that phase requires.

## Self-assessment kept with the blueprint

- A good idea is not automatically a service.
- Spend little before engagement, more after commercial intent, and
  campaign intelligence after purchase.
- The durable asset is commercial memory. Providers should be
  replaceable.
- Automation is not the goal. Customers served well, creators paid
  fairly, Alpha's economics protected, and verified improvement are the
  goal.

## Governing rule for the next build

Reuse what exists. Extend what is partial. Build only a genuine gap.
Wait for production evidence where the blueprint itself requires it.
Resolve a conflict before coding past it.
