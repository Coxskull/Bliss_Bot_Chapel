// Actual registration browser-script runtime: fictional data only, no network/IdP.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const source = readFileSync("frontend/public/advertiser-registration.js", "utf8");
const storageKey = "alpha.advertiser-registration.request-key.v1";
function uuid(i) { return "00000000-0000-4000-8000-" + String(i).padStart(12, "0"); }

async function scenario(mode, { enabled = true, authenticated = true } = {}) {
  const saved = new Map();
  const outputs = { signup: { textContent: "" }, registration: { textContent: "" } };
  const signupButton = { disabled: true }, registrationButton = { disabled: true };
  let submitRegistration, requestCount = 0, resetCount = 0, generated = 0;
  const submitted = [];
  const registration = {
    elements: {
      businessName: { value: "Fictional Studio" },
      countryCode: { value: "PH" },
      website: { value: "" }
    },
    querySelector(name) {
      assert.equal(name, "button");
      return registrationButton;
    },
    addEventListener(type, fn) {
      if (type === "submit") submitRegistration = fn;
    },
    reset() { resetCount++; }
  };
  const signup = {
    querySelector(name) {
      assert.equal(name, "button");
      return signupButton;
    },
    addEventListener() {},
    elements: {}
  };
  const elements = {
    "signup-form": signup,
    "registration-form": registration,
    "signup-result": outputs.signup,
    "registration-result": outputs.registration,
    "signin-link": { hidden: true },
    "privacy-consent": { checked: true },
    "terms-consent": { checked: true },
    "marketing-consent": { checked: false }
  };
  const fetch = async (url, opts = {}) => {
    if (url === "/api/auth/supabase-config")
      return { ok: true, async json() { return { advertiserRegistrationEnabled: enabled }; } };
    if (url === "/api/auth/session")
      return { ok: true, async json() {
        return { isAuthenticated: authenticated, csrfToken: authenticated ? "fixture-csrf" : null };
      } };
    assert.equal(url, "/api/advertisers/register", "unexpected external request");
    requestCount++;
    assert.equal(opts.credentials, "same-origin");
    assert.equal(opts.headers["X-CSRF-TOKEN"], "fixture-csrf");
    const payload = JSON.parse(opts.body);
    assert.equal(payload.processingConsent, true);
    assert.equal(payload.termsAccepted, true);
    assert.equal(payload.marketingOptIn, false);
    submitted.push(payload.requestKey);
    if (mode === "network-error") throw Error("synthetic interrupted transport");
    const valid = {
      status: "PENDING_REVIEW", receiptId: uuid(100),
      advertiserId: uuid(101), marketingOptIn: false
    };
    const responses = {
      accepted: { status: 201, body: valid },
      replay: { status: 200, body: valid },
      missing: { status: 201, body: {} },
      mismatch: { status: 201, body: { ...valid, marketingOptIn: true } },
      unexpected: { status: 202, body: valid },
      conflict: { status: 409, body: {} }
    };
    const result = responses[mode];
    assert.ok(result, "unrecognized fixture");
    return {
      ok: result.status < 400,
      status: result.status,
      async json() { return result.body; }
    };
  };
  runInNewContext(source, {
    document: { getElementById(id) {
      assert.ok(Object.hasOwn(elements, id), "unexpected DOM element");
      return elements[id];
    } },
    fetch,
    crypto: { randomUUID: () => uuid(++generated) },
    sessionStorage: {
      getItem(name) { return saved.get(name) ?? null; },
      setItem(name, value) { saved.set(name, value); }
    }
  }, { filename: "advertiser-registration.js" });
  await new Promise(resolve => setImmediate(resolve));
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(typeof submitRegistration, "function");
  return {
    outputs, registrationButton, submitted,
    initialKey: saved.get(storageKey),
    get storedKey() { return saved.get(storageKey); },
    get resetCount() { return resetCount; },
    get requestCount() { return requestCount; },
    async send() { await submitRegistration({ preventDefault() {} }); }
  };
}

for (const mode of ["accepted", "replay"]) {
  const client = await scenario(mode);
  assert.equal(client.registrationButton.disabled, false);
  await client.send();
  assert.equal(client.requestCount, 1, mode);
  assert.equal(client.resetCount, 1, mode);
  assert.notEqual(client.storedKey, client.initialKey, mode + ": rotate only after validated receipt");
  assert.equal(client.submitted[0], client.initialKey);
  assert.match(client.outputs.registration.textContent, /Receipt: 00000000/);
  assert.match(client.outputs.registration.textContent, /PENDING_REVIEW/);
}

for (const mode of ["missing", "mismatch", "unexpected", "network-error", "conflict"]) {
  const client = await scenario(mode);
  await client.send();
  assert.equal(client.requestCount, 1, mode);
  assert.equal(client.resetCount, 0, mode + ": form must not be cleared");
  assert.equal(client.storedKey, client.initialKey, mode + ": preserve idempotency key");
  assert.doesNotMatch(client.outputs.registration.textContent, /Registration received/);
}
for (const mode of [{ enabled: false }, { authenticated: false }]) {
  const client = await scenario("accepted", mode);
  assert.equal(client.registrationButton.disabled, true);
  await client.send();
  assert.equal(client.requestCount, 0, "disabled or unauthenticated accounts cannot POST");
}
console.log("TASK3B_REGISTRATION_VALID_RECEIPT_AND_IDEMPOTENCY_PASS");
console.log("TASK3B_REGISTRATION_MALFORMED_RECEIPT_AND_AUTH_DENIED_PASS");
