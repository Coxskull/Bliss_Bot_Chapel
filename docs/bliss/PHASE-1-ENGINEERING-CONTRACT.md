# PHASE-1-ENGINEERING-CONTRACT

## Scope

Phase 1 delivers foundation, database, domain architecture, tests, and basic API visibility.

## Non-negotiable rules

1. Never create a direct one-to-one `Creator → Advertiser` relationship. The path is `BlissMatch`.
2. Never create a permanent `ContentItem → Advertiser` relationship. Inventory stays reusable.
3. Never make `BlissMatch.CreatorId` unique.
4. Never make `CampaignPlacement.ContentItemId` unique.
5. Never treat UNKNOWN as zero. Nullable decimals remain null.
6. Never let future `RuleVersion` edits rewrite historical `BlissMatch` rows.
7. `NetworkAccess` and `ProgramAccess` are independent persisted states.
8. AI is not the authority for deterministic match arithmetic. Phase 1 stores components; it does not score.

## Layering

- Controllers return DTOs. They do not mutate domain state except through the development seeder at startup.
- EF configurations live in `Bliss.Infrastructure/Configurations` and are applied with `ApplyConfigurationsFromAssembly`.
- GUID primary keys on all domain entities.
- Foreign keys and lookup fields are indexed.
- Major relationships use `DeleteBehavior.Restrict`.

## Secrets

Connection strings are configuration-only. No Supabase passwords are committed.

## Optional table

`MatchEvaluationRun` is architectural preparation for Phase 3 historical auditability. It is not an intelligence engine.
