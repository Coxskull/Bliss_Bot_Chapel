import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const desk=readFileSync("tools/commercial/alpha346-proposal-desk.html","utf8");
const contact=readFileSync("frontend/public/contact.html","utf8");
const publicBuilder=readFileSync("scripts/build-vercel-portal.mjs","utf8");

// This is strictly offline, internal and private; no browser storage, network, or checkout.
for(const phrase of ["default-src 'none'","connect-src 'none'","form-action 'none'",
                     "No entries are transmitted or saved to a server",
                     "INTERNAL REVIEW DRAFT", "NOT A CUSTOMER OFFER",
                     "GLOBAL STOP ENGAGED", "PAYMENT DISABLED","DELIVERY NOT SENT"]) {
  assert.ok(desk.includes(phrase), "proposal desk safety/positioning missing: "+phrase);
}
for(const forbidden of ["fetch(","XMLHttpRequest","WebSocket","sendBeacon(","localStorage",
                        "sessionStorage",'src="https:', 'action="https:',".innerHTML=",
                        "https://checkout.stripe.com/"]) {
  assert.ok(!desk.includes(forbidden), "forbidden token: "+forbidden);
}
for(const phrase of ["Campaign brief: objective", "Suitability matrix comparing up to three",
                     "Creative direction sheet with assets","Coordination plan: owners",
                     "One consolidated in-scope revision round"]) {
  assert.ok(desk.includes(phrase),"promised four-document and revision scope: "+phrase);
}
assert.equal((desk.match(/data-approval="/g)||[]).length,8,"unapproved operator evidence check count");
assert.ok(desk.includes("value.revisions!=='1'"),"one revision only");

assert.ok(desk.includes('id="demoButton"'),"fictional demo button must exist");
assert.ok(desk.includes("Riverfern Café (fictional)"),"fictional brand only");
assert.ok(desk.includes("SYNTHETIC-NO-CUSTOMER-RECORD"),"no actual lead");
assert.ok(desk.includes("price:''"),"fictional sample must not populate payment amount");
assert.ok(desk.includes("for(const box of approvalBoxes)box.checked=false"),"demo must clear all approval flags");

assert.ok(desk.includes("value=\"dejesuserwin1@gmail.com\""),"operator-selected real contact");
assert.ok(desk.includes("VERIFY MONITORING BEFORE PAID ORDERS"),"actual customer support not yet verified");
assert.ok(contact.includes('href="mailto:dejesuserwin1@gmail.com"'),"staged matching public contact");
assert.ok(!contact.includes("alpha7media@gmail.com"),"prior contact retired");
assert.ok(!publicBuilder.includes("tools/commercial/"),"tools must not be published by Vercel portal");
assert.ok(!publicBuilder.includes("alpha346-proposal-desk.html"),"internal tool omitted from website");
console.log("ALPHA-346 offline four-deliverable proposal desk safety and source consistency PASS");
