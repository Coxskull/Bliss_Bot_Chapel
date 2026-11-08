import assert from "node:assert/strict";
import {runSyntheticBudgetWorkflow,evaluateVerifiedInventory} from "../supabase/functions/alpha-public-intake/marketplace-budget.mjs";

const now="2026-11-08T12:00:00.000Z";
const evidence={status:"VERIFIED",sourceRef:"SYNTHETIC_FIXTURE_SOURCE",
  sha256:"a".repeat(64),observedAt:"2026-11-07T12:00:00.000Z",expiresAt:"2026-12-08T12:00:00.000Z"};
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
