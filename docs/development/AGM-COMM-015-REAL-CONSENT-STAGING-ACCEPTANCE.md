# AGM-COMM-015 - Real visitor identity and consent staging acceptance

**Recorded:** 2026-10-16  
**Parent:** [AGM-COMM-015 / GitHub #231](https://github.com/Coxskull/Bliss_Bot_Chapel/issues/231)  
**Disposition:** **BLOCKED - NO LIVE VISITOR CONSENT, NO COMMERCIAL RELEASE**  
**Scope:** an executable *acceptance plan*, not an instruction to activate a provider, migrate production, send messages, process payments or release transcripts.

## Why this package exists

The repository has successful synthetic identity, ownership, revocation, CSRF and HTTP tests. Those are useful security prerequisites, but **none authenticate a real visitor against an owner-approved hosted identity provider, prove ownership of a historic conversation, or record privacy-approved informed consent.**

Keep the separate status distinctions explicit:

| Domain | Verified today | Still missing |
| --- | --- | --- |
| CI | Automated checks on merged synthetic test PRs | Acceptance of real hosted workflow |
| HTTP | Fake-auth TestServer proves anonymous 401 / CSRF 400 / deny-only 409 | OIDC signed-token, callback, cookie, proxy, CSRF and actual-browser verification |
| Ownership | Disposable PostgreSQL fixtures and readonly EF guardrails | Approved authoritative visitor-bound production schema and isolated staging migrations |
| Consent | Synthetic opt-in/revoke ledger; real commands deny with 409 | Legal notice and user-owned real consent/withdrawal storage |
| Commercial | Delivery and finance authority denied | Separate quality, rights, financial, payment and explicit launch acceptance |

**Owner permission to develop is not an individual visitor's consent.** A release approval is not an identity signature and neither is an audit record from a synthetic test.

## Evidence submission rule (all gates)

Provide for every gate: (1) owner of the decision, (2) non-secret artifact location and revision, (3) test or review run URL, (4) reviewer independent of author, (5) accepted/rejected decision and timestamp, (6) sanitized logs and rollback proof where applicable. Link artifacts from this single issue and the existing Alpha Daily Operations **AGM-COMM-015** parent row. Never create new S## stage rows. Omit raw connection strings, passwords, certificate bytes, bearer tokens, cookie values, private transcripts, personal email or user identifiers from evidence.

### Gate IDP-01 - Actual hosted visitor issuer and client selection - BLOCKED

- **Owner/security decision required:** exact HTTPS OIDC issuer/authority, visitor client ID, redirect/callback origin, audience, scoped roles and subject claim issuer policy; segregate visitor identity from staff and advertiser authorization.
- Verify application uses **authorization-code + PKCE**, TLS and signature validation against the actual issuer keys, and that userinfo/role mapping is trustworthy and does not invent visitor role authority.
- Demonstrate invalid issuer, wrong audience, expired token, bad signature, nonce mismatch, state replay, second/ambiguous identity and unexpected role are denied.
- Record non-secret provider discovery URL and config schema, the selected sandbox tenant, and reviewer approval. Secrets only in a secret store; never GitHub source or evidence reports.
- **No approval recorded at this checkpoint. Do not set `Authentication__AskAlphaVisitorEnrollmentRequested=true` merely to make tests green.**

### Gate SESS-01 - Hosted cookie, CSRF and ingress review - BLOCKED

- Check `__Host-Bliss-Session` and `__Host-Bliss-Csrf` cookies over real HTTPS: Secure, HttpOnly, SameSite, path, domain, issuance, rotation, expiration, logout/invalidation and browser isolation.
- Review the reverse-proxy forwarded-header trust boundary (`Runtime:KnownProxies`), HTTPS termination, origin isolation, rate limits, open-redirect behavior, redirect URI and cookie replay across sessions/restarts/replicas.
- Prove anonymous request 401, missing/forged/cross-browser CSRF 400, valid authenticated request reaching default-denied controller 409; no secrets or transcript metadata in body or cache.
- The existing fake-auth TestServer evidence is **not** sufficient for this gate.

### Gate OWN-01 - Visitor-owned *new* session and PostgreSQL tenant model - BLOCKED

- Approve real `WorkspaceId`, `AdvertiserId`, newly server-generated `SessionId`, issuer and protected subject binding, immutable ownership constraints, unique scoped retry rules, audit actor, retention and deletion policy with privacy/data engineering.
- Forbid adopting pre-existing staff/advertiser/demo sessions from UUIDs, retry keys or client-supplied conversation references.
- Review schema changes and RLS/tenant boundaries **off production**; verify migration up/down in disposable and hosted isolated staging, create+ownership insert atomic rollback and multi-tenant concurrent retries.
- Require proof that foreign issuer/workspace/subject or inactive tenant is denied identically without existence disclosure.
- Do not repoint disposable `agm_j_` fixture helpers at hosted or production databases.

### Gate PRIV-01 - Legally reviewed, versioned consent copy - BLOCKED

- Privacy/legal owner must approve the exact purpose (creative preview only unless separately authorized), specific shared material, recipients, retention periods, withdrawal effect, lawful processing, deletion/export limits and user-facing notice revision.
- Existing `AGM-CONSENT-PREVIEW-V1` and fixture `AGM-CONSENT-FIXTURE-V1` are **unapproved placeholders**, not real consent text.
- Capture a genuinely voluntary, unchecked opt-in from the independently authenticated visitor. Do not derive consent from owner approval, an earlier conversation, prechecked boxes, synthetic tokens or registration alone.
- Reject silent extension of preview consent to transcript export, profiling, unsolicited advertiser/creator contact, ad send, payments or payouts.

### Gate CONS-01 - Real opt-in/withdrawal storage and lifecycle - BLOCKED

- Require server-authoritative session/issuer/subject/workspace identity, exact approved purpose/version and user-initiated opt-in; append-only audit, idempotency and collision detection.
- Authenticated withdrawal must take effect on every future read, consent-controlled content attachment and sharing. Record revocation propagation and legal-retention exceptions separately; test stale sessions, concurrent actions and retries.
- Block cross-workspace consent borrowing, cross-subject recovery and replay. Provide evidence of zero transcript/export/payment/ad authority despite a consent event unless downstream rights and commercial gates separately pass.
- **Current `GET /api/ask-alpha/visitor-consent/readiness` is observational and POST opt-in/withdraw remain HTTP 409.** Do not change this without all upstream gates approved.

### Gate HOST-01 - Independently witnessed hosted staging - BLOCKED

- Use an explicitly isolated nonproduction identity tenant, callback origin, PostgreSQL database and storage. No production migration, checkout, creator payout or advertiser outreach.
- Record redacted request/response logs for anonymous, wrong issuer, wrong audience, staff, malformed nonce, forged CSRF, cross-browser CSRF, real account opt-in, withdrawal, login-session rotation and revocation propagation.
- Complete independent security/privacy review, database rollback demonstration, key/cert/proxy inspection, backup-restoration declaration and no-secret-leak review.
- Evidence must identify exact code commit, environment, change request, test actors (pseudonymous) and security reviewer; never include live credentials or visitor transcripts.

### Gate COMM-01 - Separate commercial approval - BLOCKED

- Even after valid consent, never infer authorization for advertising sends or payments. Require creator rights, inventory/placement specification, financial settlement and taxes, sandbox payment with explicit authorization, campaign QA and human business-owner release decision.
- Until then retain `CampaignReady=false` and `Delivery=NOT_SENT`, no real charges, payouts, contact, transcript export or ad send.

## Safe sequence and commands

1. **Decision packet (no secrets):** owner/security supplies proposed issuer + visitor client ID and staging environment architecture; privacy counsel approves the actual text/purpose/version/retention/recipients. Track gate sign-offs and reviewer evidence in this single parent.
2. **Repository-only design review:** map current ASP.NET OIDC + cookie configuration in `Bliss.Api/Program.cs`, `Bliss.Api/Controllers/AuthenticationController.cs` and `Bliss.Api/Security/BlissAuthenticationOptions.cs`. Review `docs/development/AGM-LAUNCH-015-STAGE-F-AUTHORITATIVE-ENROLLMENT-READINESS.md` and `docs/development/AGM-LAUNCH-015-STAGE-I-CONSENT-BACKEND-CONTRACT.md`. No environment configuration changes before approval.
3. **Isolated developer verification:** `dotnet build BlissBotChapel.sln -c Release`; `dotnet test Bliss.Tests/Bliss.Tests.csproj -c Release --filter "FullyQualifiedName~AskAlphaVisitor"`. Run repository CI's planner/finance PostgreSQL, container and browser jobs at the **exact head**, and record each result; do not substitute a previous SHA's passing checks.
4. **Only after approved provider and privacy decision:** implement real nonproduction issuer/cookie acceptance separately. Only after approved schema and controls, implement nonproduction ownership + consent services with rollback evidence.
5. **Release decision:** collect independent gate approvals, hosted acceptance and executive authorization. A passing PR/CI, synthetic consent event, or this runbook **never** activates live consent or commercial workflows.

## Current source anchors and status

- `Bliss.Api/Controllers/AskAlphaVisitorConsentController.cs` - deliberately no storage service; denies write commands with HTTP 409.
- `Bliss.Api/Security/AskAlphaVisitorEnrollmentGate.cs` - issuer/claims preflight only, does not issue visitor sessions.
- `Bliss.Api/Security/AskAlphaVisitorClaimGate.cs` - historical claim-shape preflight only, does not prove past ownership.
- `Bliss.Infrastructure/Persistence/AskAlphaVisitorStagingEnrollment.cs` and `AskAlphaVisitorStagingConsentLedger.cs` - disposable synthetic PostgreSQL fixture guardrails only.
- `Bliss.Tests/Api/AskAlphaVisitorConsentHttpPipelineTests.cs` - in-memory HTTP security tests with fake-auth handler.
- `Bliss.Api/Runtime/ProductionPosture.cs` - checks general hosted infrastructure declarations, **not actual real consent acceptance**.

All gates above are **BLOCKED / NOT ACCEPTED as of 2026-10-16**. This file is a review-ready handoff and is not evidence of consent, independent acceptance or authorization to deploy.
