# ALPHA GLOBAL MEDIA — Six-City Creator Scout Staging Worker

Evidence: ALPHA-EV-20261024-6CITY-WORKER-001

This is an isolated *candidate*, NOT an authorized live Scout execution. It adds deterministic public-source recheck logic for the six pinned existing `scout_tasks` draft manifests. All six task IDs and SHA256 hashes are fixed in `worker.mjs`. It performs **zero** AI inference, purchases, outreach, advertising, or production writes.

Controls: verified Captain Ed25519 envelope, 10-minute expiry, D1 task approval and seed bytes, atomic nonce receipt, status/attempt compare-and-update, geographic fixed HTTPS host, D1 STOP before and after fetch, one message per Queue batch, independent D1 STOP + Cloudflare Queue pause in `finally`. Only one task may be armed at a time; STOP and Queue pause must not be bypassed. The supplied 2026-10-24 envelopes will expire by 16:25:27 Asia/Manila. Refresh them after technical approval.

Run locally: `node --test tests/worker.test.mjs` (23 passing tests, zero failures on Node v22.16.0). Tests use synthetic keys, simulated D1, mocked HTTP responses; no remote Worker ran.

Production constraints: never replace existing production or staging Worker without review. No trigger has been attached to this candidate. Never set `captain_stop` to zero merely to make a test pass; separate Captain acceptance and one-shot rearm are required. Cloudflare owner's restricted Queue control credential is required for automatic emergency pause; verify secret and Queue settings before any live use. Never put Captain private key in repo/Drive/Worker.

Existing live state at inspection: `scout_tasks` six `draft`, no approvals, attempts=0; `scout_six_city_worker_results` zero rows; STOP `1`, enabled `0`, queue paused; existing Worker still pinned to pilot `...-003`.
