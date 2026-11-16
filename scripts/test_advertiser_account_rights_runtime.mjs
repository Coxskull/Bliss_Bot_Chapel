// Disposable browser-script runtime regression: no host network or real IdP.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const source = readFileSync("frontend/public/advertiser-account-rights.js", "utf8");

async function mount({ configOk = true, sessionOk = true, historyOk = true, postMode = "accepted" } = {}) {
  let onSubmit;
  let postCount = 0;
  let historyCount = 0;
  let resetCount = 0;
  const submittedKeys = [];
  let sequence = 0;
  const stored = new Map();
  const submit = { disabled: true };
  const status = { textContent: "" };
  const history = { replaceChildren() {}, appendChild() {} };
  const form = {
    elements: { requestType: { value: "ACCESS" } },
    addEventListener(type, callback) {
      if (type === "submit") onSubmit = callback;
    },
    reportValidity() { return true; },
    reset() { resetCount++; }
  };
  function response(ok, data, code = ok ? 200 : 503) {
    return { ok, status: code, async json() { return data; } };
  }
  const fetch = async (url, options = {}) => {
    if (url === "/api/auth/supabase-config")
      return response(configOk, { advertiserPrivacyRequestsEnabled: true });
    if (url === "/api/auth/session")
      return response(sessionOk, { isAuthenticated: true, csrfToken: "fictional-csrf" });
    if (url === "/api/advertisers/privacy-requests") {
      if (options.method === "POST") {
        postCount += 1;
        assert.equal(options.credentials, "same-origin");
        assert.equal(options.headers["X-CSRF-TOKEN"], "fictional-csrf");
        const payload = JSON.parse(options.body);
        assert.equal(payload.requestType, "ACCESS");
        submittedKeys.push(payload.requestKey);
        if (postMode === "network-error") throw Error("simulated uncertain transport");
        if (postMode === "conflict") return response(false, {}, 409);
        const receipt = {
          caseId: "11111111-1111-4111-8111-111111111111",
          requestType: "ACCESS", status: "RECEIVED",
          createdAt: "2026-11-16T03:00:00Z"
        };
        if (postMode === "missing") return response(true, {}, 201);
        if (postMode === "wrong-type") return response(true, { ...receipt, requestType: "ERASURE" }, 201);
        if (postMode === "wrong-status") return response(true, { ...receipt, status: "COMPLETED" }, 201);
        if (postMode === "bad-date") return response(true, { ...receipt, createdAt: "not-a-date" }, 201);
        if (postMode === "bad-id") return response(true, { ...receipt, caseId: "unverified" }, 201);
        if (postMode === "unexpected") return response(true, receipt, 202);
        if (postMode === "replay") return response(true, { ...receipt, status: "VERIFYING" }, 200);
        assert.equal(postMode, "accepted");
        return response(true, receipt, 201);
      }
      historyCount += 1;
      return response(historyOk, []);
    }
    throw Error("unexpected browser fixture URL");
  };
  const document = {
    getElementById(id) {
      const elements = {
        "account-rights-form": form,
        "account-right-submit": submit,
        "account-right-status": status,
        "account-right-history": history
      };
      if (!elements[id]) throw Error("unexpected browser fixture element");
      return elements[id];
    },
    createElement() { return { textContent: "" }; }
  };
  runInNewContext(source, {
    document, fetch,
    crypto: { randomUUID: () => `00000000-0000-4000-8000-${String(++sequence).padStart(12, "0")}` },
    sessionStorage: {
      getItem(key) { return stored.get(key) ?? null; },
      setItem(key, value) { stored.set(key, value); }
    }
  }, { filename: "advertiser-account-rights.js" });
  // The browser IIFE starts initialize() without awaiting it.
  await new Promise(resolve => setImmediate(resolve));
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(typeof onSubmit, "function");
  return {
    submit, status, get postCount() { return postCount; },
    get historyCount() { return historyCount; },
    get resetCount() { return resetCount; },
    initialKey: stored.get("alpha.advertiser-privacy-case.request-key.v1"),
    get storedKey() { return stored.get("alpha.advertiser-privacy-case.request-key.v1"); },
    submittedKeys,
    async send() { await onSubmit({ preventDefault() {} }); }
  };
}

const historyDown = await mount({ historyOk: false });
assert.equal(historyDown.historyCount, 1);
assert.equal(historyDown.submit.disabled, false,
  "optional history outage must not disable a verified privacy-intake session");
assert.match(historyDown.status.textContent, /history is unavailable/);
await historyDown.send();
assert.equal(historyDown.postCount, 1, "new request should still reach its own authorized POST");
assert.equal(historyDown.submit.disabled, false);
assert.match(historyDown.status.textContent, /your request was accepted\. Do not resubmit it/);
assert.equal(historyDown.historyCount, 2);
assert.equal(historyDown.resetCount, 1, "valid 201 receipt permits form reset");
assert.notEqual(historyDown.storedKey, historyDown.initialKey, "rotate only after valid receipt");

const replay = await mount({ postMode: "replay" });
await replay.send();
assert.equal(replay.postCount, 1);
assert.equal(replay.resetCount, 1, "verified 200 idempotent replay is accepted");
assert.notEqual(replay.storedKey, replay.initialKey);
assert.match(replay.status.textContent, /Request VERIFYING/);

for (const postMode of ["missing", "wrong-type", "wrong-status", "bad-date", "bad-id", "unexpected", "network-error"]) {
  const client = await mount({ postMode });
  await client.send();
  assert.equal(client.postCount, 1, postMode + ": no automatic extra POST");
  assert.equal(client.resetCount, 0, postMode + ": unverified receipt cannot clear input");
  assert.equal(client.storedKey, client.initialKey, postMode + ": preserve key for safe retry");
  assert.equal(client.submittedKeys[0], client.initialKey);
  assert.equal(client.historyCount, 1, postMode + ": no post-receipt history lookup");
  assert.doesNotMatch(client.status.textContent, /Request (RECEIVED|VERIFYING)\./,
    postMode + ": must not claim accepted receipt");
}
const keyConflict = await mount({ postMode: "conflict" });
await keyConflict.send();
assert.equal(keyConflict.postCount, 1);
assert.equal(keyConflict.resetCount, 0);
assert.notEqual(keyConflict.storedKey, keyConflict.initialKey,
  "a definitively rejected 409 key rotates for the next explicit submit");
assert.match(keyConflict.status.textContent, /No new case was created/);

const authDown = await mount({ sessionOk: false, historyOk: false });
assert.equal(authDown.submit.disabled, true, "unverified sessions must remain blocked");
assert.equal(authDown.historyCount, 0, "do not request history without valid sign-in");
await authDown.send();
assert.equal(authDown.postCount, 0, "unverified sign-in must not POST");

const featureDown = await mount({ configOk: false });
assert.equal(featureDown.submit.disabled, true, "disabled feature must remain blocked");
assert.equal(featureDown.historyCount, 0);
await featureDown.send();
assert.equal(featureDown.postCount, 0);

console.log("TASK3B_PRIVACY_INITIAL_HISTORY_FAILURE_NONBLOCKING_PASS");
console.log("TASK3B_PRIVACY_INVALID_AUTH_AND_DISABLED_FLAG_DENIED_PASS");
console.log("TASK3B_PRIVACY_VALID_RECEIPT_AND_SAFE_RETRY_PASS");
console.log("TASK3B_PRIVACY_MALFORMED_RECEIPT_PRESERVES_KEY_PASS");
