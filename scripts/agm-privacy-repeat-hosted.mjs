import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";

// Synthetic-only integration test against ALPHA's existing public intake.
// Never contacts HighLevel: kind=privacy is explicitly not CRM eligible.
// All test identities use reserved @example.invalid domain and are
// cleaned up by an independent exact-identity DB readback/delete step.
const endpoint = "https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake";
const origin = "https://alphacreativecontent.com";
const suffix = [process.env.GITHUB_RUN_ID || "local",
  process.env.GITHUB_RUN_ATTEMPT || "1"].join("-");
const email = "privacy-repeat-ci-" + suffix + "@example.invalid";
const note = "AGM-INTEREST-2026-11-05-GHL";
const base = {
  kind: "privacy",
  full_name: "Synthetic ALPHA Repeat Privacy CI",
  email,
  organization: "",
  website: "",
  market: "Test",
  category: "",
  middle_name: "",
  processing_acknowledged: true,
  adult_confirmed: true,
  marketing_opt_in: false,
  notice_version: note
};
const submit = async (body, requestOrigin=origin) => {
  const r=await fetch(endpoint,{
    method:"POST",
    headers:{"Content-Type":"application/json","Origin":requestOrigin},
    body:JSON.stringify(body),
    signal: AbortSignal.timeout(15000)
  });
  return {code:r.status,data:await r.json()};
};
const first={...base,description:"Synthetic access request, no real person",
  request_key:randomUUID()};
const second={...base,description:"Synthetic correction request, independent matter",
  request_key:randomUUID()};
const a=await submit(first);
assert.equal(a.code,201,"first privacy request saved");
assert.equal(a.data.status,"RECEIVED_NOT_YET_VERIFIED");
assert.match(a.data.receipt_id,/^[a-f0-9-]{36}$/i);
const retry=await submit(first);
assert.equal(retry.code,201,"same-key privacy retry accepted");
assert.equal(retry.data.receipt_id,a.data.receipt_id,"same receipt idempotency");
const b=await submit(second);
assert.equal(b.code,201,"independent second privacy request from same email accepted");
assert.match(b.data.receipt_id,/^[a-f0-9-]{36}$/i);
assert.notEqual(b.data.receipt_id,a.data.receipt_id,"distinct request has distinct receipt");
const reject=await submit({...second,request_key:randomUUID(),
  processing_acknowledged:false});
assert.equal(reject.code,422,"processing acknowledgement required");
const foreign=await submit({...second,request_key:randomUUID()},
  "https://untrusted.example.invalid");
assert.equal(foreign.code,403,"foreign origin rejected");
console.log("ALPHA privacy repeat: same-email two distinct receipts, same-key idempotency, 422 consent denial and 403 origin denial PASS; no GHL calls.");
