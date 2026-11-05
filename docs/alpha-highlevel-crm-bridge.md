# ALPHA GLOBAL MEDIA — Bliss Bot Forms → GoHighLevel CRM

Status as of 2026-11-05: **CODE/QUEUE PREPARED; CRM LIVE CREDENTIALS NOT YET CONNECTED.** Public signup URLs stay on the existing Vercel `alphacreativecontent.com` frontend. GoHighLevel is CRM destination, not the form host. No new platform, Cloudflare, paid inbound webhook, email automation, user accounts, or Railway backend changes.

## 1. Server-to-server data flow

1. Creator or advertiser reads the updated public Privacy Notice and explicitly checks processing acknowledgement. Marketing consent is a separate unchecked option.
2. The public Supabase Edge Function validates fields and creates a durable `alpha_public_intake_requests` record and atomic `alpha_public_intake_receipts` receipt. Browser gets HTTP 201 only when a receipt is independently present.
3. For **only** creator/advertiser requests bearing notice `AGM-INTEREST-2026-11-05-GHL`, an insertion trigger writes `alpha_ghl_delivery` with status `PENDING`. Privacy requests and older notice versions are excluded.
4. If the exact CRM credentials are configured **and** `ALPHA_GHL_SYNC_ENABLED=true`, the server claims one delivery (5-minute lease) and calls HighLevel `POST https://services.leadconnectorhq.com/contacts/upsert`, API Version `v3`, with the location-specific Private Integration Token. No API token reaches the frontend, GitHub or logs.
5. Response containing a valid HighLevel contact ID updates ledger `DELIVERED`; 429/5xx/network issues mark `RETRY`, 401/403/invalid-record mark `BLOCKED`; attempts capped at 5. User's Supabase submission remains received even if CRM sync is pending.
6. For an authorized operator to replay eligible failed/pending deliveries, use private Edge Function `alpha-ghl-retry` (POST, Gateway JWT verification on, **exact service-role Authorization** required; no permissive CORS), maximum 20 contacts per execution. No unauthenticated or browser replay endpoint.

## 2. Information shared with HighLevel

Only **name, email, website/channel URL, optional advertiser business name**, source (`ALPHA GLOBAL MEDIA · Creator Interest` or `ALPHA GLOBAL MEDIA · Advertiser Interest`), and location ID. **Never** free-text descriptions, location/market, privacy requests, marketing opt-in, Supabase request keys, receipts, auth tokens, or internal UUIDs. No `tags` overwrites, `dnd` mutations, campaign activation, or auto-sending messages.

**Retention:** Supabase public-intake rows/receipts and bridge ledger are deleted after 90 days. GoHighLevel CRM contacts are separate and are **not** automatically deleted by the Supabase purge. This is explicitly disclosed in the public Privacy Notice. Honor privacy deletion/access requests in both systems after verification.

## 3. Owner account-side setup (never share tokens in ChatGPT)

1. Sign in to **https://app.gohighlevel.com/** in your own browser. Switch to the **intended ALPHA-owned sub-account**. Earlier historical reference was an account named **24 AI Calls**, but verify the actual account and owner entity before using it; the name does not prove authorization to send ALPHA customer data there.
2. In that sub-account, open **Settings → Business Profile** and copy the correct **Location ID**. Do **not** use an agency or user ID.
3. Open **Settings → Private Integrations**, create one integration named **ALPHA Bliss Bot Intake**, and grant **only `contacts.write`**. Copy its location-specific Private Integration Token. Use the latest API version supported by your account; the bridge uses HighLevel Contacts Upsert Version `v3`.
4. Inspect **Automation → Workflows** and existing campaign/notification triggers in the selected sub-account. Ensure contact creation/update will **not send unsolicited email, SMS, WhatsApp or paid automations** when syncing these forms. Do not rely on the public form marketing checkbox to block unrelated HighLevel workflows.
5. Visit existing Supabase **Bliss Bot** project `bkutbglzivfdnoerigyb` → **Edge Functions → Secrets**. Add:
   - `ALPHA_GHL_LOCATION_ID` = the sub-account's Location ID
   - `ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN` = the `contacts.write` token; **never paste into chat, GitHub or the public website**
   - `ALPHA_GHL_SYNC_ENABLED` = `false` while checking the account and workflows
6. After the intended sub-account, consent text, API scope and no-outreach behavior have been verified, set `ALPHA_GHL_SYNC_ENABLED` to **`true`** using the same secret settings page. Changes become available to deployed functions without redeployment.
7. Make one **owner-authorized real/safe** test submission (do not use @example.invalid or disposable fictional identities to create HighLevel contacts) from the existing [creator form](https://alphacreativecontent.com/join-creator.html) or [advertiser form](https://alphacreativecontent.com/join-advertiser.html). Check its Supabase receipt, one `alpha_ghl_delivery` record marked `DELIVERED` and exactly one corresponding contact in the correct HighLevel sub-account. Do not activate outreach or charge-bearing automations.
8. If needed, authorized staff can invoke the protected `alpha-ghl-retry` function through a securely authenticated service-role environment; it processes at most 20 due rows and returns counts only. Do not place the service key in public scripts, tickets, or chat.

## 4. Evidence and status queries (aggregate only)

Use the connected Supabase project to run read-only SQL:

```sql
SELECT d.state, r.kind, count(*) AS requests, max(d.last_attempt_at) AS latest_attempt
FROM public.alpha_ghl_delivery d
JOIN public.alpha_public_intake_requests r ON r.id=d.request_id
GROUP BY d.state,r.kind ORDER BY r.kind,d.state;
```

No personal records should appear in GitHub issues, ChatGPT notifications or general spreadsheets. The separate `public.alpha_ghl_delivery` table is RLS enabled with no anon or authenticated grants.

## Acceptance criteria

- Both existing public forms publish updated CRM disclosure and receive HTTP 201 verified Supabase receipts; privacy requests stay in Supabase only.
- One authorized contact appears under **the correct HighLevel Location ID**, with an independent CRM contact ID matched against the service-only outbox ledger.
- Same submission retry never creates another Supabase receipt or duplicated CRM lead; no extra charges/outreach.
- HighLevel auth failure/retry states are visible to authorized operators and do not silently discard Supabase requests.
- This is **interest lead CRM routing**, not full authenticated advertiser/creator account registration. Existing Railway STOP and preview restrictions remain in place.

Official references:
- HighLevel Contacts API: https://marketplace.gohighlevel.com/docs/ghl/contacts/upsert-contact/
- HighLevel Private Integrations: https://marketplace.gohighlevel.com/docs/Authorization/PrivateIntegrationsToken/
- Locate HighLevel Location ID: https://help.gohighlevel.com/support/solutions/articles/48001204848-how-do-i-find-my-client-s-location-id-
- Supabase Edge Secrets: https://supabase.com/docs/guides/functions/secrets
