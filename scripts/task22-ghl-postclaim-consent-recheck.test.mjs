import assert from "node:assert/strict";
import test from "node:test";
import { deliverToGhl, isApprovedGhlInquiry } from "../supabase/functions/alpha-public-intake/ghl-sync.mjs";
import { ghlNotice } from "../supabase/functions/alpha-public-intake/ghl-payload.mjs";

// Task #22 negative acceptance: *all* contacts and approvals here are fictional.
// No real Supabase, HighLevel, provider credentials, network, or data mutations.
const locationId = "SyntheticLocation123";
const approved = () => ({
  kind: "creator", notice_version: ghlNotice,
  processing_acknowledged: true, adult_confirmed: true,
  contact_consent: true, contact_consent_version: ghlNotice,
  contact_consent_at: "2026-11-09T00:00:00Z",
  followup_gate: "CLEARED", followup_approved_at: "2026-11-09T00:00:00Z",
  business_identity_verified: true, decision_authority_verified: true,
  crm_suppression_checked: true, full_name: "Synthetic Creator",
  email: "creator@unit.test", website: "https://example.org/fictional",
  organization: "", marketing_opt_in: false
});
const config = {
  ALPHA_GHL_SYNC_ENABLED: "true",
  ALPHA_GHL_LOCATION_ID: locationId,
  ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN: "SYNTHETIC-TEST-TOKEN"
};
function fixture(initial, afterClaim) {
  let reads = 0, claims = 0;
  const finishes = [];
  const client = {
    from(name) {
      assert.equal(name, "alpha_public_intake_requests");
      return { select: () => ({eq: () => ({single: async () => {
        reads++;
        return {data: reads === 1 ? initial : afterClaim, error: null};
      }})})};
    },
    async rpc(name, args) {
      if (name === "alpha_ghl_claim") {
        claims++;
        return {data:[{claim_token:"synthetic-claim-1"}],error:null};
      }
      if (name === "alpha_ghl_finish") {
        finishes.push(args);
        return {data:true,error:null};
      }
      throw Error("Unexpected database RPC");
    }
  };
  return {client, finishes, get reads() { return reads; }, get claims() { return claims; }};
}
async function withOfflineGhl(fn) {
  const previousDeno = globalThis.Deno;
  const previousFetch = globalThis.fetch;
  globalThis.Deno = {env:{get:key=>config[key]}};
  try { await fn(); }
  finally { globalThis.Deno = previousDeno; globalThis.fetch = previousFetch; }
}

test("Task22: revoked consent or operator clearance after claim prevents CRM upsert", async () => {
  await withOfflineGhl(async () => {
    let externalPosts = 0;
    globalThis.fetch = async () => { externalPosts++; throw Error("CRM upsert must not be called"); };
    const revoked = [
      ["consent withdrawn", {contact_consent:false}],
      ["consent notice downgraded", {contact_consent_version:"OLD_NOTICE"}],
      ["new notice not acknowledged", {notice_version:"OLD_NOTICE"}],
      ["consent timestamp removed", {contact_consent_at:null}],
      ["operator approval revoked", {followup_gate:"HOLD"}],
      ["operator approval timestamp removed", {followup_approved_at:null}],
      ["identity no longer verified", {business_identity_verified:false}],
      ["authority no longer verified", {decision_authority_verified:false}],
      ["suppression no longer clear", {crm_suppression_checked:false}],
      ["processing acknowledgment withdrawn", {processing_acknowledged:false}],
      ["adult confirmation withdrawn", {adult_confirmed:false}],
      ["request kind changed", {kind:"privacy"}]
    ];
    for (const [label, patch] of revoked) {
      const start = approved();
      const changed = {...start, ...patch};
      assert.equal(isApprovedGhlInquiry(start), true, "initial eligibility: " + label);
      assert.equal(isApprovedGhlInquiry(changed), false, "subsequent revocation: " + label);
      const run = fixture(start, changed);
      assert.equal(await deliverToGhl(run.client,"synthetic-id"), "BLOCKED", label);
      assert.equal(run.reads,2,label);
      assert.equal(run.claims,1,label);
      assert.equal(run.finishes.length,1,label);
      assert.equal(run.finishes[0].p_new_state,"BLOCKED",label);
      assert.equal(run.finishes[0].p_error_class,"invalid_record",label);
      assert.equal(externalPosts,0,"must not send to HighLevel: " + label);
    }
  });
});

test("Task22: invalid preflight still HOLDs without claiming or sending", async () => {
  await withOfflineGhl(async () => {
    let externalPosts = 0;
    globalThis.fetch = async () => { externalPosts++; throw Error("Forbidden external call"); };
    const bad = {...approved(),crm_suppression_checked:false};
    const run = fixture(bad,bad);
    assert.equal(await deliverToGhl(run.client,"synthetic-id"),"HOLD");
    assert.equal(run.claims,0);
    assert.equal(run.reads,1);
    assert.equal(run.finishes.length,0);
    assert.equal(externalPosts,0);
  });
});

test("Task22: current approval still delivers once through an offline fake provider", async () => {
  await withOfflineGhl(async () => {
    let externalPosts = 0;
    globalThis.fetch = async (url, options) => {
      externalPosts++;
      assert.equal(url,"https://services.leadconnectorhq.com/contacts/upsert");
      assert.equal(options.method,"POST");
      const mapped = JSON.parse(options.body);
      assert.equal(mapped.email,"creator@unit.test");
      assert.equal(mapped.createNewIfDuplicateAllowed,false);
      assert.equal(Object.hasOwn(mapped,"marketing_opt_in"),false);
      assert.equal(Object.hasOwn(mapped,"tags"),false);
      assert.equal(Object.hasOwn(mapped,"dnd"),false);
      return {ok:true,status:200,json:async()=>({contact:{id:"synthetic-contact-123"}})};
    };
    const a = approved();
    const run = fixture(a,a);
    assert.equal(await deliverToGhl(run.client,"synthetic-id"),"DELIVERED");
    assert.equal(run.reads,2);
    assert.equal(run.claims,1);
    assert.equal(run.finishes.length,1);
    assert.equal(run.finishes[0].p_new_state,"DELIVERED");
    assert.equal(externalPosts,1);
  });
});
