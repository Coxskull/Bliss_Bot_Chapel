// Operator-only read-only Stripe reconciliation. No payment, refund, or fulfillment writes.
// node scripts/alpha346-stripe-reconcile.mjs PRIVATE-quote-input.json PRIVATE-order-events.json cs_live_... NEW-evidence.json
import {readFile,writeFile} from "node:fs/promises";
import {pathToFileURL} from "node:url";
import {estimateQuote} from "./alpha346-quote-costing.mjs";
import {replayOrder} from "./alpha346-order-ledger.mjs";
import {deliverables} from "./alpha346-manual-proposal-gate.mjs";
const link="plink_1UfWXNH6XjUXn6bha65jeI1t", price="price_1UfWONH6XjUXn6bhPYLJNax5", product="prod_VguCvW3SA1yQUM";
const id=(value,prefix)=>typeof value==="string"&&value.startsWith(prefix)&&/^[A-Za-z0-9_]{8,200}$/.test(value);
export async function reconcile(input,events,sessionId,read) {
  const quote=estimateQuote(input);
  const order=replayOrder(quote,events,deliverables);
  if(quote.packageKind!=="starter"||quote.total!==14900||order.status!=="AWAITING_PAYMENT")throw Error("Accepted $149 Starter Package order required");
  if(!id(sessionId,"cs_live_"))throw Error("Live Checkout Session required");
  // Email must be verified from the accepted order by the operator, not inferred from a quote reference.
  const email=typeof input.acceptedCustomerEmail==="string"?input.acceptedCustomerEmail.trim().toLowerCase():"";
  if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)||email.length>254)throw Error("Accepted customer email required");
  const session=await read("/v1/checkout/sessions/"+sessionId);
  if(session.id!==sessionId||session.object!=="checkout.session"||session.livemode!==true||session.payment_link!==link||session.client_reference_id!==quote.quoteId||session.mode!=="payment"||session.status!=="complete"||session.payment_status!=="paid"||session.currency!=="usd"||session.amount_total!==14900||session.amount_subtotal!==14900||!id(session.payment_intent,"pi_")||session.customer_details?.email?.trim().toLowerCase()!==email)throw Error("Checkout does not match accepted live order");
  const items=await read("/v1/checkout/sessions/"+sessionId+"/line_items?limit=2");
  const item=items.data?.[0];
  if(items.has_more!==false||items.data?.length!==1||item.quantity!==1||item.amount_total!==14900||item.currency!=="usd"||item.price?.id!==price||item.price?.product!==product||item.price?.unit_amount!==14900)throw Error("Checkout line items do not match Starter Package");
  const intent=await read("/v1/payment_intents/"+session.payment_intent);
  if(intent.id!==session.payment_intent||intent.livemode!==true||intent.status!=="succeeded"||intent.currency!=="usd"||intent.amount_received!==14900||!id(intent.latest_charge,"ch_"))throw Error("Payment Intent not fully settled");
  const charge=await read("/v1/charges/"+intent.latest_charge);
  if(charge.id!==intent.latest_charge||charge.livemode!==true||charge.payment_intent!==intent.id||charge.currency!=="usd"||charge.amount!==14900||charge.amount_captured!==14900||charge.status!=="succeeded"||charge.paid!==true||charge.captured!==true||charge.refunded!==false||charge.amount_refunded!==0||charge.disputed!==false)throw Error("Charge unsettled, refunded, or disputed");
  return {schema:"alpha-starter-merchant-evidence/v1",verifiedAt:new Date().toISOString(),quoteId:quote.quoteId,checkoutSessionId:sessionId,paymentIntentId:intent.id,chargeId:charge.id,paymentLinkId:link,priceId:price,productId:product,amountCents:14900,currency:"USD",livemode:true,customerMatched:true,status:"MERCHANT_PAYMENT_VERIFIED_AT_READ_TIME",fulfillmentAuthorized:false,revenueRecognized:false,requiresPrivateOrderAndDuplicateReview:true};
}
export function stripeReader(key, fetcher=fetch) {
  if(typeof key!=="string"||!key.startsWith("rk_live_")||key.length<20)throw Error("Restricted live read-only Stripe key required");
  return async path=>{
    if(!/^\/v1\/(?:checkout\/sessions\/cs_live_[A-Za-z0-9_]+(?:\/line_items\?limit=2)?|payment_intents\/pi_[A-Za-z0-9_]+|charges\/ch_[A-Za-z0-9_]+)$/.test(path))throw Error("Unexpected read path");
    const r=await fetcher("https://api.stripe.com"+path,{method:"GET",headers:{Authorization:"Bearer "+key},redirect:"error",signal:AbortSignal.timeout(10000)});
    if(!r.ok)throw Error("Stripe read refused or unavailable (HTTP "+r.status+")");
    return r.json();
  };
}
async function main(){
 const args=process.argv.slice(2);
 if(args.length!==4)throw Error("Usage: node scripts/alpha346-stripe-reconcile.mjs QUOTE_INPUT.json EVENTS.json cs_live_ID NEW_EVIDENCE.json");
 const input=JSON.parse(await readFile(args[0],"utf8")),events=JSON.parse(await readFile(args[1],"utf8"));
 const result=await reconcile(input,events,args[2],stripeReader(process.env.ALPHA_STRIPE_RESTRICTED_READ_KEY));
 await writeFile(args[3],JSON.stringify(result,null,2)+"\n",{flag:"wx",mode:0o600});
 console.log("ALPHA_STARTER_MERCHANT_PAYMENT_VERIFIED=PASS; fulfillment still requires authorized operator review");
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)main().catch(()=>{console.error("ALPHA_STARTER_MERCHANT_PAYMENT_VERIFIED=REFUSED_OR_UNAVAILABLE; no fulfillment authorized");process.exitCode=1;});
