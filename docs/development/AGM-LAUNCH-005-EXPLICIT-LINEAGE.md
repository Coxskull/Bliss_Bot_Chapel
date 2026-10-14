# AGM-LAUNCH-005-GAP-001 — selected local lineage evidence

The read-only builder correlates explicitly selected creator observation intake,
match-formation and planned-placement audit IDs. It refuses seeded matches with
no formation audit, a separate creator intake, wrong advertiser scope, mixed or
unverified classifications, missing exact intake provenance, inconsistent graph
or statuses, and intake recorded after formation began.

It reuses the existing creator ingestion, formation, rule evaluation, placement
and provenance records. No schema, production seed, matching algorithm, financial
operation or campaign mutation is added. There is no route or DI registration.
Its grant is a trusted caller policy, not an authenticated HTTP endpoint.
Only synthetic records and named disposable loopback PostgreSQL fixtures (or
the in-memory test provider) are supported. Relational reads use a dedicated
repeatable-read, read-only transaction; pending tracked mutations are refused.

The exported report carries exact selected record IDs and SHA256 fingerprints
of stored source snapshots/provenance. It omits raw creator names, URLs and
payloads. Repeated reads of unchanged records yield the same bytes and digest.
The digest is correlation, not a signature or independent verification. The
report explicitly denies research-truth, consent, creative-rights, payment,
activation and independent-QA acceptance; delivery remains NOT_SENT.

This increment proves selected stored graph correlation in a local fixture.
It does not prove the observation was used by scoring, external research truth,
economic research promotion, real-party rights, or source audit immutability.
Legacy provenance links are exact generated note text, not a foreign key.
Changes to source records produce different fingerprints, not tamper-proof
history. The report is exported evidence, not a persisted approved mapping.

GAP-001 remains open for durable evidence-bound mapping review, real inventory
consent, advertiser and creative usage authority. This local rehearsal is
submitted for independent review; no campaign activation or publication follows.
