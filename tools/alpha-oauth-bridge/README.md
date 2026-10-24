# ALPHA-HYBRID-003 — OAuth-only staging bridge

Evidence: ALPHA-EV-20261024-OAUTH001. Status: draft PR, keyless deterministic worker, second ChatGPT account not independently connected.

## Keyless architecture

No Captain Ed25519 private key, message signature or OpenAI API key is needed for this narrow ChatGPT Work and GitHub proof. Each ChatGPT account independently authenticates to GitHub via its own OAuth connection. The GitHub Actions job uses GitHub-managed short-lived permissions (repository contents: read only). This is not anonymous access: OAuth tokens are still managed by the platform. A custom OpenAI API-powered worker added later requires provider authentication and may have costs.

- Account 1: Executive ChatGPT account connected to GitHub as Coxskull, numeric identity 119336582, confirmed with current connection.
- Bridge: a DRAFT PR titled [ALPHA-OAUTH-PILOT] in Coxskull/Bliss_Bot_Chapel.
- Account 2: Engineering ChatGPT account, must independently connect GitHub and schedule a ChatGPT Work task to respond to eligible GitHub pull request events.
- Deterministic worker: .github/workflows/alpha-oauth-bridge-pilot.yml. It only validates the trusted GitHub PR event and computes SHA-256 of one synthetic staging payload. No secrets or write permission. Output appears in GitHub job summary.

## Safety enforcement

Program verifies trusted GitHub-generated event metadata: repository, actor numeric ID/login, PR author numeric ID/login, head and base repo, target main branch, open/draft state, PR number, commit SHA, title prefix, and supported event action. The exact JSON task schema requires staging-only, SHA256_EVIDENCE, READ_ONLY, zero additional spending, size <= 256 bytes, and a maximum seven-day TTL. It rejects extra fields and untrusted sources.

This does NOT automatically establish which ChatGPT account authored a GitHub action. Distinct independently authorized GitHub identities or separate authenticated Work task evidence are required to prove both accounts participated. If both use the same GitHub user, do not claim two-account identity proof.

No automation or polling has been connected to Alpha-Galaxy/alpha-shipping-yard-test Issue #3; that issue's restrictions are preserved. Existing key-based PR #311 is untouched.

## Account 2 Work setup

1. Account 2: Settings > Plugins > GitHub > Connect, then authorize only the intended repository. Verify Work and event-trigger task availability.
2. Open Work, use instructions in ACCOUNT2-WORK-TASK.md and choose GitHub pull request opened / commit update if supported. Review the Trigger, Condition and Prompt before scheduling it.
3. This PR was created before Account 2's trigger may be set up. After its task is enabled, issue a new safe draft PR commit so a fresh authorized event occurs. Do NOT claim an old event was delivered.
4. Account 2 independently reads PR and workflow summary. It records an ACK with observed account evidence; if comment posting requires approval, it must stop for approval.
5. Account 1 independently reads actual Account 2 ACK and records VERIFIED or REJECTED. No fabricated two-account proof.
6. To stop: pause/delete Account 2 Work task and do not update the PR. Close the draft PR if necessary. Never expand repository access or bypass a rejected event.

## Local tests

Run: node --test tools/alpha-oauth-bridge/pilot.test.mjs

Live read-only worker: GitHub Actions on the pilot draft PR. Accept only a verified provider-backed run, not simulated local GitHub event fields.

Acceptance has four separate gates: GitHub Actions result; Account 2 independently receives/ACKs; Account 1 independently verifies; emergency stop demonstrated. This branch alone satisfies none of the latter three.
