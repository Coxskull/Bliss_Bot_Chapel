# Phase 8 OIDC Security Evidence

## Acceptance result

Phase 8 adds a provider-neutral organizational identity boundary without
introducing Bliss-managed passwords or committing identity-provider secrets.

## Implemented controls

- OIDC Authorization Code flow with PKCE
- secure, HTTP-only, same-origin session cookie
- anonymous frontend shell with protected workspace sign-in gate
- authenticated API read boundary
- operator, reviewer, viewer, and administrator role policies
- claim-derived reviewer and placement operator labels
- anti-forgery validation on every unsafe `/api` request
- API-native `401` and `403` responses without login-page redirects
- content security, framing, referrer, permissions, and MIME-sniffing headers
- production fail-closed startup when OIDC is disabled
- explicit authentication-disabled Development mode

## Automated verification

Command:

```bash
dotnet test Bliss.Tests/Bliss.Tests.csproj --no-restore --verbosity minimal
```

Result: **81 passed, 0 failed, 0 skipped**.

Dedicated tests prove:

- Development open mode is explicit and does not issue an anti-forgery token.
- Browser-submitted audit labels are replaced by authenticated claims.
- Anonymous API reads return `401` when OIDC is enabled.
- Viewer-role writes return `403`.
- Operator-role writes without an anti-forgery token are rejected.
- An operator request with the session anti-forgery token passes the security boundary.

## Browser and HTTP verification

A production-mode instance with OIDC enabled and no user session was exercised
at desktop and mobile widths.

- The operations shell remained available.
- Workspace data stayed hidden behind the SSO gate.
- The sign-in gate remained responsive.
- No severe browser-console errors were recorded.
- `GET /api/creators` returned `401`.
- Responses included Content-Security-Policy, X-Content-Type-Options, and Referrer-Policy headers.

An end-to-end identity-provider login requires the deployment's real OIDC
authority, client registration, redirect URI, and secret. No placeholder
provider credentials are treated as acceptance evidence.

## Deployment note

Production operators must configure a persistent ASP.NET Core Data Protection
key store shared by all Bliss API replicas. Without it, session cookies cannot
survive ephemeral instance replacement or be read across replicas.
