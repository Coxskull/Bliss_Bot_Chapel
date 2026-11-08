import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { BUDGET_NOTICE } from "../supabase/functions/alpha-public-intake/marketplace-intake.mjs";
import { mapGhlContact, ghlNotice } from "../supabase/functions/alpha-public-intake/ghl-payload.mjs";
import { calculatePlanningServiceQuote, calculateSustainableQuote } from "../supabase/functions/alpha-public-intake/marketplace-budget.mjs";

// TASK #7 / ALPHA-346 — offline-only security and approval-boundary regression.
// This file deliberately contains no fetch, Supabase client, live account,
// environment secrets, provider credentials, database writes, or real identities.
const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");
const now = new Date("2026-11-09T00:00:00.000Z");
const evidence = () => ({
  status: "VERIFIED",
  sourceRef: "SYNTHETIC_EVIDENCE_ONLY",
  sha256: "a".repeat(64),
  observedAt: "2026-11-08T00:00:00.000Z",
  expiresAt: "2026-12-08T00:00:00.000Z"
});
const makeOffer = () => ({
  kind: "PLANNING_ONLY", marketCode: "MANILA", currency: "PHP",
  budgetCurrency: "PHP", budgetMinor: 90000,
  customerRequirements: "Synthetic local business campaign planning.",
  requirementsVerified: true, customerRequirementsEvidence: evidence(),
  scope: "A written planning document, no ad placement.",
  scopeConfirmed: true, scopeEvidence: evidence(),
  deliveryCapacity: {
    status: "CONFIRMED", ownerRef: "SYNTHETIC_OPERATOR",
    windowStart: "2026-11-10T00:00:00.000Z",
    windowEnd: "2026-11-20T00:00:00.000Z"
  },
  capacityEvidence: evidence(),
  terms: { status: "CONFIRMED", reference: "SYNTHETIC_TERMS_V1" },
  termsEvidence: evidence(),
  laborCostEvidence: evidence(), deliveryCostEvidence: evidence(),
  processingCostEvidence: evidence(), alphaFeeEvidence: evidence(),
  taxEvidence: evidence(),
  costs: {
    creatorCompensationMinor: 0, alphaFeeMinor: 30000,
    laborMinor: 10000, deliveryMinor: 2000,
    paymentProcessingMinor: 1000, taxMinor: 1000
  }
});
const policy = () => ({
  policyId: "SYNTHETIC_POLICY", minimumContributionMinor: 10000,
  minimumContributionBasisPoints: 1500
});
const quote = (offer, p = policy()) => calculatePlanningServiceQuote(offer, p, now);

