import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";

// Uses ONLY synthetic .invalid identifiers. Runs on the existing GitHub
// runner against the intended existing Supabase Edge endpoint.
// No secrets, account authentication, actual customer data or outreach.
const endpoint="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake";
const goodOrigin="https://alphacreativecontent.com";
const id=process.env.GITHUB_RUN_ID+"-"+process.env.GITHUB_RUN_ATTEMPT;
const send=async(payload,origin=goodOrigin)=>{
  const resp=await fetch(endpoint,{method:"POST",headers:{"Origin":origin,"Content-Type":"application/json"},body:JSON.stringify(payload),signal:AbortSignal.timeout(15000)});
  let body;
  try{body=await resp.json();}catch{body={};}
  return {status:resp.status,body,originHeader:resp.headers.get("access-control-allow-origin")};
};
const base={
  full_name:"Synthetic ALPHA CI",
  email:"alpha-ci-"+id+"@example.invalid",
  organization:"",website:"https://example.invalid/alpha/ci",
  market:"Test Manila",category:"Podcast",
  description:"Automated synthetic test only",middle_name:"",
  adult_confirmed:true,processing_acknowledged:true,marketing_opt_in:false,
  request_key:randomUUID(),notice_version:"AGM-INTEREST-2026-11-05"
};
const check=async(resp,kind)=>{
  assert.equal(resp.status,201,kind+" should be persisted");
  assert.equal(resp.originHeader,goodOrigin,"public CORS origin");
  assert.equal(resp.body.status,"RECEIVED_NOT_YET_VERIFIED");
  assert.match(resp.body.receipt_id,/^[0-9a-f-]{36}$/i,"server-generated receipt");
};
const creator={...base,kind:"creator"};
const created=await send(creator);
await check(created,"creator");
const retry=await send(creator);
await check(retry,"retry");
assert.equal(retry.body.receipt_id,created.body.receipt_id,"same-key retry must retain receipt");
const noConsent=await send({...creator,request_key:randomUUID(),processing_acknowledged:false});
assert.equal(noConsent.status,422,"missing consent refused");
const notOurDomain=await send({...creator,request_key:randomUUID()},"https://example.invalid");
assert.equal(notOurDomain.status,403,"foreign origin refused");
const advertiser={
  ...base,kind:"advertiser",
  email:"advertiser-ci-"+id+"@example.invalid",
  organization:"Synthetic Alpha Brand",
  website:"https://example.invalid/brand",
  category:"Retail",request_key:randomUUID()
};
await check(await send(advertiser),"advertiser");
const privacy={
  ...base,kind:"privacy",
  email:"privacy-ci-"+id+"@example.invalid",
  organization:"",website:"",market:"",category:"",
  description:"Synthetic test access question",request_key:randomUUID()
};
await check(await send(privacy),"privacy request");
console.log("Hosted ALPHA interest intake verified: 3 synthetic receipts, safe retry, consent rejection, origin rejection.");
