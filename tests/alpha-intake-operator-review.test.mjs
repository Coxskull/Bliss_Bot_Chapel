import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { authorizedReview, buildReviewQueue, NOTICE, ACTIVE_NOTICE } from "../supabase/functions/alpha-intake-operator-review/review-core.mjs";

const id = "11111111-1111-4111-8111-111111111111";
const receiptId = "22222222-2222-4222-8222-222222222222";
const sessionId = "33333333-3333-4333-8333-333333333333";
const userId = "44444444-4444-4444-8444-444444444444";
const req = (overrides = {}) => ({
  id, kind: "creator", created_at: "2026-11-06T02:00:00Z",
  processing_acknowledged: true, adult_confirmed: true,
  marketing_opt_in: false, notice_version: NOTICE,
  intake_status: "NOT_YET_VERIFIED",
  // Sensitive real fields would not be returned even if supplied.
  full_name: "PRIVATE TEST CREATOR", email: "private@example.test",
  description: "PRIVATE TEST MESSAGE",
  ...overrides
});
const rec = (overrides = {}) => ({
  id: receiptId, request_id: id, kind: "creator",
  notice_version: NOTICE, recorded_at: "2026-11-06T02:01:00Z", ...overrides
});
const del = (overrides = {}) => ({ request_id: id, state: "DELIVERED", ...overrides });
const queue = (requests = [req()], receipts = [rec()], deliveries = [del()]) =>
  buildReviewQueue({ requests, receipts, deliveries });

test("receipt-linked creator inquiry enters manual review but NEVER account or campaign approval", () => {
  const result = queue();
  assert.equal(result.source, "BLISS_PUBLIC_INTEREST_ONLY");
  assert.equal(result.readonly, true);
  assert.equal(result.count, 1);
  assert.equal(result.review_ready, 1);
  assert.deepEqual(Object.keys(result.items[0]).sort(), [
    "campaign_authorized", "crm_delivery_state", "invitation_authorized", "kind",
    "manual_identity_and_rights_review_required", "payment_authorized",
    "receipt_id", "received_at", "request_id", "review_state"
  ].sort());
  assert.equal(result.items[0].review_state, "REVIEW_READY");
  assert.equal(result.items[0].receipt_id, receiptId);
  assert.equal(result.items[0].invitation_authorized, false);
  assert.equal(result.items[0].campaign_authorized, false);
  assert.equal(result.items[0].payment_authorized, false);
  assert.equal(result.items[0].manual_identity_and_rights_review_required, true);
  assert.equal(JSON.stringify(result).includes("PRIVATE TEST"), false);
  assert.equal(JSON.stringify(result).includes("private@example.test"), false);
});

test("separate marketing opt-in does not grant eligibility or change manual review state", () => {
  assert.equal(queue([req({ marketing_opt_in: true })]).items[0].review_state, "REVIEW_READY");
  assert.equal(queue([req({ marketing_opt_in: false })]).items[0].invitation_authorized, false);
});
test("consent/age/notice mismatch always holds for reviewer", () => {
  for (const change of [
    { processing_acknowledged: false },
    { adult_confirmed: false },
    { notice_version: "LEGACY-NOTICE" }
  ]) assert.equal(queue([req(change)]).items[0].review_state, "HOLD_CONSENT");
});
test("approved live notice versions permit human review only when receipt matches", () => {
  for (const notice of [
    "AGM-INTEREST-2026-11-05",
    NOTICE,
    "AGM-INTEREST-2026-11-08-GHL-01",
    ACTIVE_NOTICE
  ]) {
    const result = queue([req({ notice_version: notice })], [rec({ notice_version: notice })]);
    assert.equal(result.review_ready, 1);
    assert.equal(result.items[0].review_state, "REVIEW_READY");
    assert.equal(result.items[0].invitation_authorized, false);
    assert.equal(result.items[0].campaign_authorized, false);
    assert.equal(result.items[0].payment_authorized, false);
  }
});
test("current public form notice and pure review contract remain synchronized", () => {
  const formScript = readFileSync(new URL("../frontend/public/alpha-intake.js", import.meta.url), "utf8");
  assert.ok(formScript.includes(`const noticeVersion = "${ACTIVE_NOTICE}";`));
});
test("new notice remains held without consent, age confirmation or exact matching receipt", () => {
  const current = req({ notice_version: ACTIVE_NOTICE, contact_consent: false });
  assert.equal(queue([current],[rec({ notice_version: ACTIVE_NOTICE })]).review_ready, 1);
  // Review-readiness is separate from follow-up consent; no output can approve contact.
  for (const invalid of [
    req({ ...current, processing_acknowledged: false }),
    req({ ...current, adult_confirmed: false }),
    req({ ...current, notice_version: "UNSUPPORTED-VERSION" })
  ]) {
    const result=queue([invalid],[rec({ notice_version: invalid.notice_version })]);
    assert.equal(result.review_ready, 0);
    assert.equal(result.items[0].review_state,"HOLD_CONSENT");
    assert.equal(result.items[0].invitation_authorized,false);
  }
  const mismatch=queue([current],[rec({ notice_version: NOTICE })]);
  assert.equal(mismatch.items[0].review_state,"HOLD_RECEIPT");
  assert.equal(mismatch.review_ready,0);
});