test("candidate contains newer budget-first release contract; legacy main cannot pass silently", () => {
  const requiredFiles = [
    "index.ts", "ghl-sync.mjs", "ghl-payload.mjs",
    "ask-alpha.mjs", "marketplace-intake.mjs", "marketplace-budget.mjs"
  ];
  const root = "supabase/functions/alpha-public-intake/";
  for (const filename of requiredFiles)
    assert.ok(read(root + filename).length > 150, "missing release helper " + filename);
  const edge = read(root + "index.ts");
  const browser = read("frontend/public/alpha-intake.js");
  assert.match(edge, /import \{ handleAskAlpha \}/);
  assert.match(edge, /import \{ BUDGET_NOTICE, normalizeStructuredAdvertiser \}/);
  assert.match(edge, /contact_consent/);
  assert.ok(edge.indexOf('if (!origin || !allowed.has(origin))') <
    edge.indexOf("const body = await req.text()"),
    "origin gate must run before request body and any database operation");
  assert.match(edge, /if \(req\.method !== "POST"\)/);
  assert.match(edge, /if \(body\.length > 6000\)/);
  assert.match(edge, /if \(payload\.middle_name\)/);
  assert.match(browser, /credentials: "omit"/);
  assert.doesNotMatch(browser, /SUPABASE_SERVICE_ROLE_KEY|ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN|localStorage|sessionStorage/);
  assert.doesNotMatch(edge, /auth\.admin\.createUser\(|auth\.signUp\(/);
  // Origin is a browser control, NOT proof of server-side client authentication;
  // hosted negative tests and rate-limit/abuse review remain open.
});

test("GHL mapping requires explicit, current contact consent; marketing is never inferred", () => {
  assert.equal(ghlNotice, BUDGET_NOTICE);
  const record = {
    kind: "creator", notice_version: ghlNotice,
    contact_consent: true, contact_consent_version: ghlNotice,
    processing_acknowledged: true, adult_confirmed: true,
    full_name: "Synthetic Creator", email: "creator@unit.test",
    website: "https://example.org/fixture", marketing_opt_in: false
  };
  const loc = "aBcD1234EFgh5678IJkl";
  const mapped = mapGhlContact(record, loc);
  assert.equal(mapped.createNewIfDuplicateAllowed, false);
  assert.equal(mapped.email, record.email);
  for (const prohibited of ["marketing_opt_in","tags","dnd","roles","contact_consent","password","phone"])
    assert.equal(Object.hasOwn(mapped, prohibited), false, "unexpected CRM field: " + prohibited);
  for (const change of [
    {contact_consent:false},
    {contact_consent_version:"LEGACY_NOTICE"},
    {notice_version:"LEGACY_NOTICE"},
    {processing_acknowledged:false},
    {adult_confirmed:false},
    {kind:"privacy"},
    {email:"synthetic@example.invalid"}
  ])
    assert.throws(() => mapGhlContact({...record, ...change}, loc), /Not eligible/);
});

test("advertiser CRM field mapping fails closed on incomplete or ambiguous IDs", () => {
  const record = {
    kind:"advertiser", notice_version:ghlNotice,
    contact_consent:true, contact_consent_version:ghlNotice,
    processing_acknowledged:true, adult_confirmed:true,
    email:"buyer@unit.test", full_name:"Synthetic Buyer",
    organization:"Synthetic Organization", budget_amount:900,
    budget_currency:"PHP", market_code:"MANILA",
    campaign_objective:"awareness", preferred_media_formats:["podcast"],
    audience_requirements:null, preferred_languages:["Filipino"],
    marketing_opt_in:false
  };
  const loc="aBcD1234EFgh5678IJkl";
  assert.throws(() => mapGhlContact(record,loc,{}), /mapping is incomplete/);
  const fieldNames=[
    "budget_amount","budget_currency","market_code",
    "campaign_objective","preferred_media_formats",
    "audience_requirements","preferred_languages"
  ];
  const fields = Object.fromEntries(fieldNames.map((name,i)=>[name,"SYNTHETIC_ID_"+i]));
  const mapped=mapGhlContact(record,loc,fields);
  assert.equal(mapped.customFields.length,7);
  assert.equal(mapped.createNewIfDuplicateAllowed,false);
  assert.equal(Object.hasOwn(mapped,"tags"),false);
  assert.equal(Object.hasOwn(mapped,"dnd"),false);
  assert.equal(Object.hasOwn(mapped,"marketing_opt_in"),false);
  assert.throws(()=>mapGhlContact(record,loc,{...fields,preferred_languages:fields.budget_amount}),/mapping is ambiguous/);
});

test("complete planning-service quote is a DRAFT, never an automatic charge or placement", () => {
  const x=quote(makeOffer());
  assert.equal(x.eligible,true);
  assert.equal(x.status,"DRAFT");
  assert.equal(x.serviceType,"PLANNING_ONLY");
  assert.equal(x.costs.totalAdvertiserCostMinor,44000);
  assert.equal(x.costs.contributionMinor,16000);
  for(const forbidden of ["paymentIntent","paymentLink","stripeCharge","creatorId","placementId"])
    assert.equal(Object.hasOwn(x,forbidden),false);
  const alternate=calculateSustainableQuote(makeOffer(),policy(),now);
  assert.deepEqual(alternate,x);
});

test("planning quote fails closed across all missing evidence and approval gates", () => {
  const cases=[
    ["missing policy",x=>x,()=>null,"MARGIN_POLICY_MISSING"],
    ["wrong service type",x=>{x.kind="PLACEMENT";},"","INVALID_SERVICE_QUOTE_TYPE"],
    ["unsupported currency",x=>{x.currency="USD";},"","UNSUPPORTED_MARKET_OR_CURRENCY"],
    ["creator placement binding",x=>{x.placementId="SYNTHETIC_SLOT";},"","UNSUPPORTED_MARKET_OR_CURRENCY"],
    ["no verified requirements",x=>{x.requirementsVerified=false;},"","CUSTOMER_REQUIREMENTS_UNVERIFIED"],
    ["stale customer evidence",x=>{x.customerRequirementsEvidence.expiresAt="2026-11-01T00:00:00.000Z";},"","CUSTOMER_REQUIREMENTS_UNVERIFIED"],
    ["no confirmed scope",x=>{x.scopeConfirmed=false;},"","SERVICE_SCOPE_UNVERIFIED"],
    ["no delivery capacity",x=>{delete x.deliveryCapacity;},"","DELIVERY_CAPACITY_UNVERIFIED"],
    ["expired delivery evidence",x=>{x.capacityEvidence.expiresAt="2026-11-01T00:00:00.000Z";},"","DELIVERY_CAPACITY_UNVERIFIED"],
    ["no confirmed terms",x=>{x.terms.status="DRAFT";},"","SERVICE_TERMS_UNVERIFIED"],
    ["nonzero creator compensation",x=>{x.costs.creatorCompensationMinor=1;},"","INCOMPLETE_SERVICE_COST_BREAKDOWN"],
    ["missing processing cost",x=>{delete x.costs.paymentProcessingMinor;},"","INCOMPLETE_SERVICE_COST_BREAKDOWN"],
    ["missing tax evidence",x=>{x.taxEvidence.status="SELF_REPORTED";},"","SERVICE_COST_EVIDENCE_UNVERIFIED"],
    ["over customer budget",x=>{x.budgetMinor=1000;},"","OVER_CUSTOMER_BUDGET"],
    ["below required margin",x=>x,()=>({...policy(), minimumContributionBasisPoints:5000}),"MINIMUM_MARGIN_NOT_MET"],
  ];
  for (const [name,mutation,policyOverride,reason] of cases) {
    const offer=makeOffer();
    mutation(offer);
    const p=typeof policyOverride==="function"?policyOverride():policy();
    const outcome=quote(offer,p);
    assert.equal(outcome.eligible,false,name);
    assert.equal(outcome.status,"DRAFT",name);
    assert.equal(outcome.reason,reason,name);
  }
});
