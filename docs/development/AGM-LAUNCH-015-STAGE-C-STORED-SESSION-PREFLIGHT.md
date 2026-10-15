# AGM-LAUNCH-015 Stage C - persisted session evidence correlation (synthetic fixture)

**Tracker:** AGM-COMM-015-S03
**Deployment scope:** repository-only / explicitly test-only. No production migration, API, consent capture, payment or delivery.

## What changes
The pre-existing Stage B HMAC receipt binds a synthetic subject, workspace/session UUID, source slug and issuer. It cannot establish whether that session exists in the application database or whether the receipt predates conversation content.

Stage C introduces `AskAlphaNewSessionFixturePreflight`, read-only application logic that combines:
- a synthetically authenticated visitor principal that passes the existing Stage A issuer/session checks;
- an **actual scoped read** of `WeddingPlannerWorkspaces` and `WeddingPlannerPlanningSessions` in the supplied EF DbContext (not user-submitted session DTO data);
- stored workspace/advertiser IDs and active/open `FICTIONAL_ASK_ALPHA_WAKE` source marker;
- a signed Stage B receipt retrieved independently from isolated fixture PostgreSQL, requiring a UTC issue time within five seconds of the persisted session creation time; and
- a database query of the earliest message timestamp (no raw text reads). A message predating the receipt denies synthetic correlation.

The only matching status is `SYNTHETIC_STORED_NEW_SESSION_MATCH_HISTORY_UNVERIFIED`. **Every** result keeps `HistoricalSessionOwnershipVerified=false`, `VisitorConsentRecorded=false`, `TranscriptLinked=false`, `TranscriptExportAuthorized=false`, `PaymentAuthorized=false`, `CampaignDeliveryAuthorized=false`, `Delivery=NOT_SENT`.

## Security and test boundary
The new class is **NOT registered in Program.cs** and has **no HTTP route**. It requires an explicitly constructed `AskAlphaOwnershipFixtureStore` that rejects anything except `127.0.0.1/financial_test`; the isolated SQL fixture and keys are not production-ready. Existing Wedding Planner session creation is advertiser/staff-scoped and therefore cannot itself prove that the Ask Alpha visitor created it. Session source marker and synthetic token are only test inputs.

Regression tests persist synthetic EF workspace and session records in isolated EF InMemory provider while reading a genuine signed fixture receipt from PostgreSQL. They enforce exact workspace/advertiser/source/session/timing agreement, foreign/missing session denial, wrong visitor role, and fail closed on a retroactive message, moved creation time or changed advertiser. One anonymous/disabled-auth negative test proves early denial without connecting to PostgreSQL. CI also executes full .NET 8 Release and other regression workflows.

## Remaining Stage D work
Provision independently authenticated visitor OIDC under an explicitly authorized development/staging identity provider; mint a new session only from server-issued identity data and link the session creation/receipt atomically in one app-authoritative store. Require appropriate actual visitor control, role scope, consent text/purpose version, user-driven withdrawal, privacy retention policies and negative tests. Do NOT assign ownership retroactively from mutable Ask Alpha demonstrations, matching UUIDs or synthetic receipts.

All commercial and financial gates remain OPEN. Stage C engineering merge would **not** satisfy parent AGM-COMM-015.
