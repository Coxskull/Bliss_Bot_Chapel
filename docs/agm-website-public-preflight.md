# ALPHA Global Media — public website preflight (read-only)

**Workstream:** AGM-WEB-STRIPE-20261017-01  
**Status:** Engineering verification aid, not a compliance certificate  
**Intended operator:** ALPHA GLOBAL MEDIA LLC (business identity and contacts must be separately verified)

## Purpose

The October 17 website/Stripe assessment found that https://alphacreativecontent.com was inaccessible to public web checks. The authenticated GoHighLevel draft and CRM were not accessible in this chat. This script enables reproducible **GET-only** evidence against ALPHA's approved public domain once it becomes reachable. This is not legal adequacy, Stripe approval, form/CRM routing, or hosted identity acceptance.

## Safe local run

```bash
python3 scripts/agm-website-preflight.py \
  --url https://alphacreativecontent.com \
  --output /tmp/agm-public-site-preflight.json
```

- Exit **0**: homepage retrieved as HTTP 200 `text/html`; `website_status=HOMEPAGE_RETRIEVED_NEEDS_REVIEW`, **not Stripe-ready**.
- Exit **2**: inaccessible, inconclusive, or invalid input. `INSUFFICIENT_EVIDENCE` is **not proof the website is globally down** or that any policy is missing.
- Output: page title, H1, footer link count, static HTML form-type inventory, discovered same-site legal/support links, and their public GET response status. No page body, form values, hidden fields, credentials, cookies, URL query strings, or personal contact details are exported.
- Hard limits: 1 MiB per response; HTTPS-only `alphacreativecontent.com` or `www.alphacreativecontent.com`; same-host-family redirects, no traversal into external domains; no arbitrary destination.
- **Never** use POST/PUT/PATCH/DELETE, authentication, real contact testing, browser sessions, CRM queries, checkouts, Stripe APIs or payouts in this script.
- The GitHub Actions workflow runs only **offline synthetic unit tests**. It does not schedule live site access or modify any hosted system.

## Interpreting the output

| Observation | Legitimate conclusion |
|---|---|
| Policy link present and GET 200 | Public link retrievable; text and legal sufficiency **UNVERIFIED** |
| Policy link not detected | `NOT_LINKED_IN_STATIC_HTML`, **not proof a policy is absent** |
| No HTML forms | GHL forms may be loaded through JS/iframe; **not proof there are no forms** |
| Homepage inaccessible | `INSUFFICIENT_EVIDENCE`; **not universal downtime** |
| Public homepage online | Does not prove mobile layout, CRM routing, Stripe identity, consent or checkout |

## Authenticated GHL Work browser acceptance required

Open the **existing** native-builder draft in the authorized GoHighLevel Work session; do not duplicate or rebuild. Capture the exact GHL location, site/page IDs, publicly approved contact identity, actual Privacy/Terms/refund page text and live policy links. Inspect desktop and 375px mobile rendering, footer links, trackers and forms. With synthetic and explicitly approved internal test contacts only, validate each Advertiser/Creator/Contact flow from browser success state -> GHL Form Submission -> CRM Contact -> matching workflow execution -> approved internal notification. Redact personal details. Do not enable paid messaging or send unsolicited communications.

## Release decisions

Confirm the correct ALPHA GLOBAL MEDIA LLC operating entity (not Alpha Auto LLC), approved public contact details, actual customer terms and service description, Stripe Payments onboarding readiness, and separate Stripe Connect charge/transfer, refund/dispute liability and payout-country eligibility. Do not publish draft legal commitments or activate any payments without independent owner/counsel approval. Keep AGM-COMM-015 hosted security/identity/consent/commercial acceptance gates BLOCKED; `CampaignReady=false`, `Delivery=NOT_SENT`.

**Source assessment:** https://drive.google.com/file/d/1OxcJYJkxzTZvF00wGjzN0g83WNHP-GRh/view

**Scope:** Repository source and safe offline checks only. No GoHighLevel, Railway, Vercel, domain, OAuth, paid resource, payment or public page mutation.
