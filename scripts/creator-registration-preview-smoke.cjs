"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { validateCreatorPreview } = require("../frontend/public/creator-registration-preview.js");
const root = path.resolve(__dirname, "..");
const fixture = {
  creatorName: "Synthetic Creator Example",
  email: "creator@example.invalid",
  channelUrl: "https://example.invalid/channel/sample",
  market: "Test City",
  category: "podcast",
  processingConsent: true
};
let count = 0;
function check(ok, description) { assert.equal(ok, true, description); count += 1; }
check(validateCreatorPreview(fixture).ok, "accept fictional complete sample");
for (const [field,value] of [
  ["processingConsent",false],
  ["creatorName","Real Customer"],
  ["creatorName",""],
  ["email","real@gmail.com"],
  ["email","bad address"],
  ["channelUrl","http://example.invalid/channel/sample"],
  ["channelUrl","https://real-site.test/channel"],
  ["market","Manila"],
  ["category",""]
]) check(!validateCreatorPreview({...fixture,[field]:value}).ok, "reject unsafe " + field);
check(validateCreatorPreview({...fixture,marketingConsent:false}).ok, "optional marketing consent is not required");
check(validateCreatorPreview({...fixture,marketingConsent:true}).ok, "marketing flag does not replace required acknowledgement");
const html = fs.readFileSync(path.join(root,"frontend/public/creator-registration-preview.html"),"utf8");
const js = fs.readFileSync(path.join(root,"frontend/public/creator-registration-preview.js"),"utf8");
const build = fs.readFileSync(path.join(root,"scripts/build-vercel-portal.mjs"),"utf8");
check(/NOT AN OPEN REGISTRATION FORM/.test(html), "visible safety notice");
check(/name="processingConsent" type="checkbox" required/.test(html), "required unchecked acknowledgement");
check(/name="marketingConsent" type="checkbox"/.test(html), "separate optional marketing");
check(!/<input[^>]*\schecked(?:\s|=|>)/i.test(html), "no consent choice prechecked");
check(/event.preventDefault\(\)/.test(js), "form submission intercepted");
check(!/\b(fetch|XMLHttpRequest|sendBeacon|localStorage|sessionStorage)\s*[.(]/.test(js), "no network or browser storage primitives");
check(!/creator-registration-preview(?:\.html|\.js)/.test(build), "not published by current Vercel portal build");
console.log("COM-261-004 fictional creator preview: " + count + "/" + count + " smoke assertions passed; no registration enabled.");
