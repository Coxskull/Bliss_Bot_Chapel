// Task20, explicit opt-in, read-only model metadata check, private runtime only.
// No prompts, no Responses creation, no customer data. Do not log secrets.
import {pathToFileURL} from "node:url";
const model="gpt-6-luna", endpoint="https://api.openai.com/v1/models/gpt-6-luna";
const fail=code=>({ok:false,code});
export async function probeOpenAiProject({env=process.env,fetcher=fetch,timeoutMs=8000}={}){
 if(env.TASK20_NONBILLABLE_ACCESS_CHECK!=="YES")return fail("OPT_IN_REQUIRED");
 const project=env.ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID;
 const org=env.ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID;
 const key=env.OPENAI_API_KEY;
 if(typeof project!=="string"||!/^proj_[A-Za-z0-9_-]{4,96}$/.test(project))return fail("PROJECT_ID_INVALID");
 if(typeof org!=="string"||!/^org_[A-Za-z0-9_-]{4,96}$/.test(org))return fail("ORGANIZATION_ID_INVALID");
 if(typeof key!=="string"||key.length<16||key.length>512||/[\s\x00-\x1f]/.test(key))return fail("API_KEY_INVALID");
 if(!Number.isSafeInteger(timeoutMs)||timeoutMs<1||timeoutMs>10000)return fail("TIMEOUT_INVALID");
 try{
  const response=await fetcher(endpoint,{method:"GET",redirect:"error",signal:AbortSignal.timeout(timeoutMs),
   headers:{"Authorization":"Bearer "+key,"OpenAI-Project":project,"OpenAI-Organization":org,"Accept":"application/json"}});
  if(response.status===401||response.status===403)return fail("CREDENTIAL_OR_PROJECT_ACCESS_DENIED");
  if(response.status!==200)return fail("MODEL_LOOKUP_UNAVAILABLE");
  if(response.headers.get("openai-organization")!==org)return fail("ORG_RESPONSE_NOT_CONFIRMED");
  if(!(response.headers.get("content-type")||"").toLowerCase().includes("application/json"))return fail("MODEL_RESPONSE_INVALID");
  const length=response.headers.get("content-length");
  if(length!==null&&(!/^\d+$/.test(length)||Number(length)>4096))return fail("MODEL_RESPONSE_TOO_LARGE");
  const reader=response.body?.getReader();
  if(!reader)return fail("MODEL_RESPONSE_INVALID");
  let total=0;const chunks=[];
  try{
   for(;;){const {done,value}=await reader.read();if(done)break;total+=value.byteLength;
    if(total>4096)return fail("MODEL_RESPONSE_TOO_LARGE");chunks.push(value);}
  }finally{try{await reader.cancel();}catch{}}
  const bytes=new Uint8Array(total);let at=0;
  for(const chunk of chunks){bytes.set(chunk,at);at+=chunk.byteLength;}
  const result=JSON.parse(new TextDecoder("utf-8",{fatal:true}).decode(bytes));
  if(result?.object!=="model"||result.id!==model)return fail("MODEL_ACCESS_NOT_CONFIRMED");
  // No IDs, headers, raw response, model output or key included in returned evidence.
  return {ok:true,code:"MODEL_METADATA_ACCESS_CONFIRMED",model,
   project_header_sent:true,org_response_matched:true,
   paid_inference_verified:false,hard_spending_cap_verified:false};
 }catch{return fail("NETWORK_OR_RESPONSE_UNVERIFIED");}
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const r=await probeOpenAiProject();
 console.log(JSON.stringify(r));
 if(!r.ok)process.exitCode=1;
}
