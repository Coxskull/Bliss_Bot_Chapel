// Private manual ALPHA-346 order tracking; never executes payment, dispatch or delivery.
// Node 20+: node scripts/alpha346-order-ledger.mjs QUOTE_INPUT.json EVENTS.json NEW_REPORT.json
// Replays operator evidence references; not a trusted Stripe or accounting ledger.
import {readFile,writeFile} from "node:fs/promises";
import {pathToFileURL} from "node:url";
import assert from "node:assert/strict";
import {estimateQuote} from "./alpha346-quote-costing.mjs";
import {deliverables} from "./alpha346-manual-proposal-gate.mjs";
export function replayOrder(quote, events, deliverables) {
  const ref=(x,n)=>{if(typeof x!=="string"||! /^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$/.test(x))throw Error("Invalid "+n);return x};
  const cents=(x)=>{if(!Number.isSafeInteger(x)||x<0||x>100000000)throw Error("Invalid cost or payment cents");return x};
  if(!quote||quote.currency!=="USD"||quote.status!=="DRAFT_NOT_SENT"||!Number.isSafeInteger(quote.subtotal)||quote.subtotal<=0||quote.total!==quote.subtotal+quote.tax)throw Error("Invalid source estimate");
  if(!Array.isArray(events)||events.length>1000)throw Error("Invalid event list");
  const state={quoteId:quote.quoteId,currency:"USD",status:"AWAITING_ACCEPTANCE",expectedTotalCents:quote.total,serviceSubtotalCents:quote.subtotal,paymentCents:0,actualCostCents:0,costs:[],assignments:Object.fromEntries(deliverables.map(d=>[d,{status:"UNASSIGNED"}])),events:0,evidenceStatus:"OPERATOR_REPORTED_NOT_INDEPENDENTLY_VERIFIED",revenueRecognized:false,contributionCents:null};
  const seen=new Set(),evidenceSeen=new Set();let last="";
  for(const e of events){
    if(!e||typeof e!=="object")throw Error("Invalid event");
    if(e.quoteId!==quote.quoteId)throw Error("Event belongs to a different quotation");
    const id=ref(e.id,"event ID");if(seen.has(id))throw Error("Duplicate event");seen.add(id);
    if(typeof e.at!=="string"||!/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/.test(e.at)||!Number.isFinite(Date.parse(e.at))||new Date(e.at).toISOString().replace(".000Z","Z")!==e.at||e.at<last)throw Error("Invalid event chronology");last=e.at;
    const ev=ref(e.evidenceRef,"private evidence reference");
    if(evidenceSeen.has(ev))throw Error("Duplicate evidence reference");evidenceSeen.add(ev);
    ref(e.operator,"operator ID");
    switch(e.type){
      case "ACCEPTANCE_RECORDED":
        if(state.status!=="AWAITING_ACCEPTANCE"||Object.values(quote.policies??{}).length!==4||Object.values(quote.policies).some(v=>!v||/^(pending|draft|unknown|tbd|unverified)/i.test(v))||e.acceptedTotalCents!==quote.total)throw Error("Accepted scope, policy and exact total required");
        state.status="AWAITING_PAYMENT";break;
      case "PAYMENT_RECORDED":
        if(state.status!=="AWAITING_PAYMENT"||e.livemode!==true||e.currency!=="USD"||e.amountCents!==quote.total||e.providerStatus!=="succeeded")throw Error("Full successful live payment record required");
        if(e.checkoutReference!==quote.quoteId||!/^cs_live_[A-Za-z0-9]{8,200}$/.test(e.checkoutSessionId??"")||!/^pi_[A-Za-z0-9]{8,200}$/.test(e.paymentIntentId??""))throw Error("Matching Stripe live checkout and payment intent references required");
        state.paymentCents=cents(e.amountCents);state.paymentReceipt={provider:"Stripe",checkoutSessionId:e.checkoutSessionId,paymentIntentId:e.paymentIntentId,checkoutReference:e.checkoutReference,evidenceRef:ev,verification:"OPERATOR_REPORTED_NOT_INDEPENDENTLY_VERIFIED"};state.status="PAID_REPORTED";break;
      case "ASSIGN":
        if(!["PAID_REPORTED","IN_PRODUCTION"].includes(state.status)||!Object.hasOwn(state.assignments,e.deliverable)||state.assignments[e.deliverable].status!=="UNASSIGNED")throw Error("Assignment unavailable");
        state.assignments[e.deliverable]={status:"ASSIGNED",assignee:ref(e.assignee,"assignee")};state.status="IN_PRODUCTION";break;
      case "COMPLETE":
        if(state.status!=="IN_PRODUCTION"||!Object.hasOwn(state.assignments,e.deliverable)||state.assignments[e.deliverable].status!=="ASSIGNED")throw Error("Assignment must precede completion");
        state.assignments[e.deliverable].status="COMPLETE_REPORTED";break;
      case "COST_RECORDED":
        if(!["PAID_REPORTED","IN_PRODUCTION","DELIVERED_REPORTED"].includes(state.status)||!["labor","creative","thirdParty","processor","other"].includes(e.category))throw Error("Invalid cost event");
        state.costs.push({category:e.category,cents:cents(e.amountCents),evidenceRef:ev});state.actualCostCents+=e.amountCents;break;
      case "DELIVERY_RECORDED":
        if(state.status!=="IN_PRODUCTION"||Object.values(state.assignments).some(a=>a.status!=="COMPLETE_REPORTED"))throw Error("All deliverables must be completed first");
        state.status="DELIVERED_REPORTED";break;
      case "COSTS_FINALIZED":
        if(state.status!=="DELIVERED_REPORTED"||e.allCostsRecorded!==true)throw Error("Delivery and full cost review required");
        state.status="CLOSED_REPORTED";state.contributionCents=quote.subtotal-state.actualCostCents;break;
      default:throw Error("Unknown event type");
    }
    state.events++;
  }
  return state;
}
function selfTest(){
 const quote={"quoteId":"DEMO","currency":"USD","status":"DRAFT_NOT_SENT","subtotal":14900,"tax":0,"total":14900,"policies":{"termsVersion":"terms-v1","refundVersion":"refund-v1","privacyVersion":"privacy-v1","supportContact":"support-v1"}};
 const seq=[{"type":"ACCEPTANCE_RECORDED","acceptedTotalCents":14900,"id":"demo-0","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-0"},{"type":"PAYMENT_RECORDED","livemode":true,"currency":"USD","amountCents":14900,"providerStatus":"succeeded","id":"demo-1","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-1"},{"type":"ASSIGN","deliverable":"campaign_discovery_brief","assignee":"operator-1","id":"demo-2","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-2"},{"type":"COMPLETE","deliverable":"campaign_discovery_brief","id":"demo-3","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-3"},{"type":"ASSIGN","deliverable":"creator_channel_suitability_plan","assignee":"operator-1","id":"demo-4","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-4"},{"type":"COMPLETE","deliverable":"creator_channel_suitability_plan","id":"demo-5","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-5"},{"type":"ASSIGN","deliverable":"creative_direction_outline","assignee":"operator-1","id":"demo-6","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-6"},{"type":"COMPLETE","deliverable":"creative_direction_outline","id":"demo-7","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-7"},{"type":"ASSIGN","deliverable":"coordination_schedule","assignee":"operator-1","id":"demo-8","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-8"},{"type":"COMPLETE","deliverable":"coordination_schedule","id":"demo-9","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-9"},{"type":"ASSIGN","deliverable":"final_planning_summary","assignee":"operator-1","id":"demo-10","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-10"},{"type":"COMPLETE","deliverable":"final_planning_summary","id":"demo-11","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-11"},{"type":"COST_RECORDED","category":"labor","amountCents":7000,"id":"demo-12","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-12"},{"type":"DELIVERY_RECORDED","id":"demo-13","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-13"},{"type":"COSTS_FINALIZED","allCostsRecorded":true,"id":"demo-14","at":"2026-11-25T00:00:00Z","operator":"fixture","evidenceRef":"fictional-14"}].map(e=>({...e,quoteId:quote.quoteId,...(e.type==="PAYMENT_RECORDED"?{checkoutReference:quote.quoteId,checkoutSessionId:"cs_live_FICTIONAL123",paymentIntentId:"pi_FICTIONAL123"}:{})}));
 const s=replayOrder(quote,seq,deliverables);
 assert.equal(s.status,"CLOSED_REPORTED");assert.equal(s.contributionCents,7900);assert.equal(s.revenueRecognized,false);
 for(const events of [[seq[1]],[seq[0],{...seq[1],livemode:false}],[seq[0],{...seq[1],amountCents:14800}],seq.slice(0,3).concat(seq.at(-2)),[seq[0],seq[0]],[{...seq[0],quoteId:"OTHER"}],[seq[0],{...seq[1],checkoutReference:"OTHER"}],[seq[0],{...seq[1],checkoutSessionId:"cs_test_FICTIONAL123"}]])assert.throws(()=>replayOrder(quote,events,deliverables));
 console.log("PASS: fictional lifecycle, contribution, and rejection of invalid transitions");
}
async function main(){
 const a=process.argv.slice(2);
 if(a.length===1&&a[0]==="--self-test"){selfTest();return;}
 if(a.length!==3)throw Error("Usage: node scripts/alpha346-order-ledger.mjs QUOTE_INPUT.json EVENTS.json NEW_REPORT.json");
 const quote=estimateQuote(JSON.parse(await readFile(a[0],"utf8")));
 const events=JSON.parse(await readFile(a[1],"utf8"));
 const report=replayOrder(quote,events,deliverables);
 await writeFile(a[2],JSON.stringify(report,null,2)+"\n",{flag:"wx",mode:0o600});
 console.log("Private operator-reported order status exported; no external actions performed.");
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)main().catch(e=>{console.error(e.message);process.exitCode=1;});
