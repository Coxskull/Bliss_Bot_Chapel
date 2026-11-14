# Task #3B legal review change manifest

Source commit pinned: cdc8852c1079316e7edb00f541e32218321d2ed0 (PR349).
Review branch: review/task3b-legal-readiness-20261114.
Status: source/documentation-only proposed review packet. **No production publication**.

| Existing source / before | Review packet / after (proposed, not effective) |
|---|---|
| frontend/public/advertiser-account-privacy.html v1, vague rights link to public-inquiry-only privacy form | ADVERTISER_REGISTRATION_PRIVACY_NOTICE.md differentiates account rights, explicitly marks current route unverified, reserves contact, retention, regions, legal basis decisions |
| frontend/public/advertiser-account-terms.html v1, account-only/no-charge core, missing material conditions | ADVERTISER_REGISTRATION_TERMS.md clarifies review workflow, IP/content/suspension/security/account closure concepts; dispute/liability/jurisdiction reserved to counsel |
| Existing form + controller v1 version constants, source contract tests | NO CODE OR HTML CHANGE in this branch. Proposed review-v2 identifiers in documents are NOT live. On approval only, version-bind browser, API, effective notice/terms and receipt in a separate reviewed implementation PR |
| No field-level legal mapping in PR349 | CONSENT_DISCLOSURE_MATRIX.md lists fields, triggers, storage, open retention/legal basis/transfer questions, citations to source |
| No consolidated issue-level legal decisions | LEGAL_REVIEW_FINDINGS.md and OWNER_APPROVAL_PACKET.md inventory severity, owner decisions, counsel, hosted gates |

**Files added:** five Markdown documents and this manifest, all under docs/legal/task3b. **Files modified/deleted:** none. **Data/credentials accessed:** none. **Hosted writes:** none. **Cost:** $0 new infrastructure. **Registration/payment/outreach:** OFF / NOT AUTHORIZED.

**Validation scope:** existing PR349 legal-contract test established relative paths and pinned browser/API/form/document v1 parity at accepted source commit; this review-only PR does not alter those source files. Verify actual checks on this PR's resulting head before claiming new CI results. Link resolution in production, actual rights intake, hosted consent receipts, data recovery and legal approval remain OPEN.

**Traceability:** PR349 https://github.com/Coxskull/Bliss_Bot_Chapel/pull/349 ; issue401 https://github.com/Coxskull/Bliss_Bot_Chapel/issues/401 ; separate engineering PR421 https://github.com/Coxskull/Bliss_Bot_Chapel/pull/421 .
