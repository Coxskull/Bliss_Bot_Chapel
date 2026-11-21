// Actual registration browser-script runtime: fictional data only, no network/IdP.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { createHash } from "node:crypto";

const source = readFileSync("frontend/public/advertiser-registration.js", "utf8");
const storageKey = "alpha.advertiser-registration.request-key.v2";
let globalGenerated = 0;
const storedKey = storage => { try { return JSON.parse(storage.get(storageKey) || "null")?.key; } catch { return null; } };
function uuid(i) { return "00000000-0000-4000-8000-" + String(i).padStart(12, "0"); }

async function scenario(mode, { enabled = true, authenticated = true,
  email = "applicant@example.invalid", saved = new Map() } = {}) {
  const outputs = { signup: { textContent: "" }, registration: { textContent: "" } };
  const signupButton = { disabled: true }, registrationButton = { disabled: true };
  let submitRegistration, requestCount = 0, resetCount = 0, releasePending = null;
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
        return { isAuthenticated: authenticated, email: authenticated ? email : null,
          csrfToken: authenticated ? "fixture-csrf" : null };
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
    if (mode === "slow-accepted") await new Promise(resolve => { releasePending = resolve; });
    const valid = {
      status: "PENDING_REVIEW", receiptId: uuid(100),
      advertiserId: uuid(101), marketingOptIn: false,
      requestKey: payload.requestKey,
      privacyNoticeVersion: payload.privacyNoticeVersion,
      termsVersion: payload.termsVersion
    };
    const responses = {
      accepted: { status: 201, body: valid },
      "slow-accepted": { status: 201, body: valid },
      replay: { status: 200, body: valid },
      missing: { status: 201, body: {} },
      mismatch: { status: 201, body: { ...valid, marketingOptIn: true } },
      unrelated: { status: 201, body: { ...valid, requestKey: uuid(999) } },
      missingbinding: { status: 201, body: { ...valid, requestKey: undefined } },
      wrongnotice: { status: 201, body: { ...valid, privacyNoticeVersion: "unapproved-notice" } },
      wrongterms: { status: 200, body: { ...valid, termsVersion: "unapproved-terms" } },
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
    TextEncoder,
    Uint8Array,
    crypto: {
      randomUUID: () => uuid(++globalGenerated),
      subtle: { async digest(algorithm, input) {
        assert.equal(algorithm, "SHA-256");
        const bytes = createHash("sha256").update(Buffer.from(input)).digest();
        return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
      } }
    },
    sessionStorage: {
      getItem(name) { return saved.get(name) ?? null; },
      setItem(name, value) { saved.set(name, value); },
      removeItem(name) { saved.delete(name); }
    }
  }, { filename: "advertiser-registration.js" });
  await new Promise(resolve => setImmediate(resolve));
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(typeof submitRegistration, "function");
  return {
    outputs, registrationButton, submitted,
    initialKey: storedKey(saved),
    get storedKey() { return storedKey(saved); },
    get saved() { return saved; },
    get resetCount() { return resetCount; },
    get requestCount() { return requestCount; },
    release() { assert.ok(releasePending, "expected a blocked response"); releasePending(); },
    async send() { await submitRegistration({ preventDefault() {} }); }
  };
}

// Cross-login retry-key containment is only a browser UX boundary;
// authoritative user/tenant isolation remains server-side and is separately tested.
{
  const sameTab = new Map();
  sameTab.set("alpha.advertiser-registration.request-key.v1", uuid(888));
  const first = await scenario("network-error", {
    saved:sameTab, email:"alpha@example.invalid"
  });
  assert.equal(sameTab.has("alpha.advertiser-registration.request-key.v1"), false);
  assert.equal(first.storedKey,first.initialKey);
  await first.send();
  const replay = await scenario("replay", {
    saved:sameTab, email:"ALPHA@example.invalid"
  });
  assert.equal(replay.initialKey,first.initialKey,
    "same sign-in should retain the request key after interrupted transport");
  await replay.send();
  assert.notEqual(replay.storedKey,replay.initialKey,"only an authoritative receipt rotates key");
  const other = await scenario("accepted", {
    saved:sameTab, email:"beta@example.invalid"
  });
  assert.notEqual(other.initialKey,replay.storedKey,
    "different signed-in applicant must not reuse old applicant's key");
  const persisted = JSON.stringify([...sameTab.entries()]);
  assert.doesNotMatch(persisted, /alpha@example\.invalid|beta@example\.invalid/i,
    "browser stores an opaque digest, not the session email");
  const savedNewUser = other.initialKey;
  await other.send();
  assert.notEqual(other.storedKey,savedNewUser);
}
{
  const stale = new Map([[storageKey, '{"scope":"forged","key":"not-a-uuid"}']]);
  const bad = await scenario("network-error",{saved:stale});
  assert.match(bad.initialKey,/^00000000-0000-4000-8000/);
  assert.equal(bad.storedKey,bad.initialKey);
  await bad.send();
  const loggedOut = await scenario("accepted",{saved:stale,authenticated:false});
  assert.equal(loggedOut.registrationButton.disabled,true);
  assert.equal(stale.has(storageKey),false,
    "logging out must clear the previous applicant's stored retry key");
}

{
  const client = await scenario("slow-accepted");
  const first = client.send(); // response deliberately still pending
  assert.equal(client.registrationButton.disabled,true);
  const duplicate = client.send();
  assert.equal(client.requestCount,1,
    "second click while first is in flight must not issue another registration POST");
  client.release();
  await Promise.all([first,duplicate]);
  assert.equal(client.registrationButton.disabled,false);
  assert.equal(client.resetCount,1);
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

for (const mode of ["missing", "mismatch", "unrelated", "missingbinding", "wrongnotice", "wrongterms", "unexpected", "network-error", "conflict"]) {
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
console.log("TASK3B_REGISTRATION_REQUEST_BOUND_RECEIPT_AND_AUTH_DENIED_PASS");
console.log("TASK3B_REGISTRATION_TAB_KEY_CROSS_IDENTITY_CONTAINMENT_PASS");
console.log("TASK3B_REGISTRATION_DUPLICATE_INFLIGHT_POST_REFUSAL_PASS");
