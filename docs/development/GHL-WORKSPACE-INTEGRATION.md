# GHL → Bliss application workspaces

The existing GHL site remains the marketing website. This change adds
`/global-media.html` to the existing Bliss application as a workspace entry page;
it does not host .NET inside GHL or connect a GitHub repository to GHL runtime.

## Route mapping

| Experience | Existing application route |
| --- | --- |
| Workspace hub | `/global-media.html` |
| Bliss Bot Chapel | `/` |
| Creative Academy | `/production.html` |
| Wedding Planner brand intake | `/brand-intake.html` |

All hub links are same-origin. Existing server authentication, ownership, CSRF
and campaign approval controls remain authoritative. Do not iframe private
workspaces or pass tokens through GHL URLs. The hub itself performs no API
requests, payment, generation or campaign activation.

## Existing GHL draft

Location `TCM0UkpGm5wxxNEPzpO1`, review site `SWrSzc95obeX0ngkqXFe`,
Native Builder page `KaY3fIhr5i0IUscJF75W` contains an ecosystem component
`agm-workspaces` with three responsive cards. It is saved as a design change;
Publish was not clicked. The builder labels `/home` Live, so the name
“UNPUBLISHED REVIEW” alone must not be treated as proof of public inaccessibility.

The GHL component's `data-app-origin` is intentionally empty. With no deployed
application, the cards show “Workspace access coming soon”, not broken launch
links. Once a reviewed HTTPS application origin exists, set that attribute to
its exact origin (no path or trailing slash), or set native GHL buttons to the
full hosted routes above. Do not point them at GitHub source-file URLs and call
that app access. GHL does not automatically sync repository changes.

## Hosting boundary

The user confirmed on 16 October 2026 that Bliss is not hosted yet. Deployment
has not been performed. Reuse the existing Dockerfile and follow the current
hosted runbook and startup gates; no Development-mode shortcut or disabled
authentication is introduced. Database, OIDC, encrypted persistent session keys,
trusted proxy and backup bindings must be supplied through approved hosting.
No new paid subscription, production database/backend or public launch is
authorized by this change. Decide hosting and approve concrete deployment
separately; do not reuse Alpha Auto's database or backend.

## Verification

Static validation checks the hub's local asset/link targets against this checkout.
No .NET/backend behavior changed. Hosted login, rendering and private API flows
remain untested until hosting is configured. Before enabling GHL launch links,
verify TLS, `/health/live`, `/health/ready`, hub rendering, all three navigation
targets and the existing sign-in/access-denial behavior at the real origin.
