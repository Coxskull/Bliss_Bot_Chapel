# Supabase live test — 2026-09-20

**Status:** PASS  
**Target:** dedicated Bliss Supabase PostgreSQL (`postgres` database on project `bkutbglzivfdnoerigyb`) via session pooler  
**API:** `Bliss.Api` at `http://127.0.0.1:5092` with `ConnectionStrings__DefaultConnection` set in the process environment (not committed)

No Alpha Auto schema, credentials, or tables were used.

## What was exercised

1. List creators, matches, and ingestion runs over HTTP against the hosted database.
2. `POST /api/creator-ingestions` for a new identity `YOUTUBE::CRT-LIVE-120022`.
3. Exact replay of the same `(sourceSystem, idempotencyKey)`.
4. Invalid negative `followers` rejected.
5. Direct PostgreSQL counts and identity uniqueness.

Raw JSON: `docs/bliss/evidence/supabase-live/`.

The same-origin dashboard at `/` loaded against this API: **API connected**, 8 matches, **7 creator profiles**, **5** creator-ingest runs. The rendered creator directory included **Supabase Live Test Creator** (PH). The ingest history listed that run first (`SUPABASE_LIVE_TEST · Created`).

## HTTP results

| Step | Result |
| --- | --- |
| `GET /api/creators` | 200; 6 existing rows, then 7 after create |
| `GET /api/bliss/matches` | 200; 8 matches (`APPROVED`, `CREATED`, `INELIGIBLE`, `REVIEW_REQUIRED`) |
| `GET /api/creator-ingestions` | 200 |
| First ingest | **201 Created**, `isReplay: false`, `outcome: CREATED`, run `35043963-87f7-45bb-bcc2-5ee73294ed0e` |
| Replay | **200 OK**, `isReplay: true`, **same run and creator IDs** |
| Negative followers | **400**, body `AudienceSize and Followers cannot be negative.` |
| `GET /swagger/v1/swagger.json` | 200 |
| `GET /` dashboard | 200 |

Creator `0a6e9b1c-22dd-450f-81b7-ddd132144f85` persisted as `Supabase Live Test Creator`, country `PH`, language `English`, platform `YOUTUBE`, external id `CRT-LIVE-120022`, followers `4200`.

## Database after the live ingest

Exact `count(*)` (not `pg_stat` estimates):

| Table | Count |
| --- | ---: |
| Creators | 7 |
| CreatorPlatforms | 6 |
| CreatorIngestionRuns | 5 |
| DataProvenances | 42 |
| BlissMatches | 8 |
| MatchEvaluationRuns | 7 |
| Duplicate non-null IdentityKey groups | 0 |
| Runs for `supabase-live-20260920T120022Z` | 1 |
| Creators named `Should Not Persist` | 0 |

Migrations present: Phase1Foundation, Phase2RuleDocument, Phase3EvaluationAudit, Phase4CreatorIngestion.

Provenance on the live creator: `Name`, `CountryCode`, `PrimaryLanguage` with `SourceType=PROVIDER_OBSERVATION`, `SourceName=SUPABASE_LIVE_TEST`, `ConfidenceLevel=HIGH`.

## Recorded dashboard run

A second, recorded pass drove the dashboard against the same hosted database:

1. Observation for `YOUTUBE::CRT-DEMO-1225` (PH / English / 8,800 followers) → toast `CREATED · YOUTUBE::CRT-DEMO-1225`.
2. Second observation for the **same** external profile ID with different values (SG / Filipino / 12,500 followers) → toast `UPDATED · YOUTUBE::CRT-DEMO-1225`.
3. Run drawer showed the canonical identity and the immutable input snapshot of the second observation.
4. Creator directory contained exactly one `Supabase Demo Creator`; overview creator profiles went 7 → 8, ingest runs 5 → 7.

Database after the recorded run: 8 creators, 7 platforms, 7 ingestion runs, **2** runs on `YOUTUBE::CRT-DEMO-1225` (`CREATED` then `UPDATED`), **1** creator named `Supabase Demo Creator`, **0** duplicate identity keys.

## Notes

- xUnit still uses EF Core InMemory. This pass is a hosted PostgreSQL/API check, not a substitute for `dotnet test`.
- Connection password is not stored in the repository.
- Do not point this Development seed/API process at Alpha Auto.
