# Phase 8 Engineering Contract — OIDC Operator Security

## Objective

Replace browser-supplied operator identity with a provider-neutral OIDC session
boundary suitable for a separately configured organizational identity provider.

## Required behavior

1. Use Authorization Code flow with PKCE and a secure same-origin session cookie.
2. Keep the OIDC client secret out of source control and accept it only through configuration.
3. Require authentication for API reads whenever authentication is enabled.
4. Require `bliss.operator` or `bliss.admin` for ingestion, match formation,
   evaluation, and placement writes.
5. Require `bliss.reviewer` or `bliss.admin` for human review decisions.
6. Derive review and placement operator labels from authenticated claims instead
   of trusting browser-submitted labels.
7. Require anti-forgery tokens for all unsafe `/api` requests when authentication is enabled.
8. Return API `401` and `403` responses instead of HTML redirects.
9. Fail closed outside Development when OIDC is not enabled.
10. Preserve an explicit authentication-disabled Development mode for local and automated testing.

## Configuration

No identity-provider values are committed. A deployment supplies:

```bash
Authentication__Enabled=true
Authentication__Authority=https://identity.example.com
Authentication__ClientId=bliss-chapel
Authentication__ClientSecret=<secret-store-value>
```

Optional claim and role mappings use the `Authentication` configuration section.
The provider redirect URI is `/signin-oidc`; the post-logout redirect URI is
`/signout-callback-oidc`.

## Explicit exclusions

- Bliss-managed passwords or account recovery
- identity-provider provisioning
- hard-coded tenant, provider, or client secrets
- authorization inferred from editable frontend labels
- changes to deterministic scoring authority
- Alpha Auto identity or data integration

## Acceptance criteria

- Anonymous users can load the shell and see the SSO gate, but protected APIs return `401`.
- A viewer can read but cannot write.
- An operator can use controlled write workflows with a valid anti-forgery token.
- A reviewer can record review decisions.
- An administrator can perform operator and reviewer actions.
- The frontend displays claim-derived identity and role-constrained controls.
- Existing Development tests remain deterministic with authentication disabled.
