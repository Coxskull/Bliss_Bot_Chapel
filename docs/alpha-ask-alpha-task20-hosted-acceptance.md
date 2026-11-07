# Task #20 — Ask Alpha OpenAI staged activation / acceptance
Directive: ALPHA-TASK-020-OPENAI-ACTIVATION-388, 2026-11-08.

## Scope, source of truth, and stop conditions
- This code EXTENDS the existing Supabase `alpha-public-intake` Edge function and existing public Ask Alpha panel. It does not add a second bot, CRM adapter, or database project.
- Source branch: existing draft PR #350 in `Coxskull/Bliss_Bot_Chapel`. Deployments and database migrations are NOT implied by passing CI.
- Factory/outreach/campaign/payment GLOBAL STOP stays engaged. `ALPHA_ASK_ALPHA_ENABLED` and `ALPHA_ASK_ALPHA_FRONTEND_ENABLED` must stay unset or false until all gates below pass.
- The historical .NET Controlled AI adapter is a private draft-generation backend, NOT a public customer authentication or consent path. Its live Railway configuration is not evidence of a public API key or billable readiness.
- Task #21 must independently verify human support case creation and escalation before Task #20's full support-journey gate can pass. The AI reply must never claim a support case was opened when one was not.

## Provider and budget
- Model pinned: `gpt-6-luna`, Responses API `/v1/responses`, `store:false`, `tools:[]`, `reasoning.effort:none`, `max_output_tokens:400`, no web/file/search or tool spending.
- Standard global short-context published token prices (verify at deployment): input $0.10/1M, cached input $0.01/1M, output $0.50/1M. No ChatGPT subscription is an API credit.
- Each call atomically reserves $0.010000 in the EXISTING Supabase database before provider request. Global lock serializes callers. Daily limit $1, monthly $10 including operator-reconciled **outside-this-handler** OpenAI usage. Settlement records provider receipt, actual tokens and calculated actual cost. Unknown billing state remains reserved and blocks further calls.
- Default-off control `public.alpha_ask_alpha_settings` requires `enabled=true`, UTC `metered_month`, `verified_at` within 24h and `externally_billed_usd`. Do not enable until an authorized operator has verified account billing and configured a separate provider-side project cap if available. If billed usage cannot be reliably reconciled, leave disabled.
- Max one concurrent request per pseudonymous session, 12 per session/day, six per HMAC-hashed gateway IP/hour, 100 total/day; 400-character user message, up to four bounded recent turns, 15s provider timeout, 30KB response cap. Any spoofable or missing gateway IP identity must be reviewed and repaired BEFORE live activation; browser `Origin` is not authentication.
- SQL candidate: `supabase/migrations/20261108_alpha_ask_alpha_ai_governor.sql`. Database writes, RLS, grants and RPC execution have NOT yet been hosted-tested.

## Human-only prerequisites — no secrets in chat
1. Confirm API organization/project, separate project-level spending guard and whether a payment method is already on file; owner approval covers up to $10/month usage, not a new plan or unrelated charges. If a new payment method is needed, report exact screen, action and cost rather than performing it.
2. In the existing Supabase project's approved Edge Function secret manager, configure `OPENAI_API_KEY` and fresh `ALPHA_ASK_ALPHA_HASH_SECRET` (minimum 32 random characters) with runtime access restricted to the existing function. Never use `NEXT_PUBLIC_` or Vercel public environment variables for API keys.
3. Owner/privacy reviewer approves AI processing notice version `AGM-ASK-ALPHA-AI-2026-11-08`, data retention, provider disclosure, support handling, and separate optional marketing choice.
4. Resolve source DB recovery/backup safety and review the staged RLS/migration. Apply the migration only to an isolated authorized staging target, verifying `anon`/`authenticated` cannot read tables or execute budget RPCs.
5. Establish a trusted (non-client-spoofable) edge IP identity, replay behavior, request-size enforcement and any WAF/rate protection; don't enable based only on CORS.
6. Reconcile provider metering into the control row, then enable the existing function by setting `ALPHA_ASK_ALPHA_ENABLED=true` ONLY in authorized staging. Enable public-page AI presentation separately with `ALPHA_ASK_ALPHA_FRONTEND_ENABLED=true` in an approved preview build.
7. Check the live Vercel `build-rate-limit` blocker before trying a preview. Do not purchase another tier without approval.

## Required evidence before updating PRE-LAUNCH MASTER #20
- Static and mocked tests: `node scripts/agm-ghl-unit-smoke.mjs` (now imports Task20 smoke) and existing CI; collect exact head and run URLs. These do NOT replace hosted acceptance.
- Real staged advertiser and creator conversations with language variations; verified response text, model ID, provider request ID, actual input/output tokens and calculated cost, matching durable receipt IDs, consent version and session isolation.
- Genuine support escalation with separately verified Task #21 support case receipt and human queue. If unavailable, mark BLOCKED, not a success.
- Explicit negative cases: denied consent (zero API calls), unsupported origin, malformed/large input, idempotency replay, per-IP/session limits, concurrency collision, daily exhaustion, monthly exhaustion including pending usage and external billed usage, missing/expired metering, missing key, timeout, provider HTTP failure, missing usage, and failed settlement. After UNKNOWN, verify the next call fails closed.
- Existing public interest and privacy forms must continue to work with their own receipt and HighLevel outbox state. No automatic CRM contact, outreach, campaign, price/quote, refund, payment or commercial approval from chat text.
- Security inspection of prompts, content sanitization, PII minimization, service-key isolation, RLS, abnormal token usage, tenant data boundaries and published policy accuracy.

## Rollback / failure containment
1. Disable the existing backend `ALPHA_ASK_ALPHA_ENABLED` first; no new provider reservations will occur. Preserve ledger entries, including UNKNOWN, for audit.
2. Rebuild/revert Vercel presentation to `ALPHA_ASK_ALPHA_FRONTEND_ENABLED=false`; the original scripted guide is preserved.
3. For a code problem, revert ONLY Task20 branch code via approved PR/release procedures, using Vercel's previously accepted production deployment if a release occurred. Never revert unrelated intake, GoHighLevel, factory STOP or Auth.
4. Never delete receipt rows or revoke existing privacy requests. A migration rollback is separate and requires approved backup and dependency review.

## Current evidence state
Engineering branch code only. No real API conversation, no provider usage or cost receipt, no staged SQL application, no new credential, no Vercel/Railway deployment, and no human support case demonstrated by this change. Maintain Task #20 OPEN until all hosted gates pass.
