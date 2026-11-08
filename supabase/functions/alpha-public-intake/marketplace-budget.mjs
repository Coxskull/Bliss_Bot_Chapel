// Budget-first marketplace decision engine. All values are integer minor units.
// This module is server-side only; the public guide must never receive raw supply data.
export const MARKET_CURRENCIES = Object.freeze({
  "SANTO_DOMINGO":"DOP", "PANAMA_CITY":"PAB", "MEDELLIN":"COP",
  "MANILA":"PHP", "KUALA_LUMPUR":"MYR", "JAKARTA":"IDR"
});
const media = new Set(["podcast","digital_creator","radio","television","newspaper","magazine"]);
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
      !validText(brief.objective,80) || !Array.isArray(formats) || !formats.length ||
      formats.some(x => !media.has(x)) ||
      !Array.isArray(brief.languages) || !brief.languages.length ||
      brief.languages.some(x => !validText(x,80)) ||
      !validText(brief.audienceRequirements,500) ||
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

export function calculateSustainableQuote(offer, policy, now = new Date()) {
  if (!offer || !policy || !validText(policy.policyId,80) ||
      !whole(policy.minimumContributionMinor) ||
      !Number.isSafeInteger(policy.minimumContributionBasisPoints) ||
      policy.minimumContributionBasisPoints < 0 || policy.minimumContributionBasisPoints > 10000)
    return {eligible:false,reason:"MARGIN_POLICY_MISSING"};
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
        !validText(offer.scope,160) || !validText(offer.offerId,120)) {
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
      verifiedAudience:offer.verifiedAudience,quote};
    if(quote.costs.totalAdvertiserCostMinor<=brief.budgetMinor) ready.push(result);
    else overBudget.push(result);
  }
  ready.sort((a,b)=>a.quote.costs.totalAdvertiserCostMinor-b.quote.costs.totalAdvertiserCostMinor);
  overBudget.sort((a,b)=>a.quote.costs.totalAdvertiserCostMinor-b.quote.costs.totalAdvertiserCostMinor);
  const preferred=ready.find(x=>brief.preferredMediaFormats.includes(x.mediaFormat))||null;
  const alternatives=ready.filter(x=>x.offerId!==preferred?.offerId).slice(0,3);
  return {
    result:preferred?"MATCH_FOUND":ready.length?"LOWER_COST_ALTERNATIVE":"NO_SUSTAINABLE_MATCH",
    preferred, alternatives, overBudget:overBudget.slice(0,3), rejectedCount:rejected.length,
    message:preferred?"Verified option fits the all-in budget."
      :ready.length?"Preferred placement is over budget; these separately agreed scopes fit."
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
