import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";

// NO REAL contacts or HighLevel API traffic. Runtime disallows .invalid CRM sync.
const endpoint = "https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake";
const origin = "https://alphacreativecontent.com";
const key = process.env.GITHUB_RUN_ID + "-" + process.env.GITHUB_RUN_ATTEMPT;
const noticeVersion = "AGM-INTEREST-2026-11-05-GHL";
const send = async (form, testOrigin=origin) => {
  const r = await fetch(endpoint,{
    method:"POST",headers:{"Origin":testOrigin,"Content-Type":"application/json"},
    body:JSON.stringify(form),signal:AbortSignal.timeout(15000)
  });
  return {status:r.status, body: await r.json()};
};
const base = {
  full_name:"Synthetic ALPHA GHL CI",
  email:"ghl-creator-ci-" + key + "@example.invalid",
  organization:"", website:"https://example.invalid/creator/ci",
  market:"Test Manila", category:"Podcast", description:"Test only",
  middle_name:"", processing_acknowledged:true, adult_confirmed:true,
  marketing_opt_in:false, notice_version:noticeVersion,
  request_key:randomUUID()
};
const check = (r,scope) => {
  assert.equal(r.status,201,scope+" receipt persisted");
  assert.equal(r.body?.status,"RECEIVED_NOT_YET_VERIFIED");
  assert.match(r.body?.receipt_id,/^[a-f0-9-]{36}$/i);
};
const creator = {...base,kind:"creator"};
const c = await send(creator); check(c,"creator");
const retry=await send(creator);check(retry,"idempotent retry");
assert.equal(retry.body.receipt_id,c.body.receipt_id);
const adv={
  ...base,kind:"advertiser",email:"ghl-advertiser-ci-"+key+"@example.invalid",
  organization:"Synthetic Company",website:"https://example.invalid/brand",
  request_key:randomUUID()
};
check(await send(adv),"advertiser");
const privacy={
  ...base,kind:"privacy",email:"ghl-privacy-ci-"+key+"@example.invalid",
  website:"",market:"",category:"",description:"Synthetic privacy request",
  request_key:randomUUID()
};
check(await send(privacy),"privacy, never CRM");
const rejected=await send({...creator,request_key:randomUUID(),
  notice_version:"UNAPPROVED"});
assert.equal(rejected.status,422,"unknown notice must be denied");
const noConsent=await send({...creator,request_key:randomUUID(),
  processing_acknowledged:false});
assert.equal(noConsent.status,422,"missing consent denied");
const badOrigin=await send({...creator,request_key:randomUUID()},
  "https://unapproved.invalid");
assert.equal(badOrigin.status,403,"unapproved browser origin denied");
console.log("GHL bridge live Supabase intake: three new-notice synthetic receipts, idempotency, notice/consent/origin denials PASS. NO HighLevel contacts sent.");
