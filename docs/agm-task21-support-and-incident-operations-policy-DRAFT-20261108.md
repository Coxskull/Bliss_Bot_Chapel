# ALPHA GLOBAL MEDIA LLC — Task #21 Support & Incident Operations Policy

**Version:** DRAFT-2026-11-08-01  
**Status:** **CAPTAIN / FINANCE / PRIVACY-SECURITY / COUNSEL APPROVAL REQUIRED — NOT EFFECTIVE OR PUBLIC**  
**Purpose:** Launch gate for customer support, advertiser/creator disputes, failed campaigns, refunds, privacy requests and suspected security incidents.  
**Implementation:** Existing Bliss Bot public intake, protected staff dashboard, durable Supabase receipts, separate HighLevel consent bridge.  
**Operational controls:** Global STOP engaged; no payment execution, refunds, outreach, CRM activation or public promise based on this draft.

## 1. Approved contact and coverage (unfilled mandatory decisions)

| Required fact | Current evidence / pending decision |
|---|---|
| Public support form URL and official business support address | Support form exists in draft/source and staging; **public support contact not approved** |
| Primary general-support responder | One verified GENERAL_SUPPORT staging operator; **not proof of production on-duty availability** |
| Backup general-support responder | **NOT NOMINATED**; a distinct reachable human is required |
| Billing/refund review owner and backup | **NOT VERIFIED** |
| Privacy/security incident owner and backup | **NOT VERIFIED**; requires scoped access and MFA |
| Support hours and time zone | **NOT APPROVED**; do not promise 24/7 coverage |
| Internal monitored alert route with independent human ACK | **NOT APPROVED / NOT AUTOMATED** |
| Owner-approved privacy/retention and customer contact notice | **NOT APPROVED** |

Do not commence paid campaigns or publish a staffed support commitment while the required route, owners, legal review and response capability remain missing. A dashboard record or Supabase alert intent is not a delivered page or human response.

## 2. Incoming case handling: proposed operator procedure

1. Receive an existing signed/validated intake and issue its durable request ID, case ID and receipt. A failed insert must not appear as successful.
2. Determine severity **at intake** from the eight category names. General/advertiser/creator = NORMAL; billing/refund/failed campaign/privacy = HIGH; security incident = CRITICAL. Do not infer fraud or breach conclusively from a submitted allegation.
3. Route to the least-privileged role. General issues go to GENERAL_SUPPORT; billing/refund/campaign to BILLING_REVIEW; privacy to PRIVACY_REVIEW; security to SECURITY_REVIEW. Do not give general staff automatic sensitive access.
4. Human operator signs in with a verified, authorized identity. Use MFA AAL2 for sensitive roles. Claim an OPEN case atomically; a second operator cannot silently take ownership.
5. Preserve the original request and audit events. Add only necessary case notes; avoid payment card data, passwords, secrets, health records or raw incident payloads. Keep the case activity immutable and access-controlled.
6. Verify the customer's identity and case entitlement before discussing account-specific details. Never use plain email alone as authority to reveal private billing or campaign data.
7. Record **IN_REVIEW**, factual investigation steps, written response, resolution reason and authorization. Use **RESOLVED** only with evidence and an owner; **CLOSED** only after the operator's review is complete. A closed case must not remain editable.
8. Handle retention and deletion under the approved legal/privacy schedule, not an arbitrary promise. Escalate preservation obligations when there is a legitimate dispute or investigation.

## 3. Proposed response service objectives — not approved

The following are planning targets **only if real staffing coverage is established**. They are not SLAs or customer representations.

| Severity | Proposed initial internal target | Escalation |
|---|---|---|
| CRITICAL suspected active security incident | Immediate queue flag; a real staffed owner must confirm independently; proposed first human review within 1 staffed hour | Security owner and distinct backup; consider temporary containment under approved incident procedure |
| HIGH refund/billing/failed campaign/privacy | Proposed acknowledgment within 1 business day of monitored hours | Billing/Privacy owner, then backup; statutory deadlines, processor deadlines and contract rights supersede this target |
| NORMAL general/creator/advertiser support | Proposed acknowledgment within 2 business days | General owner, then backup |

**Not permitted:** promise immediate/24-hour human assistance, claim provider delivery as human ACK, invent a legal response deadline, or claim an approved service standard before staffing and policy signoff.

## 4. Suspected security and privacy incidents

