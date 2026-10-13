# Phase 6 — Measurement lineage and operating readiness

The user authorized Phase 6 and asked us to choose the hosted platform and
recovery targets. Recommended target: Azure App Service, Microsoft Entra
External ID, Azure Database for PostgreSQL Flexible Server, 14-day retention,
RPO one hour and RTO four hours. These are proposed operational targets, not
measured hosted guarantees. No Azure resources or paid services are provisioned
by this local implementation; secure service bindings remain a deployment task.

## Measurement and reviewed research

Add workspace-scoped append-only observations attached to an immutable Phase 5
handoff. The handoff's frozen snapshot binds candidate/asset hash, confirmed
Brand DNA/reference, instructor assessment, human approvals, match/rule, slot,
and accepted quote. Subsequent brand/candidate changes do not rewrite historical
attribution. Observations capture a lineage snapshot and its SHA-256.

Kinds are LOCAL_VALIDATION, ADVERTISER_FEEDBACK, and EXTERNAL_REPORT. Required
source label, evidence text and UTC observation time document the report's
provenance. Local validation and advertiser feedback accept no traffic metrics.
External reports require an explicit unverified-report acknowledgment, a bounded
UTC reporting window and at least one nonnegative metric (impressions, clicks,
conversions); clicks cannot exceed impressions when both are supplied. These
remain UNVERIFIED_REPORT, even after research review. Missing values remain
unknown. No attribution rates, sales, revenue or ROI are inferred. Recorded
feedback or an external report does not establish application delivery; Phase 5
handoffs remain PLANNED / NOT_SENT. This API neither contacts nor fetches an
external evidence URL. Evidence is a bounded human-entered report, not imported
credentials or remote code.

Staff can record any observation and propose research. An authenticated owning
advertiser can record only ADVERTISER_FEEDBACK. Access is checked before entity
lookup. Read histories require workspace access; writes require CSRF and the
existing write rate limit. Server-resolved subjects and labels are retained.
Retry keys scope to workspace, normalized payload and subject. Exact retries
replay; changed inputs conflict. Observation and audit commit atomically.

A staff-authored learning proposal references exactly one observation and its
handoff lineage, states a hypothesis, proposed experiment and success criterion,
and starts PROPOSED. It never applies a change. Reviewer/admin authority appends
versioned ACCEPT_FOR_RESEARCH, REQUEST_REVISION or REJECT decisions with a
rationale. In hosted authentication, the reviewer must differ from the proposal
author. Stale decisions conflict; identical concurrent writes replay. Editing
requires a new proposal; history cannot be updated or deleted. Applying research
to production is explicitly refused. Review does not verify reported metrics,
activate delivery, or update matching rules, Academy references, prices or quotes.

## Operating readiness

Reuse existing production posture gates, OIDC role/tenant checks, database
VerifyFull TLS policy, encrypted persistent data-protection keys, known proxies,
backup declaration, request correlation, redacted operational events, readiness
health and fail-closed startup. Add a staff-only readiness report that separates
configuration declarations, actual database/migration checks, and pending hosted
evidence. Development exemptions must not produce hosted acceptance. No raw
connection strings, credentials, URLs or certificate paths appear in this report.
Operational event history is bounded in memory, so durable external log capture
and alert tests remain hosted requirements.

The workspace UI resumes observations/proposals and records explicit research
reviews. Staff can inspect operating readiness; advertisers cannot see the
operational report. A local recovery drill dumps and restores a fresh fictional
application database, then compares stored creative/brand lineage, observation,
proposal, review and audit records. It refuses remote hosts, touches only databases
created by the drill, protects/deletes dump files, and preserves failures.

Required checks: attribution to frozen handoff lineage; malformed reports;
ownership/roles/independent reviewers/anonymous/CSRF; exact retries and conflicts;
immutability; atomic PostgreSQL concurrency; unchanged matching/pricing;
browser history/review/readiness; migrations; database recovery; full regression.
Hosted OIDC sign-in, database TLS negotiation, durable logs/alerts, actual backup
policy, and measured recovery RPO/RTO require a separately recorded hosted check.
Live AI validation remains deferred under Phase 4; no provider is activated here.
