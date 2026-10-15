// AGM-LAUNCH-015 Stage I: disabled real-consent API acceptance in isolated CI.
// This test intentionally sends synthetic commands to demonstrate 409 denial,
// not to record legal visitor consent. Host is loopback Development only.
const assert = require("node:assert/strict");
(async () => {
  const base = process.env.BLISS_SMOKE_BASE || "http://127.0.0.1:5086";
  assert(["localhost", "127.0.0.1"].includes(new URL(base).hostname));
  const endpoint = base + "/api/ask-alpha/visitor-consent";
  const readiness = await fetch(endpoint + "/readiness");
  assert.equal(readiness.status, 200);
  const status = await readiness.json();
  assert.equal(status.visitorConsentStatus, "NOT_RECORDED");
  assert.equal(status.authenticatedVisitorConsentRecorded, false);
  assert.equal(status.transcriptExportAuthorized, false);
  assert.equal(status.delivery, "NOT_SENT");

  const commands = [
    ["/opt-in", {
      purpose: "CREATIVE_PREVIEW_ONLY",
      consentVersion: "AGM-CONSENT-PREVIEW-V1",
      explicitlySelected: true, requestKey: "fixture_stage_i_12345"
    }],
    ["/withdraw", {
      purpose: "CREATIVE_PREVIEW_ONLY",
      consentVersion: "AGM-CONSENT-PREVIEW-V1",
      explicitlySelected: false, requestKey: "fixture_withdraw_12345"
    }],
    ["/opt-in", {
      purpose: "TRANSCRIPT_EXPORT",
      consentVersion: "AGM-CONSENT-PREVIEW-V1",
      explicitlySelected: true, requestKey: "fixture_stage_i_67890"
    }]
  ];
  for(const [path,body] of commands) {
    const response = await fetch(endpoint + path, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    assert.equal(response.status,409,path + ": " + response.status);
    const result=await response.json();
    assert.equal(result.visitorConsentStatus,"NOT_RECORDED");
    assert.equal(result.authoritativeOwnershipVerified,false);
    assert.equal(result.authenticatedVisitorConsentRecorded,false);
    assert.equal(result.transcriptExportAuthorized,false);
    assert.equal(result.paymentAuthorized,false);
    assert.equal(result.creatorPayoutAuthorized,false);
    assert.equal(result.campaignDeliveryAuthorized,false);
    assert.equal(result.delivery,"NOT_SENT");
  }
  console.log("PASS: synthetic GET status and rejected opt-in/withdrawal/forged purpose HTTP 409, no consent record, transcript export, payment, payout or delivery. Development-only without OIDC; no paid AI calls.");
})().catch(error => { console.error(error); process.exitCode = 1; });
