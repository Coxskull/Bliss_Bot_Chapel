# Directive 433 — commercial matching implementation status

This work continues draft PR #356 and preserves its full-spectrum marketplace guidance.

## Implemented in this branch

- Advertiser inquiry captures an all-in budget, currency, market, objective, preferred formats, audience needs, language preferences, and one-time contact consent.
- Supported launch markets and local currencies: Santo Domingo/DOP, Panama City/PAB, Medellín/COP, Manila/PHP, Kuala Lumpur/MYR, Jakarta/IDR.
- A server-side budget evaluation module validates budget precision, current evidence, market currency, verified availability, audience geography, inventory terms, and accepted supplier compensation.
- Quote calculation itemizes creator compensation, Alpha fee, payment processing, fulfillment, taxes, advertiser total, and contribution against a versioned minimum-margin policy.
- An over-budget preferred placement can be paired with a separately scoped verified lower-cost placement. The engine does not reduce the original agreement's compensation.
- Synthetic acceptance fixtures exercise consented intake → CRM contact fixture → pending opportunity fixture → inventory evaluation → sustainable quote → affordable alternative. The fixtures use `example.invalid`, synthetic evidence labels, and no provider calls.

## Live integration status and release blockers

The workflow harness proves the decision path with injected synthetic adapters. It does not establish live CRM or marketplace writes.

- The deployed `alpha_public_intake_requests` table has no budget, currency, objective, media format, audience requirement, language, or one-time contact-consent columns. The active intake Edge Function also does not accept the new notice version or persist those fields. The current public form therefore must not be released with the new notice until a schema and Edge Function update land together.
- HighLevel custom-field IDs for the seven structured advertiser fields are not present in the configured source. Contact sync must remain disabled for these fields until the authorized field IDs are mapped and consent-gated.
- No live mapping from this intake table to the existing marketplace opportunity record has been established. The synthetic adapter is not that mapping.
- Existing marketplace tables include inventory rows, but the current evidence review did not establish a specific verified, available supplier quote with accepted terms and compensation. Do not produce customer quotations from counts or synthetic fixtures.
- No production deployment, live intake, CRM contact, customer quote, campaign, payment, or outbound communication was performed.

## Required next release sequence

1. Add and review the additive Supabase schema migration for the advertiser fields and consent record.
2. Update the existing `alpha-public-intake` function in place, preserving its current Ask Alpha action and CRM outbox guards.
3. Configure and verify the HighLevel custom-field mappings and connect consented advertiser requests to the existing CRM contact and opportunity workflow.
4. Bind the matching engine to current verified inventory and supplier terms; then validate real quotes and alternative scopes against the approved minimum-margin policy.
5. Run hosted acceptance in an isolated staging environment before considering any production release.

No additional spending or external contact is authorized by this work.
