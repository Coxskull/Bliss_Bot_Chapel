# ALPHA GLOBAL MEDIA LLC — Creative Image Pilot
**Directive:** ALPHA-CREATIVE-IMAGE-PILOT-20261120  
**Owner authorization:** maximum $20 total, no renewals or automatic recharges  
**Target:** existing Bliss Bot Creative Academy + Controlled AI, Henry Ford Factory read-only QA handoff  
**State:** SOURCE IMPLEMENTED, REAL PAID GENERATION NOT EXECUTED; GLOBAL STOP ENGAGED, external delivery NOT_SENT

## Official model and variable-cost pricing (checked November 21, 2026)

Use OpenAI **gpt-image-2.5-flare**, Images API POST https://api.openai.com/v1/images/generations.
Standard costs: input text $5 / 1M tokens; input image $8 / 1M tokens; image output $30 / 1M tokens.
The response's usage object, including input_tokens_details.text_tokens and image_tokens and output_tokens, determines estimated generated usage USD. Rate is not a fixed price per image. Actual API billing may differ due adjustments and lag and must be reconciled in the API console.

- Official model: https://developers.openai.com/api/docs/models/gpt-image-2.5-flare
- Official image guide: https://developers.openai.com/api/docs/guides/image-generation
- Official pricing: https://developers.openai.com/api/docs/pricing
- OpenAI billing and prepaid: https://help.openai.com/en/articles/8264644-setting-up-and-managing-prepaid-api-billing
- OpenAI spend controls and enforcement delays: https://help.openai.com/en/articles/6614457-troubleshooting-api-usage-and-spend-limits

**Important:** Token-priced output does not expose a mathematically guaranteed maximum cost per call; provider project hard spend caps can lag. The application ledger's $2 conservative reservation and provider hard budget must BOTH be used, with one owner-only call at a time. No claim of an absolute live API $20 ceiling independent of provider billing can be made. If a firm, non-overshooting financial limit is required, stop without making calls until provider confirms enforcement guarantees.

## Existing architecture and safety

The existing Bliss ControlledAiService already has a PostgreSQL reservation ledger, unique request keys, no automatic retries, pending-review images, and NOT_SENT delivery. This PR allows OpenAI IMAGE only with explicit Runtime:ControlledAi:ImagePilotEnabled and a valid independent provider account; default OFF. The existing unmetered CreativeGeneration direct OpenAI path is intentionally blocked, even if a token is accidentally configured.

The Henry Ford Factory POC QA handoff is read-only: it verifies digest/usage evidence and returns NOT_LIVE_FACTORY_ACCEPTANCE while preserving STOP. No Sailor, public endpoint, queue, mailing, campaign activation, Stripe charge, or outside contact is created. The Factory's actual Global STOP is not independently attested by these local fixtures.

## Connected-account permission readback (2026-11-21; READ ONLY)

An authenticated browser inspection of the **Alpha Global Media LLC** OpenAI API Platform organization confirmed one accessible API project and these constraints:

- **Current project spending limit observed: $9 USD**, resetting in approximately 10 days from the inspection, with a 100%-of-$9 alert. **This is not proof that $9 remains unspent.** The approved pilot maximum remains $20 total, but existing project restrictions take precedence.
- The signed-in account was refused billing inspection with the exact error **required permission: `api.billing.read`**; organization settings editing `organization.write` was also unavailable.
- Actual payment method, prepaid balance, used/remaining funds, auto-recharge setting, and availability of `gpt-image-2.5-flare` for this project **could not be verified**.
- No payment method, model entitlement, key, image generation or spending-limit change was performed.
- A separate organization owner/billing administrator must either grant **minimum access** for billing read/model verification or inspect these details and provide an approved evidence summary. Organization-wide write access is not inherently required for the engineering operator.
- **No live paid image request is permitted yet.** A monthly project limit is not a trusted fixed remaining balance and can lag or apply to other consumers. Avoid falsely setting `ALPHA_IMAGE_PILOT_PROVIDER_LIMIT_USD=20.00` while the actual project limit remains $9; owner-side environmental assertions are not authoritative provider billing evidence.

**Remaining owner action:** organization owner confirms current billed spend / available balance / auto-recharge OFF / dedicated project model access and signed-in API key privilege, then verifies adequate headroom before single-master generation. If permissions remain unavailable, leave the pilot blocked; do not work around by increasing spend or routing through a shared key.

## Owner-only prerequisites (DO NOT post secrets)

