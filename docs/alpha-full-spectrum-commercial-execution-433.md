# Directive 433 — commercial matching implementation status

This work continues draft PR #356 and preserves the budget-first matcher, sliding-scale quotation rules, creator compensation safeguards and six-market currency support.

## Implemented on the PR branch

- The public advertiser form collects structured budget, market, currency, objective, media formats, optional audience requirements, languages and separate contact consent.
- Server-side validation binds the six launch markets to DOP, PAB, COP, PHP, MYR and IDR, normalizes market labels, checks currency precision and rejects missing consent or unsupported field values.
- The additive migration `20261108130000_alpha_full_spectrum_advertiser_intake.sql` adds nullable structured fields plus consent metadata. Existing records remain unchanged; the new fields become mandatory only for new-version advertiser submissions.
- The intake function persists the structured fields and includes them in its request-key duplicate comparison. The GHL queue trigger now accepts only the new notice version and explicit contact consent.
- CRM delivery maps the seven structured advertiser fields only when all seven HighLevel field IDs are configured. Missing or duplicate IDs fail closed. CRM credentials and field IDs are not included in source.
- The existing quote engine still requires current verified audience, availability, geography, price, tax, accepted compensation and margin evidence. Synthetic acceptance remains separate from live acceptance.

## Live database findings — 2026-11-08

Read-only inspection of the active Bliss Bot Supabase project found:

- `alpha_public_intake_requests` has 5 existing rows: 3 advertiser and 2 creator inquiries, all using the 2026-11-05 GHL notice. The existing CRM outbox marks all five as delivered.
- The live table has no structured budget fields or separate `contact_consent` field, and its notice-version constraint does not include the new contract yet. The migration is therefore not applied.
- The old inquiry rows cannot be treated as consent evidence for this new follow-up scope. The separate contact-consent record did not exist; existing marketing preference is not substituted for it.
- Intake, receipt and GHL outbox tables have RLS enabled with no public policies. The Edge Function uses a server-side service-role credential; no direct public table access was granted.
- The existing marketplace contains 8 creator records, 7 platform records, 8 availability-flagged inventory slots and 6 placements. Only 2 of 8 creator profiles have audience size, geography and language all populated; none of the 6 placements has an active/open status. All 58 provenance observations are older than 30 days. The current schema has no placement price, tax or agreed creator-compensation fields.
- These rows do not establish verified available inventory or a customer-ready quote. No customer quotation is eligible from this evidence.

## Remaining release blockers

- The migration and Edge Function are staged on the draft PR but not applied or deployed. No production migration, form release or merge occurred.
- No connected HighLevel tool is available to inspect the account, credentials or custom-field identifiers. The seven environment-variable mappings are implemented, but the identifiers and delivery settings remain unverified.
- The existing advertiser-opportunity records are not linked to intake IDs or CRM contacts. No authorized isolated intake-to-CRM-to-opportunity record has been persisted.
- No current inventory has verified availability, audience evidence, market fit, agreed compensation, price and tax terms. No real quotation has been produced.
- No live provider call, external contact, supplier commitment, charge or additional spend occurred.

## Release gates

1. Review and apply the migration together with the matching Edge Function and notice/form version, in an isolated or otherwise authorized environment.
2. Verify the HighLevel custom-field IDs, credentials, duplicate/opt-out behavior and inquiry-only contact workflow without exposing secrets.
3. Implement and prove an idempotent intake → CRM contact → existing opportunity mapping using an authorized isolated record.
4. Refresh creator and placement evidence, then issue a quotation only if the full cost and compensation safeguards pass.
5. Keep production form release, merge, and deployment gated until those checks pass.

Factory STOP, no-outreach, no-paid-supplier-call and no-transaction boundaries remain unchanged. No additional spending was incurred.
