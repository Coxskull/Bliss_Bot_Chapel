import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

// Synthetic DOM + same-origin API fixtures. This test never contacts a real
// identity provider, creates an account, or fetches private creator records.
const dashboard = readFileSync(
  new URL("../frontend/public/creator-dashboard.js", import.meta.url), "utf8");

const ids = [
  "status", "access-badge", "refresh", "logout", "signin-panel",
  "pending-panel", "profile-panel", "creator-name", "creator-id",
  "creator-market", "creator-language", "creator-status"
];
const privateIds = [
  "creator-name", "creator-id", "creator-market",
  "creator-language", "creator-status"
];
const verifiedProfile = Object.freeze({
  creatorId: "14d778d7-1a4e-4419-b23d-7aed5e32998b",
  name: "<img src=x onerror=alert(1)> Fixture Channel",
  countryCode: "PH", primaryLanguage: "fil", authorityStatus: "VERIFIED"
});
const session = (isAuthenticated = false) => ({
  authenticationEnabled: true, isAuthenticated,
  csrfToken: isAuthenticated ? "fake-csrf-fixture" : null
});
const response = (status, data = null) => ({
  status, ok: status >= 200 && status < 300,
  redirected: false,
  json: async () => data
});
const fixture = (sessionResponse, profileResponse = null) => {
  const nodes = Object.fromEntries(ids.map(id => [
    id, {
      hidden: ["logout", "signin-panel", "pending-panel", "profile-panel"].includes(id),
      disabled: false,
      textContent: "", className: "", handlers: {},
      addEventListener(name, callback) { this.handlers[name] = callback; }
    }
  ]));
  const calls = [];
  const navigations = [];
  let currentSession = sessionResponse;
  let currentProfile = profileResponse;
  async function fakeFetch(url, options) {
    calls.push({ url, options });
    assert.equal(options?.credentials, "same-origin", "no cross-origin cookies");
    assert.equal(typeof url, "string");
    if (url === "/api/auth/session") {
      assert.equal(options.method, "GET");
      return currentSession;
    }
    if (url === "/api/creators/me") {
      assert.equal(options.method, "GET");
      return currentProfile;
    }
    if (url === "/api/auth/logout") {
      assert.equal(options.method, "POST");
      assert.equal(options.headers["X-CSRF-TOKEN"], "fake-csrf-fixture");
      return response(200, {});
    }
    assert.fail("Unexpected or off-origin request: " + url);
  }
  vm.runInNewContext(dashboard, {
    document: { getElementById(id) {
      assert.ok(nodes[id], "unknown DOM element: " + id);
      return nodes[id];
    } },
    fetch: fakeFetch,
    location: { assign(url) { navigations.push(url); } }
  }, { filename: "creator-dashboard.js", timeout: 3000 });
  return {
    nodes, calls, navigations,
    sessionResponse(value) { currentSession = value; },
    profileResponse(value) { currentProfile = value; },
    async settle() {
      // Flush nested async fetch and JSON parse turns without any timers in
      // the tested code. No network, external browser or credentials.
      for (let n = 0; n < 5; n++) await Promise.resolve();
      await new Promise(resolve => setImmediate(resolve));
    }
  };
};

let assertions = 0;
const check = (condition, label) => {
  assert.ok(condition, label);
  assertions += 1;
};
const noPrivateData = h => {
  check(h.nodes["profile-panel"].hidden, "private profile hidden");
  check(privateIds.every(id => !h.nodes[id].textContent.includes("Fixture Channel")), "no prior personal data in hidden DOM");
};

// 1. Anonymous visitor can only see login; never calls creator profile.
{
  const h = fixture(response(200, session(false)));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "SIGN IN", "anonymous sign-in state");
  check(!h.nodes["signin-panel"].hidden, "anonymous login CTA");
  check(h.calls.length === 1 && h.calls[0].url === "/api/auth/session", "no creator GET anonymously");
  noPrivateData(h);
}
// 2. Unavailable service fails closed.
{
  const h = fixture(response(503));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "UNAVAILABLE", "API refusal is not success");
  check(h.calls.length === 1, "no secondary privileged call after refused session");
  noPrivateData(h);
}
// 3. Feature disabled prevents sign-in CTA.
{
  const h = fixture(response(200, { authenticationEnabled: false, isAuthenticated: false }));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "NOT OPEN", "auth disabled state");
  check(h.nodes["signin-panel"].hidden, "login is not advertised in disabled environment");
  noPrivateData(h);
}
// 4. Signed in but not in the creator allowlist remains empty.
{
  const h = fixture(response(200, session(true)), response(403));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "ACCESS PENDING", "forbidden creator remains pending");
  check(!h.nodes["pending-panel"].hidden, "pending panel shown");
  check(h.nodes["logout"].hidden === false, "signed-in non-creator can sign out");
  noPrivateData(h);
}
// 5. Authenticated creator but missing VERIFIED database row is not disclosed.
{
  const h = fixture(response(200, session(true)), response(404));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "NOT VERIFIED", "not found is unverified");
  check(!h.nodes["pending-panel"].hidden, "unverified pending panel");
  noPrivateData(h);
}
// 6. Expired session never shows creator data or sign-out.
{
  const h = fixture(response(200, session(true)), response(401));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "SESSION EXPIRED", "expired session");
  check(h.nodes["logout"].hidden === true, "expired cookie offers no logout");
  noPrivateData(h);
}
// 7. Even HTTP 200 cannot assert verified without an explicit marker.
{
  const h = fixture(response(200, session(true)), response(200, {
    ...verifiedProfile, authorityStatus: "UNVERIFIED"
  }));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "NOT VERIFIED", "bad verification marker denied");
  noPrivateData(h);
}
// 8. Approved creator sees only own summary via textContent, never HTML execution.
{
  const h = fixture(response(200, session(true)), response(200, verifiedProfile));
  await h.settle();
  check(h.nodes["access-badge"].textContent === "VERIFIED", "authorized creator status");
  check(!h.nodes["profile-panel"].hidden, "verified own profile visible");
  check(h.nodes["creator-name"].textContent === verifiedProfile.name, "untrusted name displayed as text only");
  check(h.nodes["creator-id"].textContent === verifiedProfile.creatorId, "server-supplied ID only");
  check(h.calls.map(x => x.url).join(",") === "/api/auth/session,/api/creators/me", "read-only known endpoints");

  // A subsequent anonymous session must clear all previous profile DOM fields.
  h.sessionResponse(response(200, session(false)));
  await h.nodes.refresh.handlers.click();
  await h.settle();
  check(h.nodes["access-badge"].textContent === "SIGN IN", "refresh recognizes sign-out");
  check(privateIds.every(id => h.nodes[id].textContent === "—"), "refresh erases previous profile data");
  noPrivateData(h);
}
// 9. Sign-out clears profile immediately, uses CSRF, and navigates only after 2xx.
{
  const h = fixture(response(200, session(true)), response(200, verifiedProfile));
  await h.settle();
  check(!h.nodes["profile-panel"].hidden, "precondition verified");
  await h.nodes.logout.handlers.click();
  check(h.nodes["profile-panel"].hidden, "logout hides profile");
  check(privateIds.every(id => h.nodes[id].textContent === "—"), "logout clears private DOM");
  check(h.calls.some(x => x.url === "/api/auth/logout"), "logout endpoint invoked");
  check(h.navigations.length === 1 && h.navigations[0] === "/", "successful logout navigates locally");
}
console.log("TASK4_CREATOR_DASHBOARD_UNIT_PASS assertions=" + assertions);
console.log("All scenarios use synthetic data; no browser, Supabase or marketplace writes.");
