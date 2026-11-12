// Task #21: manual-first public support and disputes path.
// Static contract only. No network, messages, customer records or provider spend.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=file=>readFileSync(new URL("../"+file,import.meta.url),"utf8");
const support=read("frontend/public/support.html");
const contact=read("frontend/public/contact.html");
const home=read("frontend/public/global-media.html");
const build=read("scripts/build-vercel-portal.mjs");
const businessAddress="dejesuserwin1@gmail.com";
assert.ok(contact.includes(businessAddress),"Use existing owner-designated public business email");
assert.ok(support.includes(businessAddress),"Manual support contact must match approved public contact");
assert.ok(contact.includes('href="/support.html"'),"Contact page must link to support");
assert.ok(home.includes('href="/support.html"'),"Primary navigation must link to support");
assert.match(build,/'contact\.html', 'support\.html'/,"Support page must be included in Vercel's explicit allowlist");
for(const required of [
  "Billing, cancellations, refunds or failed delivery",
  "Privacy access, correction or deletion",
  "Suspected security issues",
  'href="/privacy-request.html"',
  "not yet available",
  "No automatic case number is issued",
  "not an emergency incident-response service",
  'href="/alpha-privacy.html"',
  'href="/alpha-terms.html"'
]) assert.ok(support.includes(required),"Missing safety/support disclosure: "+required);
for(const forbidden of [
  "data-kind=\"support\"",
  "data-endpoint=",
  "alpha_support_case",
  "SUPABASE_SERVICE_ROLE_KEY",
  "OPENAI_API_KEY",
  "guaranteed response within",
  "24/7 support",
  "<form",
  "ALPHA_GHL_SUPPORT_SYNC_ENABLED"
]) assert.ok(!support.includes(forbidden),"Unsafe or unlaunched support feature on public page: "+forbidden);
assert.ok((support.match(/href="mailto:/g)||[]).length>=4);
assert.ok(!build.includes("'frontend/private/support-operator'"),"Private operator UI must remain excluded");
console.log("TASK21_PUBLIC_MANUAL_SUPPORT_SAFE_ROUTE_PASS — no claim of live tickets, response SLA, payment, or backend support mutations");
