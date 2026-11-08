import assert from "node:assert/strict";
import {runSyntheticBudgetWorkflow,evaluateVerifiedInventory,calculateSustainableQuote,parseBudgetToMinor,MARKET_CURRENCIES} from "../supabase/functions/alpha-public-intake/marketplace-budget.mjs";
import {normalizeStructuredAdvertiser,BUDGET_NOTICE} from "../supabase/functions/alpha-public-intake/marketplace-intake.mjs";

const now="2026-11-08T12:00:00.000Z";
const structured=normalizeStructuredAdvertiser({
  budget_amount:"900.00",budget_currency:"PHP",market_code:"MANILA",
  campaign_objective:"awareness",preferred_media_formats:["podcast","radio"],
  audience_requirements:"",preferred_languages:["English","Filipino"],contact_consent:true
});
assert.equal(BUDGET_NOTICE,"AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1");
assert.equal(structured.market,"Manila, Philippines");
assert.equal(structured.budget_amount_minor,90000);
assert.equal(structured.audience_requirements,null);
assert.throws(()=>normalizeStructuredAdvertiser({
  budget_amount:"900",budget_currency:"USD",market_code:"MANILA",
  campaign_objective:"awareness",preferred_media_formats:["podcast"],
  preferred_languages:["English"],contact_consent:true
}),/INVALID_OR_UNCONSENTED_ADVERTISER_INTAKE/);
assert.throws(()=>normalizeStructuredAdvertiser({
  budget_amount:"1000.25",budget_currency:"IDR",market_code:"JAKARTA",
  campaign_objective:"awareness",preferred_media_formats:["podcast"],
  preferred_languages:["English"],contact_consent:true
}),/CURRENCY_PRECISION/);
assert.throws(()=>normalizeStructuredAdvertiser({
  budget_amount:"900",budget_currency:"PHP",market_code:"MANILA",
  campaign_objective:"awareness",preferred_media_formats:["podcast"],
  preferred_languages:["English"],contact_consent:false
}),/INVALID_OR_UNCONSENTED_ADVERTISER_INTAKE/);
assert.deepEqual(Object.keys(MARKET_CURRENCIES).sort(),["JAKARTA","KUALA_LUMPUR","MANILA","MEDELLIN","PANAMA_CITY","SANTO_DOMINGO"]);
assert.equal(parseBudgetToMinor("900.00","PHP"),90000);
assert.equal(parseBudgetToMinor("100000","IDR"),100000);
assert.throws(()=>parseBudgetToMinor("1000.25","IDR"),/CURRENCY_PRECISION/);
assert.throws(()=>parseBudgetToMinor("1,000","PHP"),/INVALID_BUDGET/);
const evidence={status:"VERIFIED",sourceRef:"SYNTHETIC_FIXTURE_SOURCE",
  sha256:"a".repeat(64),observedAt:"2026-11-07T12:00:00.000Z",expiresAt:"2026-12-08T12:00:00.000Z"};
const planningQuote=calculateSustainableQuote({
  kind:"PLANNING_ONLY",marketCode:"MANILA",currency:"PHP",budgetCurrency:"PHP",
  budgetMinor:90000,customerRequirements:"Synthetic advertiser requests a local awareness plan.",
  requirementsVerified:true,customerRequirementsEvidence:evidence,
  scope:"One campaign brief, channel plan, and measurement outline.",
  scopeConfirmed:true,scopeEvidence:evidence,
  deliveryCapacity:{status:"CONFIRMED",ownerRef:"SYNTHETIC-OPERATOR",
    windowStart:"2026-11-10T00:00:00.000Z",windowEnd:"2026-11-20T00:00:00.000Z"},
  capacityEvidence:evidence,
  terms:{status:"CONFIRMED",reference:"SYNTHETIC-TERMS-V1"},termsEvidence:evidence,
  laborCostEvidence:evidence,deliveryCostEvidence:evidence,
  processingCostEvidence:evidence,taxEvidence:evidence,
  costs:{creatorCompensationMinor:0,alphaFeeMinor:30000,laborMinor:10000,
    deliveryMinor:2000,paymentProcessingMinor:1000,taxMinor:1000}
},policy,new Date(now));
assert.equal(planningQuote.eligible,true);
assert.equal(planningQuote.status,"DRAFT");
assert.equal(planningQuote.costs.totalAdvertiserCostMinor,44000);
assert.equal(planningQuote.costs.contributionMinor,16000);
assert.equal(planningQuote.costs.creatorCompensationMinor,0);
assert.equal(planningQuote.serviceType,"PLANNING_ONLY");
assert.equal(calculateSustainableQuote({
  kind:"PLANNING_ONLY",marketCode:"MANILA",currency:"PHP",budgetCurrency:"PHP",
  budgetMinor:90000,costs:{alphaFeeMinor:30000,laborMinor:10000,deliveryMinor:2000,
    paymentProcessingMinor:1000,taxMinor:1000}
},policy,new Date(now)).reason,"CUSTOMER_REQUIREMENTS_UNVERIFIED");

