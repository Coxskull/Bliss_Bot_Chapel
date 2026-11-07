#!/usr/bin/env node
"use strict";
// Offline QA of protected Vercel Preview-only operator output. Never logs or
// accesses a real key, customer record, API, or staff identity.
const assert=require("node:assert/strict");
const fs=require("node:fs"),path=require("node:path");
const root=path.resolve(__dirname,"../..");
const out=path.join(root,".vercel-portal","staff","task21");
const names=fs.readdirSync(out).sort();
assert.deepEqual(names,[
  "index.html","operator.css","operator.js","operator-config.js",
  "operator-sdk.js","operator-view-model.mjs"].sort());
const config=fs.readFileSync(path.join(out,"operator-config.js"),"utf8");
const html=fs.readFileSync(path.join(out,"index.html"),"utf8");
const sdk=fs.readFileSync(path.join(out,"operator-sdk.js"),"utf8");
const model=fs.readFileSync(path.join(out,"operator-view-model.mjs"),"utf8");
assert.match(config,/environment":"staging"/);
assert.match(config,/mutationsEnabled":false/);
assert.match(config,/https:\/\/wdlmcriajgsretgyusmo\.supabase\.co/);
assert.match(config,/https:\/\/task21-ci-preview\.vercel\.app/);
assert.match(config,/sb_publishable_/);
assert.doesNotMatch(config,/sb_secret_|SERVICE_ROLE|SUPABASE_SERVICE_ROLE_KEY/);
assert.doesNotMatch(config+html+sdk+model,/bkutbglzivfdnoerigyb/);
assert.doesNotMatch(config+html+sdk+model,/https:\/\/cdn\.jsdelivr\.net/);
assert.match(html,/noindex,nofollow,noarchive/);
assert.match(sdk,/createClient/);
assert.match(model,/config.environment!=="staging"/);
assert.ok(!fs.existsSync(path.join(root,".task21-private-build")),
  "Generated staging preview must purge unsigned intermediate build");
assert.ok(!fs.existsSync(path.join(out,"package-lock.json")),
  "Never serve build dependencies or npm lockfile in staff bundle");
console.log("TASK21_PROTECTED_PREVIEW_OFFLINE_PASS: locked SDK, exact stage only, no live URL, no privileged key, no external files or uploads.");
