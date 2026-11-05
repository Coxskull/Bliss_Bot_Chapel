# ALPHA GLOBAL MEDIA LLC — Privacy Request Handling Runbook (DRAFT)

**Status:** Operational draft for owner/legal approval, **not** an approved Privacy Policy or designation of a Data Protection Officer. Last updated 2026-11-05.

Scope: public creator/advertiser expressions of interest on `alphacreativecontent.com`; Supabase **Bliss Bot** project `bkutbglzivfdnoerigyb`; GoHighLevel **24 AI Calls** contacts for consented creator/advertiser inquiries. **Not** protected Railway marketplace accounts, payments, or all ALPHA projects.

## 1. Assignment and intake ownership

**Owner MUST designate an accountable privacy-request reviewer/DPO (as applicable) and alternate, define the monitored contact channel, and approve the response deadlines by market before marking pre-launch Task 5 complete.** Do not invent a mailbox or publish an unstaffed support email. The four-hour *ALPHA Public Intake Watch* is aggregate-only detection; it does not read, verify or reply to personal requests and is not a substitute for a human responder.

Official public entry point: https://alphacreativecontent.com/privacy-request.html . Preserve unrestricted public access, receipt-backed storage, and clear access/correction/deletion wording. Do not route privacy inquiries into GoHighLevel marketing lists or creator/advertiser CRM contact sync.

## 2. Secure private handling flow

1. **Detect:** monitor new `kind='privacy'`, `intake_status='NOT_YET_VERIFIED'` counts and oldest age in the existing service-only `public.alpha_public_intake_requests` table; inspect the message *only in an approved private operator environment*. Do not include email, name, narrative, receipt UUID or personal details in GitHub comments, ChatGPT alerts or the general pre-launch spreadsheet.
2. **Acknowledge:** reviewer privately confirms receipt using an approved communication channel after confirming the requester's contact route. Acknowledgement itself must not reveal the existence of another person's records.
3. **Verify identity proportionately:** confirm control of the subject's original email via a one-time verification step or another appropriate method before disclosure, correction, or deletion. Do not require unnecessary government IDs or sensitive data in this public form; never send passwords, tokens or full records over unsecured channels. For representatives, request narrowly tailored proof of authorization.
4. **Classify:** access, rectification, erasure/blocking, objection, portability, complaint or another lawful request. Record the nature and lawful basis in a private case ledger. Do not assume a request is invalid because the same email submitted one earlier.
5. **Locate sources:** separately check Supabase public intake/consent receipts, GoHighLevel contacts (after explicit consented sync), related audit/retention controls and any lawful downstream provider only after owner permission; scope to the verified person's records. A `DELIVERED` Supabase outbox status is *not* proof of deletion in HighLevel.
6. **Assess:** reviewer/legal owner decides entitlement, restrictions and applicable deadline according to the relevant jurisdiction. Preserve only legally necessary evidence, and ensure data held to defend a legal claim is not deleted prematurely.
7. **Act:** execute correction, export or deletion in *each applicable system* using privileged, audited tools. For HighLevel, verify the exact contact identity and account before modification. Don't automate a deletion from a public unauthenticated form submission. Capture service-side acknowledgement or readback per provider without publishing personal identifiers.
8. **Confirm:** privately tell the requester what was completed, what was retained under an identified lawful ground, and how to follow up. Store only minimum case evidence and closure timestamp in a staff-only system. Close the intake status only after provider verification; do not treat mere submission as a completed data-rights action.

## 3. Repeat requests and idempotency

Distinct privacy requests from the same email **must be independently receipted**, e.g. a new request for access months after an earlier correction request. On the other hand, a network retry using the **same `request_key` and identical contents** must return the same receipt and not create another record.

The database migration `20261105_alpha_privacy_repeat_requests.sql` changes the former universal unique `(kind,email)` index into a **partial unique index only for `creator` and `advertiser`**. This retains single first-interest intake per role and does not change the per-request UUID constraint, rate limits, RLS, receipt trigger, marketing opt-in or GoHighLevel eligibility rules. Distinct privacy inquiries generate no HighLevel delivery outbox row.

## 4. Retention and cross-provider reconciliation

- Supabase intake/receipt/CRM outbox are currently subject to a **daily approximately 90-day cleanup**. This does *not* automatically delete any separate HighLevel contact, any protected Railway account, or platform-managed logs.
- An unresolved request nearing the scheduled purge requires an owner-approved evidence-preservation and retention decision consistent with law. The existing automated cleanup should not be mistaken for a full request-closure workflow.
- Where legally required, handle production and backup copies according to provider retention and restoration controls. Record limitations transparently.
- Avoid claiming HighLevel follows the Supabase 90-day retention automatically. The [public notice](https://alphacreativecontent.com/alpha-privacy.html) currently discloses separate CRM retention.

## 5. Release acceptance checklist (owner sign-off, not assumed complete)

- [ ] Written owner/legal approval of public Privacy Notice and Terms for applicable markets.
- [ ] Named accountable DPO/reviewer, alternate and actively monitored response channel.
- [ ] Documented privacy request deadlines, identity-check and escalation procedures for each relevant market.
- [ ] Two distinct synthetic privacy requests with identical email can create two **different** receipts, while a same-key retry returns its original receipt.
- [ ] Privacy submissions never create HighLevel contacts or outbox rows.
- [ ] Creator/advertiser `(kind,email)` uniqueness and GHL duplicate/upsert controls remain intact.
- [ ] One *authorized staff-only* dual-provider correction/delete drill is proven with safe test identities (no deletion of genuine data without review).
- [ ] Cron and open-case retention interplay explicitly reviewed.

## Primary regulator references (Philippines)

- [Republic Act 10173, Data Privacy Act — National Privacy Commission](https://privacy.gov.ph/Data-Privacy-Act/)
- [Implementing Rules and Regulations, Section 26 — National Privacy Commission](https://privacy.gov.ph/implementing-rules-regulations-data-privacy-act-2012/)
- [NPC Advisory 2021-01 on data subject rights and identity verification](https://www.privacy.gov.ph/wp-content/uploads/2021/02/NPC-Advisory-2021-01-FINAL.pdf)
- [Data subject rights — National Privacy Commission](https://privacy.gov.ph/data-subject-rights/)

This runbook is operational preparation, not legal advice or a substitute for owner/counsel review.