const brief={kind:"advertiser",intakeId:"SYNTHETIC-INTAKE-001",
  contactName:"Synthetic Market Buyer",contactEmail:"buyer@example.invalid",
  budgetMinor:90000,currency:"PHP",marketCode:"MANILA",objective:"awareness",
  preferredMediaFormats:["podcast","digital_creator"],audienceRequirements:"Synthetic Manila audience",minimumTargetMarketShareBasisPoints:5000,
  languages:["English","Filipino"],contactConsent:true};
const policy={policyId:"SYNTHETIC-MARGIN-POLICY-01",
  minimumContributionMinor:10000,minimumContributionBasisPoints:1500};
const offer=(o)=>({status:"AVAILABLE",verifiedInventory:true,marketCode:"MANILA",currency:"PHP",
  mediaFormat:"podcast",languages:["English"],verifiedAudience:12000,
  audienceEvidence:evidence,availabilityEvidence:evidence,geographyEvidence:evidence,
  targetMarketShareBasisPoints:8000,priceEvidence:evidence,
  taxEvidence:evidence,creatorAgreement:{status:"ACCEPTED",agreementRef:"SYNTHETIC-CREATOR-AGREEMENT"},
  ...o});
const offers=[
  offer({offerId:"SYNTHETIC-PREMIUM-60S",creatorId:"SYNTHETIC-CREATOR-A",
    scope:"60-second host read",costs:{creatorCompensationMinor:65000,alphaFeeMinor:30000,
      paymentProcessingMinor:4000,fulfillmentMinor:4000,taxMinor:0}}),
  offer({offerId:"SYNTHETIC-ENTRY-15S",creatorId:"SYNTHETIC-CREATOR-B",
    scope:"15-second intro",costs:{creatorCompensationMinor:15000,alphaFeeMinor:15000,
      paymentProcessingMinor:1000,fulfillmentMinor:2000,taxMinor:0}}),
  offer({offerId:"SYNTHETIC-UNVERIFIED",creatorId:"SYNTHETIC-CREATOR-C",
    audienceEvidence:{...evidence,status:"SELF_REPORTED"},
    costs:{creatorCompensationMinor:1,alphaFeeMinor:1,paymentProcessingMinor:0,fulfillmentMinor:0,taxMinor:0}})
];
assert.doesNotThrow(()=>evaluateVerifiedInventory({...brief,audienceRequirements:null},offers,policy,{now}));
let crmPayload,opportunityPayload;
const result=await runSyntheticBudgetWorkflow({brief,policy,offers,now,
  crm:{upsertContact:async x=>(crmPayload=x,{id:"SYNTHETIC-CRM-CONTACT-01"})},
  marketplace:{createPendingOpportunity:async x=>(opportunityPayload=x,
    {id:"SYNTHETIC-MARKETPLACE-OPPORTUNITY-01",status:x.status})}});
assert.equal(result.fixture,"SYNTHETIC_ONLY");
assert.equal(crmPayload.consent,true);
assert.equal(crmPayload.budgetMinor,90000);
assert.equal(opportunityPayload.crmContactId,"SYNTHETIC-CRM-CONTACT-01");
assert.equal(result.opportunityStatus,"SYNTHETIC_PENDING_REVIEW");
assert.equal(result.evaluation.result,"LOWER_COST_ALTERNATIVE");
assert.equal(result.evaluation.preferred.offerId,"SYNTHETIC-ENTRY-15S");
assert.equal(result.evaluation.overBudget[0].offerId,"SYNTHETIC-PREMIUM-60S");
assert.equal(result.evaluation.overBudget[0].quote.costs.creatorCompensationMinor,65000);
assert.equal(result.evaluation.preferred.quote.costs.creatorCompensationMinor,15000);
assert.equal(result.evaluation.preferred.quote.costs.alphaFeeMinor,15000);
assert.ok(result.evaluation.preferred.quote.costs.contributionMinor>=policy.minimumContributionMinor);
assert.equal(result.evaluation.rejectedCount,1);
const weakGeo=evaluateVerifiedInventory(brief,[offer({offerId:"SYNTHETIC-WEAK-GEO",targetMarketShareBasisPoints:2000})],policy,{now});
assert.equal(weakGeo.result,"NO_SUSTAINABLE_MATCH");
assert.throws(()=>evaluateVerifiedInventory({...brief,contactConsent:false},offers,policy,{now}),/INVALID_OR_UNCONSENTED/);
const noPolicy=await evaluateVerifiedInventory(brief,[offer({offerId:"SYNTHETIC-NO-POLICY"})],null,{now});
assert.equal(noPolicy.result,"NO_SUSTAINABLE_MATCH");
console.log("Synthetic acceptance: consented intake -> CRM contact -> pending marketplace opportunity -> verified inventory -> sustainable quote -> lower-cost alternative; no provider calls.");
