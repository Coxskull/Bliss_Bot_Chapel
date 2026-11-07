import test from "node:test";
import assert from "node:assert/strict";
import { createReviewHandler } from "../supabase/functions/alpha-intake-operator-review/http-handler.mjs";
import { NOTICE } from "../supabase/functions/alpha-intake-operator-review/review-core.mjs";

const USER = "11111111-1111-4111-8111-111111111111";
const SESSION = "22222222-2222-4222-8222-222222222222";
const REQUEST = "33333333-3333-4333-8333-333333333333";
const RECEIPT = "44444444-4444-4444-8444-444444444444";
const ORIGIN = "https://operator.example.test";
const b64 = obj => Buffer.from(JSON.stringify(obj)).toString("base64url");
const token = (claims = {}) => [
  b64({ typ: "JWT", alg: "HS256" }),
  b64({ sub: USER, session_id: SESSION, aal: "aal2", ...claims }),
  "fixture-signature-not-valid"
].join(".");
const sample = {
  requests: [{ id: REQUEST, kind: "creator", created_at: "2026-11-06T10:00:00Z",
    processing_acknowledged: true, adult_confirmed: true,
    notice_version: NOTICE, intake_status: "NOT_YET_VERIFIED",
    email: "must-never-be-returned@example.test" }],
  receipts: [{ id: RECEIPT, request_id: REQUEST, kind: "creator",
    notice_version: NOTICE, recorded_at: "2026-11-06T10:01:00Z" }],
  deliveries: [{ request_id: REQUEST, state: "DELIVERED", ghl_contact_id: "private-ghl-id" }]
};
const defaultEnv = {
  ALPHA_INTAKE_REVIEW_ORIGINS: ORIGIN,
  ALPHA_INTAKE_REVIEW_ENABLED: "true",
  ALPHA_INTAKE_REVIEW_USER_IDS: USER,
  SUPABASE_URL: "https://fixture.supabase.test",
  SUPABASE_SERVICE_ROLE_KEY: "PRIVATE-SERVER-KEY"
};
function fixture(options = {}) {
  const env = { ...defaultEnv, ...(options.env || {}) };
  const calls = { client: 0, auth: 0, rpc: 0, tables: [], selected: [] };
  const data = { ...sample, ...(options.data || {}) };
  const createClient = (url, key, configuration) => {
    calls.client++;
    assert.equal(url, defaultEnv.SUPABASE_URL);
    assert.equal(key, defaultEnv.SUPABASE_SERVICE_ROLE_KEY);
    assert.equal(configuration.auth.persistSession, false);
    return {
      auth: { getUser: async () => {
        calls.auth++;
        return options.authError
          ? { data: null, error: new Error("token rejected") }
          : { data: { user: options.user === undefined
              ? { id: USER, app_metadata: { alpha_intake_operator: true } } : options.user
          }, error: null };
      } },
      rpc: async (method, args) => {
        calls.rpc++;
        assert.equal(method, "alpha_intake_operator_session_active");
        assert.equal(args.p_user_id, USER);
        assert.equal(args.p_session_id, SESSION);
        return {
          data: options.live === undefined ? true : options.live,
          error: options.rpcError ? new Error("rpc denied") : null
        };
      },
      from: table => {
        calls.tables.push(table);
        const key = table === "alpha_public_intake_requests" ? "requests"
          : table === "alpha_public_intake_receipts" ? "receipts"
          : table === "alpha_ghl_delivery" ? "deliveries" : null;
        assert.ok(key, "only three approved tables may be read");
        let selected = "";
        const chain = {
          select: fields => { selected = fields; calls.selected.push({ table, fields }); return chain; },
          order: () => chain,
          in: (_column, ids) => {
            assert.ok(ids.every(x => x === REQUEST));
            return chain;
          },
          limit: n => {
            assert.ok(n === 50 || n === 101);
            if (options.tableError === key)
              return Promise.resolve({ data: null, error: new Error("redacted DB error") });
            return Promise.resolve({ data: data[key], error: null });
          }
        };
        return chain;
      }
    };
  };
  const handler = createReviewHandler({ getEnv: name => env[name], createClient });
  const request = ({ method = "GET", origin = ORIGIN, authorization = "Bearer " + token(), url = "https://fixture.example.test/review" } = {}) => {
    const headers = {};
    if (origin !== null) headers.Origin = origin;
    if (authorization !== null) headers.Authorization = authorization;
    return new Request(url, { method, headers });
  };
  return { handler, request, calls, env };
}
async function call(f, options = {}) {
  const result = await f.handler(f.request(options));
  const json = result.status === 204 ? null : await result.json();
  assert.match(result.headers.get("cache-control") || "", /no-store/);
  return { result, json };
}
test("valid role, matched signed claims, AAL2 and live session yields PII-free review but no invitation", async () => {
  const f = fixture();
  const { result, json } = await call(f);
  assert.equal(result.status, 200);
  assert.equal(result.headers.get("Access-Control-Allow-Origin"), ORIGIN);
  assert.equal(json.review_ready, 1);
  assert.equal(json.items[0].receipt_id, RECEIPT);
  assert.equal(json.items[0].crm_delivery_state, "DELIVERED");
  assert.equal(json.items[0].invitation_authorized, false);
  assert.equal(json.items[0].campaign_authorized, false);
  assert.equal(json.items[0].payment_authorized, false);
  assert.equal(f.calls.auth, 1);
  assert.equal(f.calls.rpc, 1);
  assert.deepEqual(f.calls.tables, [
    "alpha_public_intake_requests", "alpha_public_intake_receipts", "alpha_ghl_delivery"
  ]);
  const serialized = JSON.stringify(json);
  for (const privateText of ["must-never-be-returned", "private-ghl-id", "PRIVATE-SERVER-KEY", "fixture-signature"])
    assert.equal(serialized.includes(privateText), false);
  for (const fields of f.calls.selected)
    assert.doesNotMatch(fields.fields, /email|full_name|payload|contact_id|website|description|notes/i);
});
test("invalid or missing Origin, disabled endpoint, wrong method or query deny before service-key use", async () => {
  for (const options of [
    { origin: null }, { origin: "https://evil.example.test" },
    { method: "POST" }, { url: "https://fixture.example.test/review?owner=other" }
  ]) {
    const f = fixture();
    const { result } = await call(f, options);
    assert.ok([403, 405].includes(result.status));
    assert.equal(f.calls.client, 0);
  }
  const f = fixture({ env: { ALPHA_INTAKE_REVIEW_ENABLED: "false" } });
  const { result } = await call(f);
  assert.equal(result.status, 503);
  assert.equal(f.calls.client, 0);
});
test("no bearer, missing configuration, or unknown JWT denies without reading customer records", async () => {
  for (const options of [
    { request: { authorization: null } },
    { request: { authorization: "Bearer a" } },
    { request: { authorization: "Bearer invalidJWT.bad" } }
  ]) {
    const f = fixture();
    const { result } = await call(f, options.request);
    assert.notEqual(result.status, 200);
    assert.equal(f.calls.tables.length, 0);
  }
  const f = fixture({ env: { ALPHA_INTAKE_REVIEW_USER_IDS: "" } });
  const { result } = await call(f);
  assert.equal(result.status, 503);
  assert.equal(f.calls.client, 0);
});
test("GoTrue denial, JWT/user mismatch, AAL1, unauthorized reviewer and revoked session cannot query leads", async () => {
  const variants = [
    { f: fixture({ authError: true }), expected: 401 },
    { f: fixture({ user: { id: USER, app_metadata: {} } }), expected: 403 },
    { f: fixture({ env: { ALPHA_INTAKE_REVIEW_USER_IDS: "55555555-5555-4555-8555-555555555555" } }), expected: 403 },
    { f: fixture({ live: false }), expected: 403 },
    { f: fixture({ rpcError: true }), expected: 403 }
  ];
  for (const { f, expected } of variants) {
    const { result } = await call(f);
    assert.equal(result.status, expected);
    assert.equal(f.calls.tables.length, 0);
  }
  for (const claims of [{ aal: "aal1" },
    { session_id: "66666666-6666-4666-8666-666666666666" },
    { sub: "77777777-7777-4777-8777-777777777777" }]) {
    const f = fixture();
    const { result } = await call(f, { authorization: "Bearer " + token(claims) });
    assert.equal(result.status, 403);
    assert.equal(f.calls.rpc, 0);
    assert.equal(f.calls.tables.length, 0);
  }
});
test("missing/duplicate receipt or delivery remains held and never grants verified account", async () => {
  for (const data of [
    { receipts: [] },
    { receipts: [...sample.receipts, ...sample.receipts] },
    { deliveries: [] },
    { deliveries: [...sample.deliveries, ...sample.deliveries] }
  ]) {
    const f = fixture({ data });
    const { result, json } = await call(f);
    assert.equal(result.status, 200);
    assert.equal(json.review_ready, 0);
    assert.equal(json.items[0].invitation_authorized, false);
  }
});
test("errors, overlong linked receipts and post-query failures return no partial customer data", async () => {
  for (const options of [
    { tableError: "requests" },
    { tableError: "receipts" },
    { tableError: "deliveries" },
    { data: { receipts: Array.from({ length: 101 }, () => sample.receipts[0]) } }
  ]) {
    const f = fixture(options);
    const { result, json } = await call(f);
    assert.equal(result.status, 503);
    assert.deepEqual(Object.keys(json), ["error"]);
    assert.equal(JSON.stringify(json).includes("must-never-be-returned"), false);
  }
});
test("OPTIONS preflight has no JSON body, returns only approved origin and needs no Auth", async () => {
  const f = fixture();
  const { result, json } = await call(f, { method: "OPTIONS", authorization: null });
  assert.equal(result.status, 204);
  assert.equal(json, null);
  assert.equal(result.headers.get("Access-Control-Allow-Origin"), ORIGIN);
  assert.equal(f.calls.client, 0);
});
