import assert from 'node:assert/strict';
import test from 'node:test';
import {parseBudgetToMinor, validateAdvertiserBrief, calculateSustainableQuote}
  from '../supabase/functions/alpha-public-intake/marketplace-budget.mjs';

const now=new Date('2026-11-08T12:00:00Z');
const evidence={status:'VERIFIED',sourceRef:'SYNTHETIC',sha256:'a'.repeat(64),
  observedAt:'2026-11-07T00:00:00Z',expiresAt:'2026-11-09T00:00:00Z'};
const policy={policyId:'SYNTHETIC',minimumContributionMinor:0,minimumContributionBasisPoints:0};
const offer={currency:'PHP',creatorAgreement:{status:'ACCEPTED',agreementRef:'SYNTHETIC'},
  priceEvidence:evidence,taxEvidence:evidence};

test('decimal budgets retain every minor unit up to the safe integer boundary',()=>{
  for (const ending of ['89','90','91']) {
    assert.equal(parseBudgetToMinor('90071992547409.'+ending,'PHP'),
      Number(BigInt('90071992547409'+ending)));
  }
  assert.throws(()=>parseBudgetToMinor('90071992547409.92','PHP'),/INVALID_BUDGET/);
});
test('brief rejects unknown and inherited market names',()=>{
  const brief={kind:'advertiser',budgetMinor:100,currency:'PHP',marketCode:'MANILA',
    objective:'awareness',preferredMediaFormats:['podcast'],languages:['English'],contactConsent:true};
  assert.doesNotThrow(()=>validateAdvertiserBrief(brief));
  for (const marketCode of ['UNKNOWN','constructor','__proto__'])
    assert.throws(()=>validateAdvertiserBrief({...brief,marketCode}),/INVALID_OR_UNCONSENTED/);
});
test('individually valid costs cannot produce an unsafe quote total',()=>{
  const result=calculateSustainableQuote({...offer,costs:{creatorCompensationMinor:Number.MAX_SAFE_INTEGER,
    alphaFeeMinor:1,paymentProcessingMinor:0,fulfillmentMinor:0,taxMinor:0}},policy,now);
  assert.equal(result.eligible,false);
  assert.equal(result.reason,'COST_OUT_OF_RANGE');
});
test('margin threshold uses exact basis points at large valid amounts',()=>{
  // The old floating point calculation rounds 110 basis points up to 111.
  const result=calculateSustainableQuote({...offer,costs:{creatorCompensationMinor:8907219343013366,
    alphaFeeMinor:99979911727625,paymentProcessingMinor:0,fulfillmentMinor:0,
    taxMinor:0}}, {...policy,minimumContributionBasisPoints:111},now);
  assert.equal(result.costs.contributionBasisPoints,110);
  assert.equal(result.eligible,false);
});
