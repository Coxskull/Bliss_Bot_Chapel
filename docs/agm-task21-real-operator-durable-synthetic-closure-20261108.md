# ALPHA GLOBAL MEDIA LLC — Task #21 verified-operator synthetic closure

**Date:** 2026-11-08 · **Environment:** Supabase development branch `alpha-first-five-staging` (`wdlmcriajgsretgyusmo`)  
**Scope:** one isolated, fictional GENERAL_SUPPORT case; no actual customer, campaign, payment, CRM delivery, refund or production change.  
**Global STOP:** engaged. **Dashboard case-update switch:** OFF. **PR #353:** draft/unmerged.

## Accepted engineering milestone

The user previously authenticated to the existing Vercel Authentication-protected preview and demonstrated both (1) the real `GENERAL_SUPPORT` queue and (2) a read-only fictional case-detail/history panel. The supplied staging Auth UUID was independently matched to the authorized operator's existing Auth account, confirmed email and one verified TOTP factor, and enrolled **only as GENERAL_SUPPORT** in the existing non-exposed `alpha_support_private.staging_operator_grants` table. Sensitive case roles remain ungranted.

1. **Real role-bound rollback rehearsal:** Executed the actual `public.alpha_support_claim` and `public.alpha_support_transition` functions via actual restricted `SET ROLE service_role`, with this user as database actor, for the existing OPEN fictional general case `931d5569-9958-4603-b3c4-fdad25c1acd6`. Verified initial claim, idempotent repeat claim, `IN_REVIEW`, `RESOLVED`, `CLOSED`, **5 audit events**, real Auth user existence, and blocked CRM row. A real `ROLLBACK` restored the original fixture with one event and no owner. **Database result:** `TASK21_REAL_VERIFIED_OPERATOR_SYNTHETIC_LIFECYCLE_ROLLBACK_PASS`.
2. Wrote a safely guarded [single-use, reproducible staging SQL exercise](../scripts/task21/staging-verified-operator-closure-receipt.sql). Source reads the operator's UUID **at runtime from the non-exposed granted staging DB**, not from GitHub source or client input. It requires exactly the pre-existing two staging cases, one private operator grant, one unrouted critical alert, and a distinct fixed fictional request key; it aborts on data drift. It atomically blocks the generated outbox entry as `BLOCKED/invalid_record` **before COMMIT**. No live GHL sender has been installed in staging, and the only deployed operator Edge remains read-only.
3. Trial-ran that **exact script's write path in a PostgreSQL `BEGIN`/`ROLLBACK`** with a separate SQL postcondition verifying the database was unchanged. **Database result:** `TASK21_DURABLE_TEST_SOURCE_STAGING_ROLLBACK_PASS`.
4. Rechecked 55 existing staging migration entries, exactly two original synthetic cases, original unread critical alert, three original Auth accounts, one authorized GENERAL_SUPPORT grant, and no prior request with the exercise key. Only then ran the guarded source **once** against the existing development branch and committed that single fictional transaction.
5. The transaction created one **new** fictional `general/NORMAL` support case, auto-created its intake receipt and immutable creation event, performed first-owner claim and idempotent duplicate claim, then moved through `IN_REVIEW`, `RESOLVED` and `CLOSED`. Its only CRM outbox entry was blocked within the same transaction.

## Actual permanent staging receipt — PASS

- **New synthetic CLOSED case ID:** `922fdedc-31a1-4dd6-9dd0-31772694922c`
- Category `general`, severity `NORMAL`, assigned queue `GENERAL_SUPPORT`, final state **`CLOSED`**
- Owner: the existing, previously verified operator from the private DB grant; actual user UUID intentionally omitted from this public GitHub report
- Resolved at timestamp present; one intake receipt; **exactly five immutable case audit events**; repeat self-claim produced no duplicate
- Generated fictional CRM outbox entry: **`BLOCKED`**; contact address `@example.invalid`, marketing opt-out; no customer contacted
- No critical alert intent was created for the normal fictional case; the pre-existing fictional critical security alert remains **`UNROUTED`**
- The earlier fictional OPEN GENERAL_SUPPORT case `931d5569-9958-4603-b3c4-fdad25c1acd6` was preserved and still appears in the dashboard's open queue; the critical security case remains OPEN and unassigned
- No database migration was necessary; migration history remains **55**

