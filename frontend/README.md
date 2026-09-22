# Frontend

Browser UI lives in this folder, separate from the .NET API, domain, and
database projects.

`Bliss.Api` still serves the files over HTTP. It does not own the source
layout.

## Layout

```
frontend/
  public/          Advertiser and creator Alpha Bliss Chapel experience
    index.html     served at /
    wedding-planner.css
    wedding-planner.js
    assets/
  operations/      Internal Bliss Chapel operator console
    index.html     served at /operations
    app.css
    app.js
```

## Routes

| URL | Audience |
| --- | --- |
| `/` | Public advertisers and creators |
| `/operations` | Chapel operators |

Phase 1 public conversation is a visual preview. It does not call an AI
provider.

## Wedding Planner surfaces

Public `/` and Operations `/operations` host Phases 1–9 Wedding Planner UI
served by `Bliss.Api`. Phase 9 adds a public read-only measurement/learning
panel and Operations aggregate-entry / accept-reject controls gated by
operator/admin capability. Exact labels: HUMAN-SUPPLIED AGGREGATES,
ASSOCIATION — NOT CAUSATION, ADVISORY ONLY. Exactly 3 logical roles map to
2 workers (`PERFORMANCE_ANALYSIS_V1`, `LEARNING_SYNTHESIS_V1`).
