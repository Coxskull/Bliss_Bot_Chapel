// ALPHA-346 operator-run proposal preparation and private estimated cost worksheet.
// Node 20+: node scripts/alpha346-quote-costing.mjs INPUT.json NEW_OUTPUT_DIRECTORY
// Does not send a proposal, collect payment, approve terms or start fulfillment.
import {readFile,mkdir,writeFile} from "node:fs/promises";
import {join} from "node:path";
import {pathToFileURL} from "node:url";
import assert from "node:assert/strict";
import {deliverables} from "./alpha346-manual-proposal-gate.mjs";
export function estimateQuote(input) {
  if(!input||typeof input!=="object"||Array.isArray(input))throw Error("Expected quote input");
  const text=(v,name,max=200)=>{if(typeof v!=="string"||!v.trim()||v.trim().length>max||/[\u0000-\u001f\u007f]/.test(v))throw Error("Invalid "+name);return v.trim();};
  const money=(v,name)=>{if(!Number.isSafeInteger(v)||v<0||v>100000000)throw Error("Invalid cents: "+name);return v;};
  const id=text(input.quoteId,"quote ID",64);
  if(!/^[A-Za-z0-9_-]+$/.test(id))throw Error("Quote ID must use letters, digits, underscores or hyphens");
  if(input.currency!=="USD")throw Error("This initial USD service requires USD; do not convert automatically");
  const packageKind=input.packageKind??"planning";
  if(!["planning","starter"].includes(packageKind))throw Error("Unknown service package");
  const subtotal=money(input.serviceCents,"service price"),tax=money(input.taxCents,"tax");
  if(packageKind==="starter"&&subtotal!==14900)throw Error("Starter service price must be $149 USD before separately agreed tax");
  if(subtotal===0)throw Error("Service price must be positive");
  const costs={};
  for(const k of ["laborCents","creativeCents","thirdPartyCents","processorCents","otherCents"])costs[k]=money(input.costs?.[k],k);
  const costTotal=Object.values(costs).reduce((a,b)=>a+b,0);
  const contribution=subtotal-costTotal;
  const marginBps=Math.round(contribution*10000/subtotal);
  const revisions=input.revisions;
  if(!Number.isInteger(revisions)||revisions<0||revisions>20)throw Error("Invalid revision count");
  const customer=text(input.customer,"customer"),market=text(input.market,"market"),scope=text(input.scope,"scope",500),schedule=text(input.schedule,"schedule",300);
  const policies={};
  for(const k of ["termsVersion","refundVersion","privacyVersion","supportContact"])policies[k]=text(input.policies?.[k],k);
  return {quoteId:id,packageKind,currency:"USD",customer,market,scope,schedule,revisions,policies,subtotal,tax,total:subtotal+tax,costs,costTotal,contribution,marginBps,status:"DRAFT_NOT_SENT",costStatus:"ESTIMATED_NOT_ACTUAL",paymentStatus:"NOT_REQUESTED",fulfillmentStatus:"NOT_STARTED"};
}
const example={
  "quoteId": "ALPHA-SAMPLE-001",
  "currency": "USD",
  "customer": "Example business (fictional)",
  "market": "Example market",
  "scope": "Five planning deliverables, no finished advertising media",
  "schedule": "Proposed delivery timing to be confirmed",
  "revisions": 1,
  "serviceCents": 14900,
  "taxCents": 0,
  "costs": {
    "laborCents": 6000,
    "creativeCents": 0,
    "thirdPartyCents": 0,
    "processorCents": 500,
    "otherCents": 500
  },
  "policies": {
    "termsVersion": "PENDING",
    "refundVersion": "PENDING",
    "privacyVersion": "PENDING",
    "supportContact": "PENDING"
  }
};
export function customerDraft(q){
 const dollars=c=>"$"+(c/100).toFixed(2)+" USD";
 const md=s=>s.replace(/[\\`*_{}\[\]()<>#|!~]/g,"\\$&");
 return [
 q.packageKind==="starter"?"# ALPHA GLOBAL MEDIA LLC — Digital Advertising Starter Package":"# ALPHA GLOBAL MEDIA LLC — Campaign Planning & Coordination",
 "**DRAFT — NOT AN OFFER OR INVOICE; review before sending**",
 "Quote: "+md(q.quoteId),"Customer: "+md(q.customer),"Market: "+md(q.market),
 "","## Scope",md(q.scope),"",
 ...(q.packageKind==="starter"?["brand_and_campaign_brief","custom_advertising_artwork","promotional_content_design","digital_marketing_materials","customer_review_and_delivery"]:deliverables).map((d,i)=>(i+1)+". "+d.replaceAll("_"," ")),
 "",q.packageKind==="starter"?"Exclusions: No booked creators, paid placement, publication, guaranteed audience or results. Specify asset count, formats, usage rights and acceptance in the written scope.":"Exclusions: No finished advertising media, booked creators, paid placement, publication, guaranteed audience or results.",
 "","Proposed schedule: "+md(q.schedule),"Included revisions: "+q.revisions,
 "","## Proposed price","Service: "+dollars(q.subtotal),"Tax (operator-entered, verify applicability): "+dollars(q.tax),"Total: "+dollars(q.total),
 "","Terms: "+md(q.policies.termsVersion),"Cancellation/refund: "+md(q.policies.refundVersion),"Privacy: "+md(q.policies.privacyVersion),"Support: "+md(q.policies.supportContact),
 "","Written scope, policy, tax, merchant and customer approval required. No payment requested, no order accepted and no delivery initiated."
 ].join("\n")+"\n";
}
function selfTest(){
 const q=estimateQuote(example);
 assert.equal(q.total,14900);assert.equal(q.costTotal,7000);assert.equal(q.contribution,7900);assert.equal(q.marginBps,5302);
 assert.ok(estimateQuote({...example,costs:{...example.costs,laborCents:20000}}).contribution<0);
 for(const change of [{taxCents:-1},{currency:"PHP"},{serviceCents:0},{costs:{...example.costs,processorCents:undefined}},{scope:"line\nbreak"}])assert.throws(()=>estimateQuote({...example,...change}));
 const draft=customerDraft(q);assert.ok(draft.includes("$149.00 USD"));assert.ok(draft.includes("DRAFT"));assert.ok(!draft.includes("$70.00"));assert.ok(!draft.includes("marginBps"));
 const starter=estimateQuote({...example,packageKind:"starter",scope:"Custom artwork and agreed promotional materials"});
 assert.equal(starter.packageKind,"starter");assert.match(customerDraft(starter),/custom advertising artwork/);assert.match(customerDraft(starter),/promotional content design/);assert.doesNotMatch(customerDraft(starter),/No finished advertising media/);assert.throws(()=>estimateQuote({...example,packageKind:"starter",serviceCents:14800}));assert.throws(()=>estimateQuote({...example,packageKind:"unknown"}));
 console.log("PASS: cent-based costing, loss detection, input validation, customer draft separation");
}
async function main(){
 const a=process.argv.slice(2);
 if(a.length===1&&a[0]==="--self-test"){selfTest();return;}
 if(a.length!==2)throw Error("Usage: node scripts/alpha346-quote-costing.mjs INPUT.json NEW_OUTPUT_DIRECTORY");
 const quote=estimateQuote(JSON.parse(await readFile(a[0],"utf8")));
 await mkdir(a[1]);
 await writeFile(join(a[1],"PRIVATE-estimate.json"),JSON.stringify(quote,null,2)+"\n",{flag:"wx",mode:0o600});
 await writeFile(join(a[1],"customer-proposal-DRAFT.md"),customerDraft(quote),{flag:"wx",mode:0o600});
 console.log("Created private cost estimate and unsent customer proposal draft.");
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)main().catch(e=>{console.error(e.message);process.exitCode=1;});
