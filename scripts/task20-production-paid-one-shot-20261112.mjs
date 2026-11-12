// ALPHA Task #20 owner-authorized ONE-TIME paid hosted production smoke.
// Synthetic non-PII question, explicit processing consent, marketing OFF.
// Fixed one-use UUID prevents accidental second charge from workflow reruns.
// Existing SQL governor reserves $0.01 and enforces $1/day, $9/month.
import assert from "node:assert/strict";

const url="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake";
const payload={
  action:"ask_alpha",
  notice_version:"AGM-ASK-ALPHA-AI-2026-11-08",
  ai_processing_consent:true,
  marketing_opt_in:false,
  session_id:"a0898279-f9a8-43ea-8219-3cc98df4e375",
  request_key:"2e3bc4b9-46d7-43c4-99ca-ecf7da6aa823",
  audience:"advertiser",
  message:"In one short sentence, how does an advertiser inquire about ALPHA GLOBAL MEDIA services?",
  history:[]
};
let response;
try {
  response=await fetch(url,{
    method:"POST",
    headers:{
      "Origin":"https://alphacreativecontent.com",
      "Content-Type":"application/json",
      "User-Agent":"ALPHA-TASK20-OWNER-AUTHORIZED-ONE-SHOT-20261112"
    },
    body:JSON.stringify(payload),
    redirect:"error",
    signal:AbortSignal.timeout(23000)
  });
} catch(err) {
  console.error("TASK20_ONE_SHOT_TRANSPORT_ERROR",err?.name||"unknown");
  process.exitCode=1;
}
if (response) {
  let result;
  try { result=await response.json(); } catch { result={error:"NON_JSON_RESPONSE"}; }
  console.log("TASK20_ONE_SHOT_HTTP_STATUS",response.status);
  console.log("TASK20_ONE_SHOT_RESULT",result?.status||"NOT_ANSWERED");
  if(result?.error) console.error("TASK20_ONE_SHOT_ERROR_CODE",String(result.error).slice(0,120));
  if(result?.receipt_id) console.log("TASK20_ONE_SHOT_RECEIPT",result.receipt_id);
  if(response.ok&&result?.status==="ANSWERED"){
    assert.match(result.receipt_id||"",/^[0-9a-f-]{36}$/i);
    assert.equal(result.model,"gpt-6-luna");
    assert.equal(result.marketing_opt_in,false);
    assert.equal(result.support_case_created,false);
    assert.ok(typeof result.answer==="string"&&result.answer.trim().length>10);
    console.log("TASK20_ONE_SHOT_ANSWERED_PASS answer_chars="+result.answer.length+" receipt_confirmed");
  } else {process.exitCode=1;}
}
