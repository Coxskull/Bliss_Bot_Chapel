import assert from "node:assert/strict";
import { mapGhlContact, ghlNotice } from "../supabase/functions/alpha-public-intake/ghl-payload.mjs";
import { deliverToGhl } from "../supabase/functions/alpha-public-intake/ghl-sync.mjs";
import "./agm-ask-alpha-smoke.mjs";
import { readFileSync } from "node:fs";

const migration = readFileSync(new URL("../supabase/migrations/20261105_alpha_ghl_contact_delivery.sql", import.meta.url), "utf8");
assert.match(migration, /ALTER TABLE public\.alpha_ghl_delivery ENABLE ROW LEVEL SECURITY;/);
assert.match(migration, /REVOKE ALL ON public\.alpha_ghl_delivery FROM PUBLIC, anon, authenticated;/);
assert.match(migration, /GRANT ALL ON public\.alpha_ghl_delivery TO service_role;/);
assert.match(migration, /REVOKE EXECUTE ON FUNCTION public\.alpha_ghl_claim\(uuid\) FROM PUBLIC,anon,authenticated;/);
assert.match(migration, /GRANT EXECUTE ON FUNCTION public\.alpha_ghl_claim\(uuid\) TO service_role;/);
assert.match(migration, /REVOKE EXECUTE ON FUNCTION public\.alpha_ghl_finish\(uuid,uuid,text,integer,text,text\) FROM PUBLIC,anon,authenticated;/);
assert.match(migration, /GRANT EXECUTE ON FUNCTION public\.alpha_ghl_finish\(uuid,uuid,text,integer,text,text\) TO service_role;/);

const loc = "aBcD1234EFgh5678IJkl";
const record = {
  kind: "creator", notice_version: ghlNotice,
  processing_acknowledged: true, adult_confirmed: true,
  full_name: "Mock Contract Creator", email: "creator@unit.test",
  organization: "", website: "https://example.org/public-channel",
  market: "Test Manila", category: "Podcast", description: "Never forward me",
  marketing_opt_in: false
};
const creator = mapGhlContact(record, loc);
assert.deepEqual(creator, {
  locationId: loc, name: record.full_name,
  email: record.email, source: "ALPHA GLOBAL MEDIA · Creator Interest",
  createNewIfDuplicateAllowed: false, website: record.website
});
const advertiser = mapGhlContact({
  ...record, kind: "advertiser", organization: "Mock Company",
  website: "https://example.org/brand"
}, loc);
assert.equal(advertiser.companyName, "Mock Company");
assert.equal(advertiser.source, "ALPHA GLOBAL MEDIA · Advertiser Interest");
for (const forbidden of [
  "tags","customFields","description","market","category","marketing_opt_in",
  "processing_acknowledged","phone","dnd","inboundDndSettings"
]) assert.ok(!(forbidden in creator), forbidden + " excluded");
for(const invalid of [
  {...record,kind:"privacy"}, {...record,notice_version:"AGM-INTEREST-2026-11-05"},
  {...record,processing_acknowledged:false},
  {...record,email:"fake@example.invalid"}
]) assert.throws(()=>mapGhlContact(invalid,loc));

const oldFetch=globalThis.fetch;
const oldDeno=globalThis.Deno;
let env={};
globalThis.Deno={env:{get:k=>env[k]}};
let fetches=0, completions=[], claims=0, httpResponse={ok:true,status:200};
let finishResult={data:true,error:null};
let claimResult={data:[{request_id:"test-id",claim_token:"test-claim"}],error:null};
const fakeDb={
  rpc: async(name,args)=>{
    if(name==="alpha_ghl_claim"){
      claims++;
      return claimResult;
    }
    if(name==="alpha_ghl_finish"){
      completions.push(args);
      return finishResult;
    }
    throw Error("Unexpected RPC");
  },
  from: name=>{
    assert.equal(name,"alpha_public_intake_requests");
    return {select:()=>({eq:()=>({single:async()=>({data:record,error:null})})})};
  }
};
try {
  env={}; // Fail closed with no toggle and no GHL account access.
  assert.equal(await deliverToGhl(fakeDb,"test-id"),"DISABLED");
  assert.equal(claims,0);
  env={ALPHA_GHL_SYNC_ENABLED:"true"};
  assert.equal(await deliverToGhl(fakeDb,"test-id"),"CONFIG_MISSING");
  assert.equal(claims,0);
  env={
    ALPHA_GHL_SYNC_ENABLED:"true",
    ALPHA_GHL_LOCATION_ID:loc,
    ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN:"SYNTHETIC-TEST-TOKEN"
  };
  claimResult={data:null,error:{message:"mock Supabase claim failure"}};
  await assert.rejects(deliverToGhl(fakeDb,"test-id"),/GHL_DELIVERY_CLAIM_FAILED/);
  claimResult={data:[{request_id:"test-id",claim_token:"test-claim"}],error:null};
  globalThis.fetch=async(url,options)=>{
    fetches++;
    assert.equal(url,"https://services.leadconnectorhq.com/contacts/upsert");
    assert.equal(options.method,"POST");
    assert.equal(options.headers.Version,"v3");
    assert.equal(options.headers.Authorization,"Bearer SYNTHETIC-TEST-TOKEN");
    assert.deepEqual(JSON.parse(options.body),creator);
    return {...httpResponse,json:async()=>({contact:{id:"mock_contact_123"}})};
  };
  assert.equal(await deliverToGhl(fakeDb,"test-id"),"DELIVERED");
  assert.equal(fetches,1);
  assert.equal(completions.at(-1).p_new_state,"DELIVERED");
  assert.equal(completions.at(-1).p_contact_id,"mock_contact_123");

  // A successful GHL HTTP response is not a successful durable delivery until
  // the exact outbox claim is committed in Supabase.
  finishResult={data:false,error:null};
  await assert.rejects(deliverToGhl(fakeDb,"test-id"),/GHL_DELIVERY_STATE_NOT_COMMITTED/);
  finishResult={data:null,error:{message:"mock Supabase write failure"}};
  await assert.rejects(deliverToGhl(fakeDb,"test-id"),/GHL_DELIVERY_STATE_NOT_COMMITTED/);
  finishResult={data:true,error:null};

  httpResponse={ok:false,status:401};
  assert.equal(await deliverToGhl(fakeDb,"test-id"),"BLOCKED");
  assert.equal(completions.at(-1).p_error_class,"auth_failure");
  httpResponse={ok:false,status:429};
  assert.equal(await deliverToGhl(fakeDb,"test-id"),"RETRY");
  assert.equal(completions.at(-1).p_error_class,"rate_limited");
  httpResponse={ok:true,status:200};
  globalThis.fetch=async()=>({ok:true,status:200,json:async()=>({contact:{id:null}})});
  assert.equal(await deliverToGhl(fakeDb,"test-id"),"RETRY");
  assert.equal(completions.at(-1).p_error_class,"invalid_response");
} finally {
  globalThis.fetch=oldFetch;
  globalThis.Deno=oldDeno;
}
console.log("ALPHA GoHighLevel CRM bridge: consent, minimal mapping, gated disabled mode, delivery and error contracts PASS");