### Independent second readback

| Aggregate | Result |
| --- | ---: |
| Total staging support cases | 3 |
| OPEN cases | 2 |
| CLOSED cases | 1 |
| Immutable case events | 7 (1 + 1 + 5) |
| Intake receipts | 3 |
| Fictional CRM outbox entries `BLOCKED` | 2 |
| Unrouted fictional critical alert intents | 1 |
| Existing Supabase Auth users | 3 |
| Enabled least-privilege GENERAL_SUPPORT grants | 1 |
| Previous OPEN/general fixture intact | Yes |
| Previous OPEN/security fixture intact | Yes |
| New CLOSED case assigned to authorized operator | Yes |
| New CLOSED case history events | 5 |
| Other owned OPEN cases | 0 |

## Remaining acceptance gates

**Important distinction:** This was an authenticated-actor-backed **database stored-procedure acceptance** executed through a trusted staging database connection. It is **not** an actual user clicking `Claim` or `Resolve` through the browser's disabled update form, and therefore the genuine end-to-end browser mutation gate remains **PENDING**. Do not enable broad `ALPHA_SUPPORT_MUTATIONS_ENABLED` on the existing shared staging branch without a separately scoped release with rapid stop/revocation. No new client role, sensitive queue, producer API credential, or unrestricted case mutation was installed.

A low-risk next browser acceptance step is available **without any deployment or mutation**: in the currently protected staff dashboard's **"Find existing case by exact ID (including closed cases)"**, enter `922fdedc-31a1-4dd6-9dd0-31772694922c`. The restricted `GET ?case_id=...` API should read the closed fictional case with its owner and five history entries despite the active open-queue filter. Verify by a redacted screenshot, do not copy browser tokens or secrets.

Still outstanding: application-triggered internal incident alert delivery, independent primary/backup staffed acknowledgement and hours/SLA evidence, policy sign-off for refunds/disputes/failed campaigns/privacy/security, signed staging-to-production release acceptance, and sandbox HighLevel consent/opt-out tests. The genuine earlier manually sent Gmail test was acknowledged, but the application alert remains unrouted.

**Formal Task #21 status:** IN PROGRESS; production unchanged and Global STOP engaged.


## Follow-up: protected browser lookup of CLOSED case — PASS

The actual staging operator has now submitted a further screenshot from the existing **Vercel Authentication-protected Support Operations preview**, after entering the new fictional closed case's exact ID into **"Find existing case by exact ID (including closed cases)"** and choosing **"Look up authorized case."** The browser displayed **"Private case record loaded. No customer was contacted."** The screenshot was reviewed in the engineering conversation; it is not copied to public GitHub.

**Human-browser acceptance independently confirmed:**
- Case `922fdedc-31a1-4dd6-9dd0-31772694922c` retrieved through role-scoped **GENERAL_SUPPORT** despite being absent from the OPEN-only queue.
- Status **CLOSED**, severity NORMAL, category general, scope GENERAL_SUPPORT, verified operator ownership, and the clearly labeled fictional requester `@example.invalid` and synthetic resolution summary.
- **Exactly five historical entries**, displayed newest-first: CLOSED/RESOLUTION_RECORDED; RESOLVED/RESOLUTION_RECORDED; IN_REVIEW/STATUS_CHANGED; TRIAGED/ASSIGNED; OPEN/CREATED. No missing or extra events on the user-visible page.
- **"Case updates not enabled"** remains displayed. The page was read-only; this browser interaction did not claim, update, email, refund, or contact anyone.
- Independent hosted Supabase read-only query after the screenshot confirmed: status CLOSED, a non-null owner and resolved timestamp, five events, one intake receipt, and synthetic HighLevel outbox still **BLOCKED**.

**PASS — real authenticated human browser CLOSED-case exact-ID retrieval and full audit-history viewing.** This is now accepted separately from the earlier real restricted-database stored-procedure claim/closure test. **PENDING:** a user-triggered browser POST and a production-safe support notification/primary+backup response service. Do not enable the broad mutation gate from this read-only screenshot. Global STOP remains engaged, preview only, no actual customer data or spending.
