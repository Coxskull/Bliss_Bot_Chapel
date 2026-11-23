import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
const page=read("frontend/public/global-media.html");
const script=read("frontend/public/wedding-planner.js");
const build=read("scripts/build-vercel-portal.mjs");
const policy=read("frontend/public/alpha-privacy.html");
const header='https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake';

for(const clause of [
  'data-ask-alpha-ai="false"',
  'id="ask-alpha-ai-consent-panel" hidden',
  'id="ask-alpha-ai-consent" type="checkbox"',
  'id="ask-alpha-marketing-consent" type="checkbox"',
  'id="ask-alpha-ai-status"'
]) assert.ok(page.includes(clause),"Public AI disclosure/consent source missing: "+clause);
assert.ok(!page.includes('id="ask-alpha-ai-consent" type="checkbox" checked'));
assert.ok(!page.includes('id="ask-alpha-marketing-consent" type="checkbox" checked'));
assert.ok(policy.includes('id="ask-alpha-ai"')&&policy.includes("AGM-ASK-ALPHA-AI-2026-11-08"));
for(const clause of [
  "askAlphaAiConsent?.checked",
  "askAlphaPending",
  "request_key:crypto.randomUUID()",
  'action:"ask_alpha"',
  'ai_processing_consent:true',
  'notice_version:"AGM-ASK-ALPHA-AI-2026-11-08"',
  "askAlphaHistory.slice(-4)",
  'credentials:"omit"',
  'result?.status === "ANSWERED"',
  "result.receipt_id",
  "askAlphaLive"
]) assert.ok(script.includes(clause),"Hosted browser contract missing: "+clause);
assert.ok(script.includes(header));
assert.ok(build.includes("process.env.ALPHA_ASK_ALPHA_FRONTEND_ENABLED === 'true'"));
assert.ok(build.includes("Ask Alpha AI consent/release controls missing"));
for(const sensitive of ["OPENAI_API_KEY","SUPABASE_SERVICE_ROLE_KEY","ALPHA_ASK_ALPHA_HASH_SECRET","localStorage","sessionStorage"])
 assert.ok(!script.includes(sensitive),"Secret or persistent chat state leaked to browser: "+sensitive);
console.log("TASK20_HOSTED_BROWSER_CONSENT_CONTRACT_PASS: default-off; separate opt-in; existing Edge endpoint; no browser secrets");
