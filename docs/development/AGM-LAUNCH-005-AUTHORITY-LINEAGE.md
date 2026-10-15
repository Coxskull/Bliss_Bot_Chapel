# AGM-LAUNCH-005-GAP-001 — campaign authority separation

This development milestone distinguishes controlled fictional parties from
unverified intake before creating planned campaign placements. It does not
close real creator inventory, advertiser, creative-rights, or research-to-match
authorization requirements.

Creators, advertiser opportunities and campaigns now default to `UNVERIFIED`.
Only `UNVERIFIED`, `VERIFIED`, and `SYNTHETIC_FIXTURE` are stored; PostgreSQL
check constraints reject other values. Known Phase 1/2 fixture IDs are explicitly
classified by the migration and seeders. Other existing rows remain unverified.

Placement planning requires all three classifications to agree. Unverified and
mixed synthetic/verified parties are refused. The queue exposes each party's
classification and `AUTHORITY_REQUIRED`, `SYNTHETIC_ONLY`, or `VERIFIED_READY`.
The placement input snapshot records the classifications and `NOT_SENT`.
Creative handoff-created campaigns inherit the stored opportunity classification.

No HTTP request can set these classifications. This increment does not add a
verification endpoint. `VERIFIED` is a stored precondition, not proof of a
reviewed evidence package. A separate reviewed implementation must bind creator
inventory consent, advertiser authority and creative usage rights to immutable
evidence before real-party activation. Existing match approvals and scores do
not grant those rights. Research intake and seeded matches remain distinct until
an explicit, evidence-backed mapping is implemented.

Verification includes synthetic planning, unverified/mixed refusal, campaign
classification mismatch, replay, creative handoff regression, and a disposable
PostgreSQL fresh/upgrade migration drill with check-constraint rejection. The
drill downgrades and upgrades only its newly created fixture database.

Status: development milestone submitted for review; GAP-001 remains open for
explicit research mapping and real-party authority evidence. No publication,
outreach, campaign activation, deployment or paid provider calls are authorized.