1. Use the existing OpenAI API Platform organization. In the official developer console, confirm the model is available to the selected project and organization verification if required. The owner's action may be required to add a **payment method** or buy up to $20 prepaid API credits, **with auto-recharge OFF**. This is not ChatGPT subscription billing.
2. Use a new or existing **isolated API project with no other paid consumers** and apply a hard project spend cap of $20 where the provider supports it. Confirm there are no prior project charges/reservations counting against the same $20. A monthly project limit is not an instantaneous guarantee.
3. Store a newly scoped API key privately in a workstation environment variable or company secret store. Never paste it in a repo, GitHub issue, chat, worksheet, or screenshot. Do NOT use the already-connected Stripe sandbox for API provider billing.
4. Confirm actual Factory Global STOP is ENGAGED. The pilot runner **does not change STOP** and never has outreach credentials. Only an isolated owner-invoked image-generation request is permitted.
5. Use a current local checkout of Coxskull/Bliss_Bot_Chapel with Node.js 20+ and a directory not synced to any public drive; the default ledger lives in .private/alpha-creative-image-pilot.

Read-only, $0 dry run (NO API key needed):

~~~powershell
node scripts/alpha-creative-image-pilot.mjs --dry-run
node scripts/alpha-creative-image-pilot-qa.mjs
~~~

The dry run prints a one-master/ten-variation plan across English, Filipino, Spanish, Bahasa Melayu, Bahasa Indonesia and Thai. Fictional prop: **HARBORLIGHT DEMO, single matte midnight-blue 350 mL cold-brew can with gold lighthouse emblem**. Footer ALPHA PILOT • NOT FOR PUBLICATION. No factual benefits/ingredients/prices are claimed. The same spec is used throughout so product accuracy and brand fidelity can be checked.

When separately verified by the owner, set private process-only environment flags (shown here WITHOUT key value; set the secret via a private trusted environment):

~~~powershell
$env:ALPHA_IMAGE_PILOT_OWNER_APPROVAL='ALPHA-CREATIVE-IMAGE-PILOT-20261120'
$env:ALPHA_IMAGE_PILOT_PROVIDER_LIMIT_USD='20.00'
$env:ALPHA_IMAGE_PILOT_AUTO_RECHARGE='OFF'
$env:ALPHA_IMAGE_PILOT_SCOPE='ISOLATED_NO_DELIVERY'
$env:ALPHA_GLOBAL_STOP='ENGAGED'
# OPENAI_API_KEY must already be available in this process through private custody.
node scripts/alpha-creative-image-pilot.mjs --execute --max-new=1
~~~

**Run just the master first**. Review image and actual usage receipt; compare incurred usage to API project billing. If acceptable and budget remains, the owner can explicitly execute another ten with:

~~~powershell
node scripts/alpha-creative-image-pilot.mjs --execute --max-new=10
node scripts/alpha-creative-image-pilot-qa.mjs
~~~

A reserved/unknown result permanently blocks this ledger and retains $2 uncertainty; DO NOT force-reset the JSON, delete the lock file, or retry an ambiguous request. No recurring/scheduled API job exists.

**Per-image receipts**: model, role, language, SHA256 of prompt and private PNG, provider x-request-id, dimensions, quality, tokens, computed usage USD, runtime latency, total committed USD and remaining budget; saved atomically under .private. Treat actual API invoice reconciliation as separate. Human review rubric explicitly leaves visual/product/branding/translation scores PENDING until artwork is inspected. Bad quality cannot be reported as pass just because an image exists.

The user can feed the local ledger and actual private image directory into Alpha-Galaxy/alpha-shipping-yard-test Factory POC:

~~~sh
cd factory-poc
python3 creative_image_pilot_handoff.py --ledger /private/path/pilot-ledger.json --image-root /private/path/images
~~~

It returns only a **local hash-consistency QA receipt**, no new Factory worker, no external placement, no STOP waiver, and no release authorization. The printed output includes hashes; inspect confidentiality before sharing.

## Release decision

Commercial creative activation is a separate decision after the owner demonstrates: (a) provider account/project hard spend control and key custody; (b) actual original master + up to ten generated variations with genuine token usage and API costs; (c) independent budget reconciliation against console; (d) human visual QA on realism, can count/volume/label, brand identity, translations, speed, quality; (e) no charged unknown requests, no cross-tenant exposure, and continued Global STOP.

**Never claim generated artwork, elapsed timings, actual spend, active paid model, or commercial readiness from GitHub CI alone.**
