# Economics Phase 5 — Evidence

## Result

Phase 5 immutable quote versions, human approvals, and advertiser
outcomes are implemented and verified. Phase 6 has not started.

## Delivered

- `Quote` commercial envelope with explicit lifecycle status
- immutable `QuoteVersion` rows and parent-version lineage
- recommendation-linked `QuoteLineItem` rows
- recommendation range snapshots preserved beside quoted amounts
- exact-version `QuoteApprovalDecision` records
- `ACCEPTED`, `DECLINED`, and `NEGOTIATED` outcome history
- negotiation creates a new draft requiring fresh human approval
- source-system + idempotency-key replay for every write
- controlled APIs and a human-operated quote workflow
- EF migration `EconomicsPhase5Quotes`

Recommendation, quote, approval, advertiser response, compensation, and
settlement remain different concepts.

## Automated verification

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --verbosity minimal
```

Result: **144 passed, 0 failed, 0 skipped**.

Evidence: `evidence/phase5/tests/dotnet-test.txt`.

Dedicated proofs cover:

- explicit 225 PHP quote remains distinct from 242 PHP recommendation target
- create retries return the original quote and original version
- outcome cannot be recorded before exact-version approval
- negotiation preserves version 1 and inserts version 2
- version 1 approval does not authorize version 2
- accepted amount is recorded only after fresh version 2 approval
- quote lines retain the recommendation snapshot if source data changes
- all quote relationships use restrict delete
- no update/delete, compensation, or settlement API exists

## PostgreSQL verification

The Economics database was migrated through:

`20260923064840_EconomicsPhase5Quotes`.

The live lifecycle verified:

1. version 1 explicitly quoted `225 PHP` against recommendation
   `198 / 242 / 286 PHP`
2. a human approved version 1
3. the advertiser negotiated to `215 PHP`
4. negotiation inserted version 2 as `DRAFT`
5. a human separately approved version 2
6. the advertiser accepted version 2

Both versions, both approvals, and both outcomes coexist.

Database snapshot:
`evidence/phase5/database/quote-lifecycle.txt`.

## API snapshots

- `evidence/phase5/api/quote-history.json`
- `evidence/phase5/api/quote-replay.json`

The replay preserves the original version-1 action identifier even
after the quote advances to accepted version 2.

## Browser verification

Route: `http://127.0.0.1:5106/operations#/economics`

User-facing artifacts:

- `/opt/cursor/artifacts/economics_phase5_quote_summary.png`
- `/opt/cursor/artifacts/economics_phase5_quotes_table.png`
- `/opt/cursor/artifacts/economics_phase5_human_approved_quote_lifecycle.mp4`

The screenshots show the negotiated accepted history: version 2 at
`215 PHP`, linked recommendation `198 / 242 / 286 PHP`, two immutable
versions, two approval decisions, and two outcomes.

The recording demonstrates a separate clean flow from explicit
`225 PHP` draft to human approval to advertiser acceptance, ending on
the persisted quote row. Independent video review found all values and
transitions legible and no visible errors.

## Boundaries preserved

- quoted amount is not market-value calculation
- accepted amount is not compensation or settlement
- no hard-coded 20/80 split
- no inventory reservation or placement
- no AI/n8n quote authority
- no Wedding Planner integration
- no matching changes
- no Phase 6 work
