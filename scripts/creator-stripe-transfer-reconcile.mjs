// Operator-only, GET-only Stripe Connect transfer reconciliation.
// Transfers to connected Stripe accounts are not bank payouts.
// Inputs must be privately reviewed against the original settlement evidence.
import {createHash} from "node:crypto";
import {readFile,writeFile} from "node:fs/promises";
import {pathToFileURL} from "node:url";
const hash=v=>createHash("sha256").update(v,"utf8").digest("hex");
const guid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const hex=/^[0-9a-f]{64}$/;
const stripeId=(v,p)=>typeof v==="string"&&v.startsWith(p)&&/^[A-Za-z0-9_]{8,200}$/.test(v);
export function transferReferenceSha256(transferId){
  if(!stripeId(transferId,"tr_"))throw Error("Exact transfer ID required");
  return hash("stripe:live:connect-transfer:"+transferId);
}
export function stripeTransferReader(key,fetcher=fetch){
  if(typeof key!=="string"||!key.startsWith("rk_live_")||key.length<20)throw Error("Restricted live read key required");
  return async transferId=>{
    if(!stripeId(transferId,"tr_"))throw Error("Exact transfer ID required");
    const r=await fetcher("https://api.stripe.com/v1/transfers/"+transferId,{method:"GET",headers:{Authorization:"Bearer "+key,Accept:"application/json"},
      redirect:"error",signal:AbortSignal.timeout(10000)});
    if(!r.ok)throw Error("Stripe transfer read unavailable");
    return r.json();
  };
}
export async function reconcileCreatorTransfer(input,read,now=()=>new Date()){
  if(!input||input.schema!=="alpha-creator-transfer-review/v1"||!guid.test(input.evidenceId)||!guid.test(input.historyId)||
    !guid.test(input.placementId)||!guid.test(input.creatorId)||!hex.test(input.originalDigest)||
    !hex.test(input.transferReferenceSha256)||!stripeId(input.transferId,"tr_")||!stripeId(input.destinationAccountId,"acct_")||
    input.currency!=="USD"||!Number.isSafeInteger(input.amountMinor)||input.amountMinor<=0||
    !Number.isSafeInteger(input.completedAtUnix)||input.completedAtUnix<=0||
    input.recipientMappingReviewed!==true||input.originalEvidenceReviewed!==true||
    transferReferenceSha256(input.transferId)!==input.transferReferenceSha256)
    throw Error("Exact USD settlement evidence and independently reviewed recipient mapping required");
  const t=await read(input.transferId);
  const date=now(),verifiedAt=date.toISOString();
  if(!t||t.object!=="transfer"||t.id!==input.transferId||t.livemode!==true||
    t.destination!==input.destinationAccountId||t.amount!==input.amountMinor||t.currency!=="usd"||
    t.created!==input.completedAtUnix||t.created>Math.floor(date.getTime()/1000)||
    t.metadata?.alpha_placement_id!==input.placementId||t.metadata?.alpha_creator_id!==input.creatorId||
    !Number.isSafeInteger(t.amount_reversed)||t.amount_reversed<0||t.amount_reversed>t.amount||
    typeof t.reversed!=="boolean"||t.reversed!==(t.amount_reversed===t.amount))
    throw Error("Provider transfer does not match reviewed settlement");
  const status=t.amount_reversed===t.amount?"FULL_REVERSAL_OBSERVED":t.amount_reversed>0?"PARTIAL_REVERSAL_OBSERVED":"CONNECT_TRANSFER_MATCHED_AT_READ_TIME";
  return {schema:"alpha-creator-connect-transfer-evidence/v1",verifiedAt,evidenceId:input.evidenceId,historyId:input.historyId,
    placementId:input.placementId,creatorId:input.creatorId,originalDigest:input.originalDigest,
    transferReferenceSha256:input.transferReferenceSha256,destinationAccountSha256:hash(input.destinationAccountId),
    amountMinor:t.amount,currency:"USD",amountReversedMinor:t.amount_reversed,transferCreatedUnix:t.created,status,
    providerReadPerformed:true,bankPayoutVerified:false,disputesVerified:false,liabilityVerified:false,
    refundAuthorized:false,transferAuthorized:false,requiresAuthorizedOperatorReview:true};
}
async function main(){
  const args=process.argv.slice(2);
  if(args.length!==2)throw Error("Usage: node scripts/creator-stripe-transfer-reconcile.mjs PRIVATE_INPUT.json NEW_PRIVATE_EVIDENCE.json");
  const input=JSON.parse(await readFile(args[0],"utf8"));
  const result=await reconcileCreatorTransfer(input,stripeTransferReader(process.env.ALPHA_CREATOR_STRIPE_RESTRICTED_READ_KEY));
  await writeFile(args[1],JSON.stringify(result,null,2)+"\n",{flag:"wx",mode:0o600});
  console.log("ALPHA_CREATOR_TRANSFER_READ="+result.status+"; no payout or refund authorized");
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)main().catch(()=>{
  console.error("ALPHA_CREATOR_TRANSFER_READ=REFUSED_OR_UNAVAILABLE; no payout or refund authorized");process.exitCode=1;
});