- Treat reports as allegations requiring triage; do not publicly confirm a breach before technical and legal verification.
- Secure evidence, prevent unauthorized access and notify designated security/privacy responders through a **verified monitored internal channel** with timestamped delivery and a separate human ACK.
- If the primary is unavailable, invoke the documented backup within a separately owner-approved window; if both are unavailable, maintain explicit UNSERVED status and halt exposure-creating operations under the approved STOP procedure.
- Determine incident legal notification requirements by actual affected jurisdiction, regulator, contractual obligation and verified facts. No universal statutory deadline is assumed in this policy.
- Do not forward case details or personal data into generic HighLevel marketing automation.

## 5. Billing, refund, payment disputes and chargebacks

- Verify transaction owner, written order, milestone evidence, merchant of record, and applicable cancellation rights. Mere inquiry never authorizes a charge.
- No support operator may issue a refund, reverse payment, edit settlement, promise credits or cancel a creator commitment without independent finance authorization and contractual/legal basis.
- Preserve processor evidence and respond before the processor's actual deadline; do not ask a customer to surrender chargeback rights.
- Proposed refund/cancellation criteria remain in `docs/stripe/refund-cancellation-policy-CAPTAIN-REVIEW.md`, which is **NOT EFFECTIVE** until owner, finance and counsel approve it.

## 6. Failed campaigns, missed placements or creator withdrawals

- Record the signed statement of work, contracted placements, mutually accepted creative, creator obligations, schedule, service evidence and the alleged shortfall.
- Offer a substitute creator or reschedule **only with customer consent**. Do not claim guaranteed impressions or a new performance promise that was not in the contract.
- Refer financial remedies to billing/finance after checking the actual deliverable failure and contract. Do not decide a refund from an automated classification alone.
- Notify affected parties only via an approved customer communication workflow; preserve separate creator/advertiser permission boundaries.

## 7. Internal incident notification acceptance (hard technical gate)

Source-only dispatch functions in `scripts/task21/incident-escalation-contract.mjs` and `incident-escalation-dispatcher.mjs` deliberately do **not** run a provider. Before activation:
- Owner approves a real internal recipient, channel, and monitored primary + backup.
- Add a durable atomic alert lease/attempt ledger and authenticated server-only transport. Prevent duplicate sends across retries and crashes.
- Obtain a genuine provider acceptance receipt, separately verify mailbox delivery, and independently capture a human ACK. None of these facts substitutes for the others.
- Test default-off/no-egress, wrong origin, wrong role/MFA, duplicate alarm, expired lease, provider failures, wrong recipient, provider-accepted-but-ledger-failed uncertainty, backup escalation, and safe shutdown.
- Keep the existing staging alert-intent row `UNROUTED` unless a separately reviewed state machine supports audited transition; never falsify delivery.

## 8. Out-of-office and no-coverage fallback

When no verified operator/backup is on duty, label the channel **not staffed**, do not run an unattended customer-facing support commitment, keep incoming cases durably queued, preserve audit data, and escalate to the owner using the **approved** fallback when available. If required support coverage for paid operations is absent, place the affected launch step on hold until coverage exists. No AI agent may silently assign itself finance, privacy or incident-response authority.

## 9. Privacy, HighLevel and release conditions

- General/creator/advertiser support may enter a reviewed CRM bridge only with correct consent and both independent GHL enablement switches. Sensitive categories always excluded.
- Test real opt-in **and opt-out** behavior in a separately approved **sandbox HighLevel** location with automations disabled. A PostgreSQL negative claim test alone is not provider acceptance.
- Review data retention, request notice, public support contact, payment jurisdiction and applicable laws before publishing this policy.
- Validate completed source CI, protected staging browser lifecycle and independent hosted data readback; then obtain formal owner/finance/privacy/counsel signatures and a separate production release decision.

## 10. Sign-off (all pending)

- [ ] Captain: public support channel, staff primary, distinct backup, hours, target times and incident escalation accepted.
- [ ] Security/privacy lead: incident procedure, MFA roles, access, evidence preservation and jurisdiction review accepted.
- [ ] Finance: refund, chargeback, merchant responsibility and failed-campaign remedy accepted.
- [ ] Counsel: consumer rights, privacy and jurisdiction-specific language accepted.
- [ ] Engineering: app-originated alert provider receipts, independent human ACK, failover, HighLevel sandbox consent/opt-out and exact-source CI accepted.
- [ ] Owner: publishing, commercial launch and production deployment separately authorized.

**Until then: TASK #21 IN PROGRESS / COMMERCIAL LAUNCH NO-GO.**
