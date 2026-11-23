# ALPHA Task #21 — Production manual support mailbox registration

**Date:** 2026-11-12, Asia/Manila. **Existing infrastructure only.** Zero new subscription or paid hosting.

## Completed provider changes

- The publicly deployed owner-approved support/disputes page stays `https://alphacreativecontent.com/support.html`. This page gives honest manual contact routes and does NOT issue automatic case numbers or support SLAs.
- In existing **Bliss Bot PRODUCTION** Supabase project `bkutbglzivfdnoerigyb`, applied migration `task21_private_manual_mailbox_metadata_20261112` to create `alpha_support_private.manual_mail_cases` and `alpha_support_private.manual_mail_case_events`. Source is `scripts/task21/production-manual-mailbox-metadata-20261112.sql`; migration guards check known production intake and Ask Alpha tables and refuse if overlapping full support-case rollout is present.
- Independent DB readback: both tables have **RLS ON**; `anon`, `authenticated` have **NO SCHEMA USAGE** and `service_role` has **NO SELECT on cases**. No REST/Edge endpoint, public form or Auth policy was granted. Owner-connector SQL (postgres privileged) is the only path.
- Store only opaque **Gmail provider message ID**, fixed support category, timestamps, internal status, random UUID and append-only status audit events. NO email addresses, subject, message text, payment data, AI prompts, client notes, contact marketing consent, or CRM copies. Status changes are transactionally audited. Registry started empty (zero existing customers imported).
- A native existing-project `pg_cron` cleanup, job ID **3**, name `alpha_task21_support_mail_90d_purge`, is **ACTIVE** daily at 03:17 UTC, deleting records older than 90 days; linked events cascade. This is separate from existing 90-day public inquiry cleanup.
- The owner-authorized hourly ChatGPT automation **ALPHA Support Inbox Watch**, ID `Automation_3d91eae400548191b0817794d38e3331`, is **ENABLED** in `Asia/Manila`; it checks only messages addressed to the existing manual ALPHA support mailbox with supported ALPHA subject types. It excludes personal/self/GitHub/synthetic emails, copies only ID/category/time with idempotent insert and internal notifications when new cases appear; no customer response or automatic payment/refund.
- Fresh readback: zero support-mail cases as of creation, because **no new qualifying customer support email was observed**. An automatically captured REAL incoming case is therefore not yet accepted; never fabricate one.

## Operational limitations / no misleading claims

1. The scheduled support inbox check is hourly, **not instant paging, 24/7 human coverage or an application-triggered transactional provider sender**. If connected accounts lose access or a run fails, case capture can pause.
2. Task21's full support-case API and staff interface from draft [PR #353](https://github.com/Coxskull/Bliss_Bot_Chapel/pull/353) remain **unmerged/staging-only**; the old migration replaces constraints that have changed in current production. Do not apply it directly as-is.
3. No verified distinct human backup, staffed support schedule, effective owner/legal approval of refund/failed-campaign/privacy policy, HighLevel opt-out acceptance, or live public customer support case acceptance. Those gates are still **OPEN**.
4. Existing Task20 paid Ask Alpha, CRM, active registrations, payments and Factory global STOP were not changed. No customer was contacted; no policy approved by inference.

**Acceptance classification:** **PASS** for durable private production manual-mailbox registry, 90-day TTL job, scheduled provider-connected monitoring. **BLOCKED** for full Task #21 staffed case operations and automated incident escalation.