test("no receipt, duplicate receipt, wrong kind, wrong notice or old receipt is held", () => {
  const invalidCases = [
    [],
    [rec(), rec({ id: "99999999-9999-4999-8999-999999999999" })],
    [rec({ kind: "advertiser" })],
    [rec({ notice_version: "LEGACY" })],
    [rec({ recorded_at: "2026-11-06T01:59:59Z" })],
    [rec({ id: "not-uuid" })]
  ];
  for (const received of invalidCases) {
    const result = queue([req()], received);
    assert.equal(result.items[0].review_state, "HOLD_RECEIPT");
    assert.equal(result.review_ready, 0);
    assert.equal(result.items[0].receipt_id, null);
  }
});
test("missing, failed, unconfirmed, duplicate or pending CRM delivery cannot imply review readiness", () => {
  for (const delivered of [
    [], [del({ state: "PENDING" })], [del({ state: "FAILED" })],
    [del({ state: "UNKNOWN" })], [del(), del()]
  ]) assert.equal(queue([req()], [rec()], delivered).items[0].review_state, "WAIT_CRM");
});
test("actual CRM SENDING/BLOCKED states remain visible but cannot imply review readiness", () => {
  for (const state of ["SENDING", "BLOCKED"]) {
    const result = queue([req()], [rec()], [del({ state })]);
    assert.equal(result.review_ready, 0);
    assert.equal(result.items[0].crm_delivery_state, state);
    assert.equal(result.items[0].review_state, "WAIT_CRM");
    assert.equal(result.items[0].invitation_authorized, false);
  }
  // Deprecated/non-schema state is never mistaken for a confirmed CRM delivery.
  const stale = queue([req()], [rec()], [del({ state: "CLAIMED" })]);
  assert.equal(stale.items[0].crm_delivery_state, "UNCONFIRMED");
  assert.equal(stale.review_ready, 0);
});

