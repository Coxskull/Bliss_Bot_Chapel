# AGM public site verification — manual outside-network check

**Scope:** ALPHA GLOBAL MEDIA LLC public site only. **No GHL edits or Stripe actions.**

The earlier chat runtime could not resolve ALPHA **or** unrelated control domains. A GitHub-hosted runner provides a second network vantage point; even success/failure there is not conclusive global availability and does not establish GHL editor state, legal approval, form-to-CRM routing, mobile rendering or Stripe onboarding.

## Usage (only after independent review and approved merge)

1. In `Coxskull/Bliss_Bot_Chapel`, open **Actions → AGM Public Website Manual Reachability**.
2. Select **Run workflow → main**. This is an opt-in manual action; it is not scheduled and will consume normal GitHub Actions minutes.
3. When the run finishes, inspect **Site audit result** and **Probe result** in its log. Download the **agm-public-site-preflight-<run ID>** JSON artifact, retained for **7 days**. Store the sanitized artifact in the authorized ALPHA EVIDENCE folder if required, with the GitHub run URL and commit SHA.
4. If `website_status=INSUFFICIENT_EVIDENCE`, distinguish auditor-local DNS/TLS/network observations from an actual live site problem. Do not report the website globally down or legal pages missing based on a single failed GET.
5. If homepage was retrieved, treat linked policies and static HTML form inventory as *observations only*. Verify actual legal text, GHL page and form mappings, CRM workflow and mobile UX separately in the owner-authorized existing **GoHighLevel Work browser**.

## Guardrails

- `pull_request` runs **offline tests only**. The actual public GET runs **exclusively** on manually requested `workflow_dispatch` after this workflow exists on `main`.
- No user-configurable URL, secret, scheduled monitor, proxy override, CRM access, checkout, login, POST or form submission.
- Fixed HTTPS origin `https://alphacreativecontent.com/`. The existing Python auditor scopes same-site redirects and scrubs request queries and error messages.
- The workflow deliberately does not fail on auditor exit **2** (`INSUFFICIENT_EVIDENCE`); it preserves the sanitized JSON and prints an inconclusive result. Unexpected execution errors still fail.
- Artifact and logs contain no raw HTML, visitor data, cookies, authorization headers or credentials. Do not copy live lead information into CI artifacts.
- The JSON artifact **never includes page title/H1 text** and will fetch/list only explicitly approved generic routes such as `/privacy-policy`, `/terms-of-service`, `/refund-policy` or `/contact`. Noncanonical policy-like links are flagged as `UNVERIFIED_NONCANONICAL_LINKS_PRESENT`, without their paths or content. This intentionally trades discovery coverage for privacy; inspect unusual GHL route slugs in the authenticated builder.
- GitHub Actions can consume minutes; use manual dispatch for an approved diagnostic need, not recurring polls.
- Independent human security/release review and explicit owner merge approval remain required. A green CI run is not permission to publish the GHL website or to activate Stripe.

**References:** `scripts/agm-website-preflight.py`, `docs/agm-website-public-preflight.md`, website evidence `ALPHA-EV-20261018-D6Q9TX`.
