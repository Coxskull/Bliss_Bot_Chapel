# ALPHA Hybrid AI Fleet — isolated signed-task pilot

**Status: code-only staging pilot; NOT deployed, NOT two live ChatGPT accounts.** This module does not modify Alpha production, access databases, contact creators, call any LLM API, create paid resources, or install automation triggers.

## Verify the locally runnable pilot

Requires Node.js 20+; zero npm dependencies, credentials or network access.

    cd tools/alpha-hybrid-pilot
    node --test pilot.test.mjs
    node pilot.mjs demo

The demo generates **two ephemeral and distinct Ed25519 keypairs** to simulate Executive and Engineering signing roles. The test validates signature verification, allowed command type, strict schema, short TTL, exactly-once journal claim, a signed Engineering result, and stop-by-default behavior. The temporary keys are **not** Captain keys, and local tests are not proof of independently authenticated ChatGPT accounts or hosted execution.

## Real signed-file processing (DISABLED)

The separate Captain-controlled signing process must approve and sign a task with a key the owner independently pins. Do not share private keys with ChatGPT, Gmail, Drive, logs, or repository files. Provide a *separately controlled* Engineering worker signing private key using an owner-approved secret store / locked-down file.

The runner requires all four safety prerequisites:
1. Captain-approved enable flag ALPHA_PILOT_ENABLED=YES (default NO).
2. ALPHA_EXEC_PUBLIC_KEY_FILE points to the independently pinned Executive verification key.
3. ALPHA_WORKER_PRIVATE_KEY_FILE points to the worker-only protected key file.
4. ALPHA_PILOT_JOURNAL points to a private durable directory.

Example (only after explicit activation authorization):

    ALPHA_PILOT_ENABLED=YES ALPHA_EXEC_PUBLIC_KEY_FILE=/secure/exec-pinned.pem ALPHA_WORKER_PRIVATE_KEY_FILE=/secure/engineering-private.pem ALPHA_PILOT_JOURNAL=/secure/journal node pilot.mjs process-file /secure/signed-task.json

Supports **only SHA256_EVIDENCE** on at most 500 text bytes, in staging scope, max 15-minute expiration, no command execution, no write beyond replay journal. Process-file prints one cryptographically signed receipt; it does **not** post GitHub comments or trigger background processing. Keep the signing key outside all AI conversations.

## Hybrid account wiring (future approval gates)

* Account 1 / Executive AI: create task intent and submit to Captain-controlled signing service; publish signed envelope to an approved bridge.
* Transfer: existing private GitHub Shipping Yard Issue #3 can store signed message envelopes (manual only in current evidence). For automatic ChatGPT Work triggers, **GitHub supports eligible PR activity, not arbitrary issue comments**; approved Gmail or Slack event triggers are alternatives. Never infer Work has subscribed to this source merely because an app is connected.
* Worker: the bridge adapter (not included or activated) reads the envelope and invokes this CLI once, after actor+public-key checks. Archive signed receipt with trace and task ID.
* Account 2 / Engineering AI: inspect work and signed evidence through an independently authorized connected account.
* Account 1: independently verify signed receipt and report to the Captain.

## Acceptance and hard stops

The following have **not** been performed: independent Captain key pin, two separately authenticated Work triggers, bridge adapter installation, real scoped GitHub message transfer to automatic worker, hosted deployment, actual OpenAI API invocation, live Creator Scout, production writes or external outreach.

The Captain's existing Shipping Yard security gates and unpinned Executive key remain blockers. No Cron, Queue, email trigger or production access is enabled by this PR.

**Emergency stop:** ALPHA_PILOT_ENABLED unset or NO, and suspend the separately approved trigger. This cannot forcibly interrupt work already running. Default NO means rejected tasks. Never reuse ephemeral demo keys.