test("another intake status cannot be silently promoted to verified", () => {
  assert.equal(queue([req({ intake_status: "VERIFIED" })]).items[0].review_state, "HOLD_STATUS");
});
test("privacy-rights requests remain isolated from commercial review", () => {
  assert.equal(queue([req({ kind: "privacy" })]).count, 0);
});
test("no duplicate source requests and no malformed/oversized page can be processed", () => {
  assert.throws(() => queue([req(), req()]), /REVIEW_REQUEST_INVALID/);
  assert.throws(() => queue([req({ id: "bad" })]), /REVIEW_REQUEST_INVALID/);
  assert.throws(() => queue(Array.from({ length: 51 }, (_, i) => req({ id: String(i) }))),
    /REVIEW_PAGE_INVALID/);
  assert.throws(() => queue([req()], [{ id: receiptId, request_id: "bad" }]), /REVIEW_SOURCE_INVALID/);
  assert.throws(() => buildReviewQueue({ requests: null, receipts: [], deliveries: [] }), /REVIEW_PAGE_INVALID/);
});
test("authorized operator requires server-verified identity, explicit allowlist, role, AAL2 and live session", () => {
  const verified = {
    userId, verifiedUserId: userId, allowlistedUserIds: [userId],
    operatorFlag: true, aal: "aal2", sessionId, liveSession: true
  };
  assert.equal(authorizedReview(verified), true);
  for (const override of [
    { verifiedUserId: id }, { allowlistedUserIds: [] },
    { operatorFlag: false }, { aal: "aal1" },
    { sessionId: "no-session" }, { liveSession: false },
    { userId: id }, { allowlistedUserIds: [id] }
  ]) assert.equal(authorizedReview({ ...verified, ...override }), false);
});
test("Edge implementation is explicitly read-only and requires fresh GoTrue, session RPC and env gates", () => {
  const entry = readFileSync(new URL("../supabase/functions/alpha-intake-operator-review/index.ts", import.meta.url), "utf8");
  assert.ok(entry.includes("Deno.serve(createReviewHandler("));
  // Owner-authorized release candidate must retain all runtime fail-closed controls.
  assert.ok(entry.includes("const PRODUCTION_INTAKE_REVIEW_RELEASE_APPROVED = true;"));
  assert.ok(entry.includes('const TRUSTED_ORIGIN = "https://alphacreativecontent.com";'));
  assert.ok(entry.includes('if (name === "ALPHA_INTAKE_REVIEW_ENABLED")'));
  assert.ok(entry.includes('Deno.env.get("ALPHA_INTAKE_REVIEW_USER_IDS") || ""'));
  assert.ok(entry.includes('PRODUCTION_INTAKE_REVIEW_RELEASE_APPROVED &&'));
  assert.ok(entry.includes('UUID.test(id) && Deno.env.get("ALPHA_INTAKE_REVIEW_ORIGINS") === TRUSTED_ORIGIN &&'));
  assert.ok(entry.includes('Deno.env.get(name) === "true" ? "true" : "false"'));
  // Execute the actual configuration wrapper with only fictional environment values.
  const source = entry.slice(entry.indexOf("const PRODUCTION_INTAKE_REVIEW_RELEASE_APPROVED"));
  const uid = "11111111-1111-4111-8111-111111111111";
  const valid = { ALPHA_INTAKE_REVIEW_ENABLED: "true", ALPHA_INTAKE_REVIEW_USER_IDS: uid, ALPHA_INTAKE_REVIEW_ORIGINS: "https://alphacreativecontent.com" };
  const configured = (env, input = source) => {
    let result;
    new Function("Deno", "createReviewHandler", "createClient", input)(
      { env: { get: name => env[name] }, serve: handler => { result = handler; } },
      opts => opts.getEnv("ALPHA_INTAKE_REVIEW_ENABLED"), () => {}
    );
    return result;
  };
  assert.equal(configured(valid), "true");
  for (const override of [
    { ALPHA_INTAKE_REVIEW_ENABLED: "false" }, { ALPHA_INTAKE_REVIEW_ENABLED: undefined },
    { ALPHA_INTAKE_REVIEW_USER_IDS: "" }, { ALPHA_INTAKE_REVIEW_USER_IDS: uid + "," + uid },
    { ALPHA_INTAKE_REVIEW_USER_IDS: "not-a-uuid" },
    { ALPHA_INTAKE_REVIEW_ORIGINS: "https://untrusted.example.test" },
    { ALPHA_INTAKE_REVIEW_ORIGINS: valid.ALPHA_INTAKE_REVIEW_ORIGINS + ",https://untrusted.example.test" }
  ]) assert.equal(configured({ ...valid, ...override }), "false");
  assert.equal(configured(valid, source.replace("APPROVED = true", "APPROVED = false")), "false");
  const code = readFileSync(new URL("../supabase/functions/alpha-intake-operator-review/http-handler.mjs", import.meta.url), "utf8");
  for (const phrase of [
    'ALPHA_INTAKE_REVIEW_ENABLED', 'ALPHA_INTAKE_REVIEW_USER_IDS',
    'ALPHA_INTAKE_REVIEW_ORIGINS', '.auth.getUser(token)',
    'alpha_intake_operator_session_active', 'app_metadata?.alpha_intake_operator',
    '.limit(51)', 'buildReviewQueue(', 'get("Authorization")'
  ]) assert.ok(code.includes(phrase), phrase);
  for (const prohibited of [".insert(", ".upsert(", ".update(", ".delete(", ".remove(", ".auth.admin.createUser("])
    assert.equal(code.includes(prohibited), false, "no write capability: " + prohibited);
  const sql = readFileSync(new URL("../docs/launch/candidates/NOT_FOR_APPLY-alpha-intake-operator-live-session.sql", import.meta.url), "utf8");
  for (const required of [
    "NOT FOR APPLY", "auth.sessions", "auth.mfa_factors", "s.aal::text = 'aal2'",
    "REVOKE ALL", "FROM PUBLIC, anon, authenticated", "TO service_role"
  ]) assert.ok(sql.includes(required), required);
});


test("owner's exact operator readiness requires scoped email and never writes or embeds PII", () => {
  const candidate = readFileSync(new URL("../scripts/task330-owner-approved-reviewer-readiness.sql", import.meta.url), "utf8");
  for (const required of [
    "current_setting('alpha.task330_operator_email', true)",
    "target.approved_email IS NOT NULL",
    "lower(btrim(u.email)) = target.approved_email",
    "confirmed_mfa_candidates = 1",
    "currently_authorized_reviewers = 0",
    "OWNER_APPROVED_EXACT_OPERATOR_PENDING_RECOVERY",
    "BACKUP_AND_ISOLATED_RESTORE_MUST_BE_VERIFIED_SEPARATELY"
  ]) assert.ok(candidate.includes(required), required);
  assert.equal(/dejesuserwin1@/i.test(candidate), false, "do not commit operator identity");
  const body = candidate.split("WITH selected_identity AS (")[1];
  for (const write of [/\bUPDATE\b/i,/\bINSERT\b/i,/\bDELETE\b/i,/\bCREATE\b/i,/\bALTER\b/i,/\bGRANT\b/i,/\bREVOKE\b/i])
    assert.equal(write.test(body), false, "read-only operator eligibility SQL");
});
