// Task20 private, READ-ONLY OpenAI project-cost evidence. No database writes.
// This is organization-admin API use, distinct from service-key model access.
// Never print, store or upload credentials. GitHub CI uses only mock Responses.
import {pathToFileURL} from "node:url";
const PINNED_PROJECT="proj_RDrdpdY1ufheFvoJu7mLWhDc";
const BASE="https://api.openai.com/v1/organization/costs";
const LIMIT=131072;
const fail=code=>({ok:false,code});
const id=(s,p)=>typeof s==="string"&&new RegExp("^"+p+"_[A-Za-z0-9_-]{4,96}$").test(s);
async function readBoundedJson(response) {
 const length=response.headers.get("content-length");
 if(length!==null && (!/^[0-9]+$/.test(length)||Number(length)>LIMIT))
   throw Error("BODY_TOO_LARGE");
 const reader=response.body?.getReader();
 if(!reader)throw Error("BODY_MISSING");
 const chunks=[];let size=0;
 try {
  for(;;) {
   const {done,value}=await reader.read();
   if(done)break;
   size+=value.byteLength;
   if(size>LIMIT)throw Error("BODY_TOO_LARGE");
   chunks.push(value);
  }
 }finally{try{await reader.cancel();}catch{}}
 const out=new Uint8Array(size);let i=0;
 for(const chunk of chunks){out.set(chunk,i);i+=chunk.byteLength;}
 return JSON.parse(new TextDecoder("utf-8",{fatal:true}).decode(out));
}
export async function readOpenAiCurrentMonthCosts({
 env=process.env,fetcher=fetch,now=new Date(),timeoutMs=8000,
 expectedProject=PINNED_PROJECT
}={}) {
 if(env.TASK20_COSTS_READONLY_OPT_IN!=="YES")return fail("OPERATOR_OPT_IN_REQUIRED");
 const admin=env.OPENAI_ADMIN_KEY;
 const org=env.ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID;
 const project=env.ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID;
 if(!id(org,"org"))return fail("ORG_ID_NOT_CONFIGURED");
 if(!id(project,"proj")||project!==expectedProject)return fail("PROJECT_ID_NOT_MATCHING_APPROVED");
 if(typeof admin!=="string"||admin.length<16||admin.length>512||/[\s\x00-\x1f]/.test(admin))
   return fail("ADMIN_KEY_NOT_CONFIGURED");
 const today=now instanceof Date?now:new Date(NaN);
 const end=Math.floor(today.getTime()/1000);
 if(!Number.isFinite(today.getTime())||!Number.isSafeInteger(end)||end<0)
   return fail("DATE_INVALID");
 if(!Number.isSafeInteger(timeoutMs)||timeoutMs<1||timeoutMs>10000)
   return fail("TIMEOUT_INVALID");
 const year=today.getUTCFullYear(),month=today.getUTCMonth();
 const start=Math.floor(Date.UTC(year,month,1)/1000);
 if(end<=start)return fail("DATE_INVALID");
 const u=new URL(BASE);
 u.searchParams.set("start_time",String(start));
 u.searchParams.set("end_time",String(end));
 u.searchParams.set("bucket_width","1d");
 u.searchParams.set("limit","50");
 u.searchParams.append("project_ids[]",project);
 u.searchParams.append("group_by[]","project_id");
 let response;
 try {
  response=await fetcher(u.toString(),{
   method:"GET",redirect:"error",signal:AbortSignal.timeout(timeoutMs),
   headers:{Authorization:"Bearer "+admin,"OpenAI-Organization":org,Accept:"application/json"}
  });
 }catch{return fail("NETWORK_OR_RESPONSE_UNVERIFIED");}
 // Never retrieve provider error bodies or log any request headers.
 if(response.status===401||response.status===403)return fail("ADMIN_ACCESS_DENIED");
 if(response.status!==200)return fail("COSTS_API_UNAVAILABLE");
 if(!(response.headers.get("content-type")||"").toLowerCase().includes("application/json"))
   return fail("RESPONSE_NOT_JSON");
 let page;
 try{page=await readBoundedJson(response);}
 catch{return fail("RESPONSE_INVALID_OR_TOO_LARGE");}
 if(!page||page.object!=="page"||!Array.isArray(page.data))
   return fail("COSTS_PAGE_SCHEMA_INVALID");
 // A UTC month is <=31 one-day buckets, so 50 bucket limit is sufficient.
 // Never accept a partial page as a complete project spend total.
 if(page.has_more!==false||page.next_page!==null)
   return fail("COSTS_PAGE_INCOMPLETE");
 let micro=0,records=0;
 const seen=new Set();
 for(const bucket of page.data) {
  if(!bucket||bucket.object!=="bucket"||!Number.isSafeInteger(bucket.start_time)||
     !Number.isSafeInteger(bucket.end_time)||bucket.end_time<=bucket.start_time||
     bucket.start_time<start||bucket.end_time>end+86400||
     !Array.isArray(bucket.results))return fail("COST_BUCKET_INVALID");
  if(seen.has(bucket.start_time))return fail("COST_BUCKET_DUPLICATE");
  seen.add(bucket.start_time);
  for(const item of bucket.results) {
   if(!item||item.object!=="organization.costs.result"||
      item.project_id!==project||item.amount?.currency!=="usd"||
      typeof item.amount?.value!=="number"||
      !Number.isFinite(item.amount.value)||item.amount.value<0||
      item.amount.value>1000000)
     return fail("PROJECT_COST_ENTRY_UNVERIFIED");
   const cents=Math.round(item.amount.value*1000000);
   if(!Number.isSafeInteger(cents)||micro>Number.MAX_SAFE_INTEGER-cents)
     return fail("COST_AMOUNT_OVERFLOW");
   micro+=cents;records++;
  }
 }
 if(records===0)return fail("NO_PROJECT_COST_RECORDS");
 const costUsd=(micro/1000000).toFixed(6);
 return {
  ok:true,code:micro>=9000000?"COSTS_RETRIEVED_AT_OR_OVER_CAP":"COSTS_RETRIEVED_FOR_REVIEW",
  month_utc:today.toISOString().slice(0,7),
  as_of_utc:today.toISOString(),
  project_scope_matched:true,
  bucket_count:seen.size,
  cost_record_count:records,
  project_cost_usd:costUsd,
  project_limit_usd:"9.000000",
  amount_below_9:micro<9000000,
  project_usage_finalized:false,
  api_key_model_access_verified:false,
  provider_limit_independently_verified:false,
  safe_to_refresh_database_meter_automatically:false,
  public_ai_activation_authorized_by_this_check:false
 };
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href) {
 const result=await readOpenAiCurrentMonthCosts();
 console.log(JSON.stringify(result));
 if(!result.ok)process.exitCode=1;
}
