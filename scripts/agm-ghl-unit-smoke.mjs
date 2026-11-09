import assert from "node:assert/strict";
import { mapGhlContact, ghlNotice } from "../supabase/functions/alpha-public-intake/ghl-payload.mjs";
import { deliverToGhl } from "../supabase/functions/alpha-public-intake/ghl-sync.mjs";
import { supportNotice } from "../supabase/functions/alpha-public-intake/support-contract.mjs";

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
const support = mapGhlContact({
  ...record,kind:"support",category:"general",
  notice_version:supportNotice,organization:"",website:"",description:"Never send content"
},loc);
assert.equal(support.source,"ALPHA GLOBAL MEDIA · General Support Inquiry");
for (const forbidden of ["category","description","organization","marketing_opt_in","tags"]) 
  assert.ok(!(forbidden in support));
for (const sensitive of ["billing","refund","failed_campaign","privacy","security_incident"])
  assert.throws(()=>mapGhlContact({...record,kind:"support",category:sensitive,
    notice_version:supportNotice},loc));
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
const fakeDb={
  rpc: async(name,args)=>{
    if(name==="alpha_ghl_claim"){
      claims++;
      return {data:[{request_id:"test-id",claim_token:"test-claim"}],error:null};
    }
    if(name==="alpha_ghl_finish"){
      completions.push(args);
      return {data:true,error:null};
    }
    throw Error("Unexpected RPC");
  },
  from: name=>{
    assert.equal(name,"alpha_public_intake_requests");
    return {select:()=>({eq:()=>({
      single:async()=>({data:record,error:null}),
      maybeSingle:async()=>({data:{kind:record.kind},error:null})
    })})};
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
