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

`/brand-intake.html` provides authenticated advertiser brand intake within the
Wedding Planner workspace. Users can save partial drafts, resume saved data,
inspect version history, and explicitly confirm brand direction. Confirmation
does not approve a creative or activate a campaign. API ownership and CSRF
checks remain authoritative. Staff can select an advertiser; advertiser accounts
use their server-bound advertiser identity.

`/production.html` resumes Creative Academy briefs from authorized workspaces,
prepares new briefs from confirmed Brand DNA and eligible references, and
retains private PNG candidate/review histories. Human instructor controls use
the review permission; the server independently enforces ownership and role.
No AI generation or campaign approval is performed by this page.

`/ai-workspace.html` requests bounded text suggestions or image drafts only when
provider configuration and usage capacity are ready. It requires explicit
context-sharing consent, preserves retry keys across reload, shows durable
outcomes, supports cancellation, and attaches image drafts to Academy only on
a human action. The current environment's paid provider is disabled; local
browser tests use an explicitly labeled HTTPS fixture, not a real model.

`/campaign-handoff.html` reviews exact candidate/assessment approvals, inventory
fit, approved Bliss matches, and accepted Economics quote lines. Authenticated
advertisers approve their own candidates; separate reviewers provide Alpha QA
and fit decisions; staff explicitly create draft campaigns and plan handoffs.
The server validates all gates. History persists after reload and revocation.
Prices are displayed from Economics. This page does not send or activate media.

`/campaign-measurement.html` links sourced observations to frozen handoff
evidence, records research proposals, and preserves independent review history.
Advertisers can submit their own feedback; staff can record local validation or
explicitly unverified external metrics and propose research. Review authority
and workspace access are enforced by the server. Operating readiness is staff
only and distinguishes live database/migration checks from configuration and
pending hosted acceptance. Blank metrics remain unknown; reviews do not promote
reported metrics to verified performance or apply matching/pricing changes.

| URL | Audience |
| --- | --- |
| `/` | Public advertisers and creators |
| `/operations` | Chapel operators |

Phase 1 public conversation is a visual preview. It does not call an AI
provider.
