# Full Frontend Acceptance Evidence

## Delivered surface

The same-origin Bliss Chapel frontend is now an operations product rather than
a phase-oriented test console. It provides:

- an operational overview and actionable queue counts;
- creator search, canonical profile detail, platforms, content, matches, and provenance;
- controlled creator ingestion with all supported audience fields;
- match search, filters, formation, deterministic evaluation, evidence, and timeline;
- queue-first human review with auditable approve, reject, and hold decisions;
- queue-first campaign placement planning with compatible campaigns, content, and slots;
- advertiser, program, opportunity, affiliate-network, and access visibility;
- creator inventory, campaign detail, and planned-placement visibility;
- evaluation, formation, review, placement, ingestion, and provenance ledgers;
- hash-routed details, responsive navigation, keyboard focus handling, and reduced-motion support.

The application remains dependency-free HTML, CSS, and JavaScript served by
`Bliss.Api`. It does not connect a browser directly to PostgreSQL.

## Safety boundaries

- Deterministic rules remain the scoring authority.
- Human review records decisions; it does not replace or rewrite evaluation evidence.
- Placement records planning intent; it does not reserve, schedule, deliver, measure, or pay.
- Creator ingestion accepts controlled input; it does not crawl providers.
- Bliss remains separate from Alpha Auto.
- Browser-stored operator labels are not represented as authentication.

## Automated acceptance

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --no-restore --verbosity minimal
```

Result: **77 passed, 0 failed, 0 skipped**.

The frontend acceptance assertions verify the operations shell, all primary
workflow names, static assets, route fallback, and the absence of the former
`TEST ENVIRONMENT` and `TEST_OPERATOR` defaults.

## Browser acceptance

The frontend was exercised against the hosted acceptance database at desktop
and 390 px mobile widths. The following passed with no severe browser-console
errors:

- all eight product areas;
- creator, advertiser, match, campaign, and content details;
- partner, inventory, and six audit-ledger tabs;
- creator-ingestion and match-formation dialogs;
- review and placement queue forms;
- mobile match navigation and cards.

The browser run was read-only; no production-like record was submitted merely
to produce evidence.
