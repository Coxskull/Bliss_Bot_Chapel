# PHASE-4-ENGINEERING-CONTRACT

## Scope

Phase 4 delivers a **provider-neutral, controlled creator-ingestion boundary** for Bliss Bot Chapel. It establishes canonical platform identity, idempotent writes, and field-level provenance before any live “Fishing Fleet” crawler or provider adapter is introduced.

Bliss remains a standalone product and repository. Nothing from Alpha Auto is imported, referenced, merged, or reused.

## Requirements

| ID | Description |
| --- | --- |
| BLC-P4-ID-001 | A platform profile is canonically identified by normalized platform + provider-owned external profile ID. |
| BLC-P4-ID-002 | `CreatorPlatform.IdentityKey` is unique when present; legacy profiles without an external ID may remain null. |
| BLC-P4-ID-003 | The same external ID on two different platforms does not collide. |
| BLC-P4-ING-001 | `POST /api/creator-ingestions` creates or updates one canonical Creator and CreatorPlatform. |
| BLC-P4-ING-002 | `(SourceSystem, IdempotencyKey)` is unique. Replaying the same request returns the original run without duplicate creators, profiles, or provenance. |
| BLC-P4-ING-003 | A new idempotency key for an existing identity updates that canonical creator instead of creating another creator. |
| BLC-P4-ING-004 | Omitted demographics remain SQL NULL. UNKNOWN is never converted to zero. |
| BLC-P4-PROV-001 | Every accepted observation writes `DataProvenance` rows with source, collection time, and optional source URL. |
| BLC-P4-AUDIT-001 | Each accepted non-replayed ingest writes an immutable `CreatorIngestionRun` input snapshot and outcome. |
| BLC-P4-API-001 | GET all / GET by id expose ingestion audit records without exposing secrets. |
| BLC-P4-UI-001 | The standalone dashboard can submit controlled fictional observations and view ingestion history. |

## Canonical identity contract

```text
IdentityKey = UPPER(TRIM(platform)) + "::" + TRIM(externalProfileId)
```

The provider-owned ID is preserved exactly after trimming; it is not uppercased because some providers use case-sensitive identifiers. Display name, URL, country, language, audience, and demographics are mutable observations—not identity.

## Exact tasks

1. Add nullable `CreatorPlatforms.IdentityKey`, backfill existing profiles, then add a filtered unique index.
2. Add `CreatorIngestionRuns` with Restrict FK to `Creators` and unique `(SourceSystem, IdempotencyKey)`.
3. Implement validation, identity resolution, idempotent create/update, and provenance persistence.
4. Add POST/GET ingestion APIs.
5. Add a dashboard ingestion form and audit list.
6. Add automated tests and TEST-DB/API/browser evidence.

## Forbidden changes

- No Alpha Auto project, table, user, order, finance, auth, or credential reuse.
- No shared database schema with Alpha Auto.
- No live YouTube, TikTok, Instagram, podcast, affiliate, Firecrawl, Tavily, or n8n calls.
- No scraping or background crawler.
- No AI identity resolution, scoring, or authority.
- No country-specific Creator tables.
- No treating UNKNOWN as zero.
- No destructive migration or deletion of Phase 1–3 keys.
- No payments, measurement, ledger, campaign execution, or ad delivery.

## Acceptance

- Duplicate idempotency requests return the same run and creator.
- Existing canonical identity updates without duplicate Creator rows.
- Same external ID on a different platform remains a distinct identity.
- Invalid observations persist nothing.
- Existing Phase 1–3 tests remain green.
- Migration is additive and TEST-only evidence is captured.

## Evidence

`docs/bliss/PHASE-4-EVIDENCE.md` and `docs/bliss/evidence/phase4/`.
