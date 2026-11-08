// Budget-first marketplace decision engine. All values are integer minor units.
// This module is server-side only; the public guide must never receive raw supply data.
export const MARKET_CURRENCIES = Object.freeze({
  "SANTO_DOMINGO":"DOP", "PANAMA_CITY":"PAB", "MEDELLIN":"COP",
  "MANILA":"PHP", "KUALA_LUMPUR":"MYR", "JAKARTA":"IDR"
});
const media = new Set(["podcast","digital_creator","radio","television","newspaper","magazine","other"]);
const currencyExponent = Object.freeze({DOP:2,PAB:2,COP:2,PHP:2,MYR:2,IDR:0});

export function parseBudgetToMinor(amount, currency) {
  const code=String(currency||"").toUpperCase();
  const exponent=currencyExponent[code];
  if (exponent===undefined || typeof amount!=="string" ||
      !/^(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$/.test(amount.trim()))
    throw new Error("INVALID_BUDGET_OR_CURRENCY");
  const parts=amount.trim().split(".");
  if ((parts[1]||"").length>exponent) throw new Error("INVALID_CURRENCY_PRECISION");
  const factor=10**exponent;
  const whole=Number(parts[0]);
  const fraction=exponent ? Number((parts[1]||"").padEnd(exponent,"0")) : 0;
  const minor=whole*factor+fraction;
  if (!Number.isSafeInteger(minor) || minor<=0) throw new Error("INVALID_BUDGET_OR_CURRENCY");
  return minor;
}
const validText = (v,max) => typeof v === "string" && v.trim().length > 0 && v.trim().length <= max;
const whole = v => Number.isSafeInteger(v) && v >= 0;
const overlap = (a,b) => Array.isArray(a) && Array.isArray(b) &&
  a.some(x => b.map(y => String(y).toLowerCase()).includes(String(x).toLowerCase()));
const nowMs = value => new Date(value).getTime();

export function validateAdvertiserBrief(brief) {
  const formats = brief?.preferredMediaFormats;
  if (!brief || brief.kind !== "advertiser" ||
      !Number.isSafeInteger(brief.budgetMinor) || brief.budgetMinor <= 0 ||
      !validText(brief.currency,3) || !validText(brief.marketCode,80) ||
      (MARKET_CURRENCIES[brief.marketCode.trim().toUpperCase()] &&
        MARKET_CURRENCIES[brief.marketCode.trim().toUpperCase()] !== brief.currency.toUpperCase()) ||
      !validText(brief.objective,80) || !Array.isArray(formats) || !formats.length ||
      formats.some(x => !media.has(x)) ||
      !Array.isArray(brief.languages) || !brief.languages.length ||
      brief.languages.some(x => !validText(x,80)) ||
      !(brief.audienceRequirements == null || (typeof brief.audienceRequirements === "string" && brief.audienceRequirements.length <= 500)) ||
      brief.contactConsent !== true)
    throw new Error("INVALID_OR_UNCONSENTED_BUDGET_BRIEF");
  return Object.freeze({
    ...brief, currency:brief.currency.toUpperCase(),
    marketCode:brief.marketCode.trim().toUpperCase(),
    preferredMediaFormats:[...new Set(formats)],
    languages:[...new Set(brief.languages.map(x=>x.trim()))]
  });
}

function evidenceIsCurrent(evidence, now) {
  if (!evidence || evidence.status !== "VERIFIED" ||
      !validText(evidence.sourceRef,200) ||
      !/^[a-f0-9]{64}$/i.test(evidence.sha256 || "") ||
      !evidence.observedAt || !evidence.expiresAt) return false;
  const observed=nowMs(evidence.observedAt), expires=nowMs(evidence.expiresAt);
  return Number.isFinite(observed) && Number.isFinite(expires) && observed<=now && expires>=now;
}

export function calculatePlanningServiceQuote(offer, policy, now = new Date()) {
  if (!policy || !validText(policy.policyId,80) ||
      !whole(policy.minimumContributionMinor) ||
      !Number.isSafeInteger(policy.minimumContributionBasisPoints) ||
      policy.minimumContributionBasisPoints < 0 || policy.minimumContributionBasisPoints > 10000)
    return {eligible:false,reason:"MARGIN_POLICY_MISSING",status:"DRAFT"};
  if (!offer || offer.kind !== "PLANNING_ONLY")
    return {eligible:false,reason:"INVALID_SERVICE_QUOTE_TYPE",status:"DRAFT"};

  const market=String(offer.marketCode||"").trim().toUpperCase();
  const currency=String(offer.currency||"").trim().toUpperCase();
  if (!MARKET_CURRENCIES[market] || MARKET_CURRENCIES[market] !== currency ||
      currencyExponent[currency] === undefined)
    return {eligible:false,reason:"UNSUPPORTED_MARKET_OR_CURRENCY",status:"DRAFT"};
  if (!validText(offer.customerRequirements,2000) ||
      offer.requirementsVerified !== true ||
      !evidenceIsCurrent(offer.customerRequirementsEvidence,now))
    return {eligible:false,reason:"CUSTOMER_REQUIREMENTS_UNVERIFIED",status:"DRAFT"};
  if (!validText(offer.scope,1200) || offer.scopeConfirmed !== true ||
      !evidenceIsCurrent(offer.scopeEvidence,now))
    return {eligible:false,reason:"SERVICE_SCOPE_UNVERIFIED",status:"DRAFT"};

  const capacity=offer.deliveryCapacity;
  const start=Date.parse(capacity?.windowStart||"");
  const end=Date.parse(capacity?.windowEnd||"");
  const nowTime=now.getTime();
  if (!capacity || capacity.status !== "CONFIRMED" ||
      !validText(capacity.ownerRef,120) ||
      !Number.isFinite(start) || !Number.isFinite(end) ||
      start < nowTime || end < start ||
      !evidenceIsCurrent(offer.capacityEvidence,now))
    return {eligible:false,reason:"DELIVERY_CAPACITY_UNVERIFIED",status:"DRAFT"};
  if (!offer.terms || offer.terms.status !== "CONFIRMED" ||
      !validText(offer.terms.reference,160) ||
      !evidenceIsCurrent(offer.termsEvidence,now))
    return {eligible:false,reason:"SERVICE_TERMS_UNVERIFIED",status:"DRAFT"};

  const c=offer.costs;
  if (!c || (c.creatorCompensationMinor !== undefined && c.creatorCompensationMinor !== 0) ||
      !whole(c.alphaFeeMinor) || c.alphaFeeMinor <= 0 ||
      !whole(c.laborMinor) || !whole(c.deliveryMinor) ||
      !whole(c.paymentProcessingMinor) || !whole(c.taxMinor) ||
      !whole(offer.budgetMinor) || offer.budgetMinor <= 0 ||
      offer.budgetCurrency?.toUpperCase() !== currency)
    return {eligible:false,reason:"INCOMPLETE_SERVICE_COST_BREAKDOWN",status:"DRAFT"};
  if (!evidenceIsCurrent(offer.laborCostEvidence,now) ||
      !evidenceIsCurrent(offer.deliveryCostEvidence,now) ||
      !evidenceIsCurrent(offer.processingCostEvidence,now) ||
      !evidenceIsCurrent(offer.taxEvidence,now))
    return {eligible:false,reason:"SERVICE_COST_EVIDENCE_UNVERIFIED",status:"DRAFT"};

  // Values come from quote-specific evidence; no rate, cost, tax, or fee is defaulted.
  const fulfillmentMinor=c.laborMinor+c.deliveryMinor;
  const totalAdvertiserCostMinor=c.alphaFeeMinor+fulfillmentMinor+
    c.paymentProcessingMinor+c.taxMinor;
  const contributionMinor=c.alphaFeeMinor-fulfillmentMinor-
    c.paymentProcessingMinor-c.taxMinor;
  if (!Number.isSafeInteger(totalAdvertiserCostMinor) ||
      !Number.isSafeInteger(contributionMinor))
    return {eligible:false,reason:"SERVICE_COST_OUT_OF_RANGE",status:"DRAFT"};
  const contributionBasisPoints=totalAdvertiserCostMinor===0?0:
    Math.floor(contributionMinor*10000/totalAdvertiserCostMinor);
  if (totalAdvertiserCostMinor>offer.budgetMinor)
    return {eligible:false,reason:"OVER_CUSTOMER_BUDGET",status:"DRAFT",
      currency,totalAdvertiserCostMinor,budgetMinor:offer.budgetMinor};
  const meetsMargin=contributionMinor>=0 &&
    contributionMinor>=policy.minimumContributionMinor &&
    contributionBasisPoints>=policy.minimumContributionBasisPoints;
  if (!meetsMargin)
    return {eligible:false,reason:"MINIMUM_MARGIN_NOT_MET",status:"DRAFT",
      currency,totalAdvertiserCostMinor,contributionMinor,contributionBasisPoints,
      policyId:policy.policyId};
  return {
    eligible:true,reason:"SUSTAINABLE_PLANNING_SERVICE",status:"DRAFT",
    serviceType:"PLANNING_ONLY",currency,marketCode:market,
    scope:offer.scope,deliveryOwnerRef:capacity.ownerRef,
    deliveryWindow:{start:capacity.windowStart,end:capacity.windowEnd},
    costs:{
      creatorCompensationMinor:0,alphaFeeMinor:c.alphaFeeMinor,
      laborMinor:c.laborMinor,deliveryMinor:c.deliveryMinor,fulfillmentMinor,
      paymentProcessingMinor:c.paymentProcessingMinor,taxMinor:c.taxMinor,
      totalAdvertiserCostMinor,contributionMinor,contributionBasisPoints
    },
    budgetMinor:offer.budgetMinor,policyId:policy.policyId,
    evidenceRefs:{
      requirements:offer.customerRequirementsEvidence.sourceRef,
      scope:offer.scopeEvidence.sourceRef,
      capacity:offer.capacityEvidence.sourceRef,
      terms:offer.termsEvidence.sourceRef,
      labor:offer.laborCostEvidence.sourceRef,
      delivery:offer.deliveryCostEvidence.sourceRef,
      processing:offer.processingCostEvidence.sourceRef,
      tax:offer.taxEvidence.sourceRef
    }
  };
}

export function calculateSustainableQuote(offer, policy, now = new Date()) {
  if (!offer || !policy || !validText(policy.policyId,80) ||
      !whole(policy.minimumContributionMinor) ||
      !Number.isSafeInteger(policy.minimumContributionBasisPoints) ||
      policy.minimumContributionBasisPoints < 0 || policy.minimumContributionBasisPoints > 10000)
    return {eligible:false,reason:"MARGIN_POLICY_MISSING"};
  if (offer?.kind === "PLANNING_ONLY") return calculatePlanningServiceQuote(offer,policy,now);
  if (!offer.creatorAgreement || offer.creatorAgreement.status !== "ACCEPTED" ||
      !validText(offer.creatorAgreement.agreementRef,120) ||
      !evidenceIsCurrent(offer.priceEvidence,now) ||
      !evidenceIsCurrent(offer.taxEvidence,now))
    return {eligible:false,reason:"TERMS_OR_PRICE_EVIDENCE_UNVERIFIED"};
  const c=offer.costs;
  if (!c || !whole(c.creatorCompensationMinor) || !whole(c.alphaFeeMinor) ||
      !whole(c.paymentProcessingMinor) || !whole(c.fulfillmentMinor) || !whole(c.taxMinor))
    return {eligible:false,reason:"INCOMPLETE_COST_BREAKDOWN"};
  const totalAdvertiserCostMinor =
    c.creatorCompensationMinor+c.alphaFeeMinor+c.paymentProcessingMinor+c.fulfillmentMinor+c.taxMinor;
  const contributionMinor =
    c.alphaFeeMinor-c.paymentProcessingMinor-c.fulfillmentMinor-c.taxMinor;
  const contributionBasisPoints = totalAdvertiserCostMinor === 0 ? 0
    : Math.floor(contributionMinor*10000/totalAdvertiserCostMinor);
  const meetsMargin=contributionMinor>=0 &&
    contributionMinor>=policy.minimumContributionMinor &&
    contributionBasisPoints>=policy.minimumContributionBasisPoints;
  return {
    eligible:meetsMargin,
    reason:meetsMargin?"SUSTAINABLE":"MINIMUM_MARGIN_NOT_MET",
    currency:offer.currency,
    costs:{...c,totalAdvertiserCostMinor,contributionMinor,contributionBasisPoints},
    policyId:policy.policyId
  };
}

export function evaluateVerifiedInventory(briefInput, offers, policy, options={}) {
  const brief=validateAdvertiserBrief(briefInput);
  const now=options.now ? new Date(options.now) : new Date();
  const ready=[], overBudget=[], rejected=[];
  for(const offer of offers||[]) {
    if (!offer || offer.status!=="AVAILABLE" || offer.verifiedInventory!==true ||
        offer.marketCode?.toUpperCase()!==brief.marketCode ||
        offer.currency?.toUpperCase()!==brief.currency ||
        !media.has(offer.mediaFormat) || !brief.preferredMediaFormats.includes(offer.mediaFormat) ||
        !overlap(offer.languages,brief.languages) ||
        !Number.isSafeInteger(offer.verifiedAudience) || offer.verifiedAudience<=0 ||
        !evidenceIsCurrent(offer.audienceEvidence,now) ||
        !evidenceIsCurrent(offer.availabilityEvidence,now) ||
        !validText(offer.scope,160) || !validText(offer.offerId,120) ||
        !Number.isSafeInteger(offer.targetMarketShareBasisPoints) ||
        offer.targetMarketShareBasisPoints < (brief.minimumTargetMarketShareBasisPoints || 0) ||
        offer.targetMarketShareBasisPoints > 10000 ||
        !evidenceIsCurrent(offer.geographyEvidence,now)) {
      rejected.push({offerId:offer?.offerId||null,reason:"INVENTORY_EVIDENCE_OR_FIT_FAILED"});
      continue;
    }
    const quote=calculateSustainableQuote(offer,policy,now);
    if(!quote.eligible) {
      rejected.push({offerId:offer.offerId,reason:quote.reason});
      continue;
    }
    const result={offerId:offer.offerId,creatorId:offer.creatorId,scope:offer.scope,
      mediaFormat:offer.mediaFormat,marketCode:offer.marketCode,languages:offer.languages,
      verifiedAudience:offer.verifiedAudience,targetMarketShareBasisPoints:offer.targetMarketShareBasisPoints,quote};
    if(quote.costs.totalAdvertiserCostMinor<=brief.budgetMinor) ready.push(result);
    else overBudget.push(result);
  }
  ready.sort((a,b)=>a.quote.costs.totalAdvertiserCostMinor-b.quote.costs.totalAdvertiserCostMinor);
  overBudget.sort((a,b)=>a.quote.costs.totalAdvertiserCostMinor-b.quote.costs.totalAdvertiserCostMinor);
  const preferred=ready.find(x=>brief.preferredMediaFormats.includes(x.mediaFormat))||null;
  const alternatives=ready.filter(x=>x.offerId!==preferred?.offerId).slice(0,3);
  return {
    result:preferred&&overBudget.length?"LOWER_COST_ALTERNATIVE":preferred?"MATCH_FOUND":ready.length?"LOWER_COST_ALTERNATIVE":"NO_SUSTAINABLE_MATCH",
    preferred, alternatives, overBudget:overBudget.slice(0,3), rejectedCount:rejected.length,
    message:preferred&&overBudget.length?"Preferred placement is over budget; these separately agreed scopes fit."
      :preferred?"Verified option fits the all-in budget."
      :ready.length?"These verified lower-cost options fit the all-in budget."
      :"No verified, sustainable option currently fits this budget."
  };
}

// Synthetic acceptance orchestrator. Real systems inject adapters; this test
// harness uses synthetic CRM/opportunity fixtures and never calls a provider.
export async function runSyntheticBudgetWorkflow({brief,crm,marketplace,offers,policy,now}) {
  const valid=validateAdvertiserBrief(brief);
  const crmRecord=await crm.upsertContact({
    consent:true, name:valid.contactName, email:valid.contactEmail,
    budgetMinor:valid.budgetMinor,currency:valid.currency,marketCode:valid.marketCode,
    objective:valid.objective,mediaFormats:valid.preferredMediaFormats,
    audienceRequirements:valid.audienceRequirements,languages:valid.languages
  });
  if(!crmRecord?.id) throw new Error("CRM_FIXTURE_DID_NOT_RETURN_ID");
  const opportunity=await marketplace.createPendingOpportunity({
    intakeId:valid.intakeId,crmContactId:crmRecord.id,budgetMinor:valid.budgetMinor,
    currency:valid.currency,marketCode:valid.marketCode,status:"SYNTHETIC_PENDING_REVIEW"
  });
  if(!opportunity?.id) throw new Error("OPPORTUNITY_FIXTURE_DID_NOT_RETURN_ID");
  const evaluation=evaluateVerifiedInventory(valid,offers,policy,{now});
  return {fixture:"SYNTHETIC_ONLY",intakeId:valid.intakeId,crmRecordId:crmRecord.id,
    opportunityId:opportunity.id,opportunityStatus:opportunity.status,evaluation};
}
