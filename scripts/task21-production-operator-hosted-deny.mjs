// Exactly one credential-free read-only production API denial probe.
// Never sends a token, accesses cases, creates users or incurs provider spend.
import assert from "node:assert/strict";
const url="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-support-manual-operator";
const res=await fetch(url,{
 method:"GET",redirect:"error",
 headers:{"Origin":"https://alphacreativecontent.com",
   "User-Agent":"ALPHA-TASK21-PROTECTED-STAFF-NEGATIVE-20261112"},
 signal:AbortSignal.timeout(18000)
});
const body=await res.text();
assert.ok([401,403].includes(res.status),
 "Production protected operator endpoint must reject credential-free request; actual HTTP "+res.status);
assert.ok(body.length<2500,"No support records in anonymous response");
for(const bad of ["provider_message_id","case_id","observed_at","customer_email","request_id"]){
 assert.ok(!body.includes(bad),"Operator metadata leaked to anonymous request");
}
console.log("TASK21_LIVE_PROTECTED_OPERATOR_ANON_DENIED_PASS http="+res.status+" no_credentials no_cases no_egress");
