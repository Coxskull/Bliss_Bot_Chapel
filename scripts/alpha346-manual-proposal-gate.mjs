import {createHash} from 'node:crypto';
import {readFile} from 'node:fs/promises';
export const deliverables=Object.freeze(['campaign_discovery_brief','creator_channel_suitability_plan','creative_direction_outline','coordination_schedule','final_planning_summary']);
export function inspect(p) {
 if (!p || p.fixture!=='SYNTHETIC_ONLY' || p.provider!=='ALPHA GLOBAL MEDIA LLC' || p.serviceId!=='AGM-MANUAL-CAMPAIGN-PLANNING-001') throw Error('Unauthorized fixture');
 if (!/^SYN-ALPHA-346-[0-9]{4}$/.test(p.inquiryReceipt??'')) throw Error('Synthetic receipt only');
 if (p.inquiryStatus!=='NOT_YET_VERIFIED' || p.crmStatus!=='DELIVERED') throw Error('Unverified pipeline status');
 if (p.globalStop!=='ENGAGED'||p.creatorAuthorization!=='NOT_VERIFIED'||p.delivery!=='NOT_SENT'||p.paymentEnabled!==false||p.outreachEnabled!==false||p.campaignReady!==false||p.customerAccepted!==false) throw Error('Unsafe state');
 if (!Array.isArray(p.deliverables)||p.deliverables.length!==5||!deliverables.every((d,i)=>p.deliverables[i]===d))throw Error('Deliverable mismatch');
 const requirements=['ownerApprovedScope','verifiedCustomerSupport','commercialTermsApproved','refundPolicyApproved','commercialPrivacyApproved','financeApprovedQuote','verifiedCustomerIdentity','approvedTimelineAndRevisions'];
 const missing=requirements.filter(x=>p[x]!==true);
 if (!p.price||!Number.isSafeInteger(p.price.amountMinor)||p.price.amountMinor<=0||!/^[A-Z]{3}$/.test(p.price.currency??''))missing.push('approvedAmountAndCurrency');
 if (!Number.isSafeInteger(p.quoteVersion)||p.quoteVersion<=0)missing.push('quoteVersion');
 return Object.freeze({directiveId:'ALPHA-FIRST-CUSTOMER-LAUNCH-346',synthetic:true,validation:missing.length?'BLOCKED_PENDING_BUSINESS_APPROVALS':'OFFLINE_READY_FOR_HUMAN_REVIEW_ONLY',missingApprovals:missing,deliverableCount:5,fingerprintSha256:createHash('sha256').update(JSON.stringify({schema:'ALPHA346_PROPOSAL_V2',id:p.inquiryReceipt,provider:p.provider,serviceId:p.serviceId,quoteVersion:p.quoteVersion??null,price:p.price?{amountMinor:p.price.amountMinor,currency:p.price.currency}:null,approvals:requirements.map(key=>[key,p[key]===true]),requirements:missing,deliverables:p.deliverables})).digest('hex'),customerContactPerformed:false,paymentEnabled:false,creatorAuthorized:false,campaignReady:false,delivery:'NOT_SENT',globalStop:'ENGAGED'});
}
if (process.argv[1]?.endsWith('alpha346-manual-proposal-gate.mjs')) {
 try {
  if (process.argv.length!==3||!process.argv[2].endsWith('alpha346-synthetic-proposal.json'))throw Error('Synthetic fixture path required');
  const result=inspect(JSON.parse(await readFile(process.argv[2],'utf8')));
  console.log(JSON.stringify(result));
  if(result.validation!=='OFFLINE_READY_FOR_HUMAN_REVIEW_ONLY')process.exitCode=3;
 } catch {console.error('Synthetic-only ALPHA proposal preflight rejected input');process.exitCode=4;}
}