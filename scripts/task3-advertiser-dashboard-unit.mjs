import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

// Task #3: offline synthetic-only browser acceptance. Never requests real
// sessions, executes hosted API calls, stores customer data or creates users.
const dashboard = readFileSync(new URL("../frontend/public/advertiser-dashboard.js", import.meta.url), "utf8");
const controller = readFileSync(new URL("../Bliss.Api/Controllers/AdvertiserAccountController.cs", import.meta.url), "utf8");
const html = readFileSync(new URL("../frontend/public/advertiser-dashboard.html", import.meta.url), "utf8");
assert.match(controller, /TryResolveVerifiedAdvertiserId\(User, _auth, out var advertiserId\)/);
assert.match(controller, /new AdvertiserSelfSummaryDto\(a.Id, a.Name, a.CountryCode, a.Website, "VERIFIED"\)/);
assert.match(controller, /Response.Headers.CacheControl = "no-store"/);
assert.match(html, /id="profile-panel"[^>]*hidden/);
assert.doesNotMatch(dashboard, /localStorage|sessionStorage|innerHTML|SUPABASE_SERVICE_ROLE_KEY/);

const id = "89b5b0dc-0664-4cb4-a55d-61aa70d3470d";
const profile = Object.freeze({
  advertiserId: id, name: "<img src=x onerror=alert(1)> Example Co.",
  countryCode: "PH", website: "https://example.invalid", authorityStatus: "VERIFIED"
});
const resp = (status, data = null) => ({
  status, ok: status >= 200 && status < 300, json: async () => data
});
const session = (authenticated = false) => ({
  authenticationEnabled: true, isAuthenticated: authenticated,
  csrfToken: authenticated ? "synthetic-antiforgery" : null
});
const ids = ["status","access-badge","refresh","logout","signin-panel",
  "pending-panel","profile-panel","business-name","business-id",
  "business-country","business-website"];
const privateIds = ["business-name","business-id","business-country","business-website"];
function fixture(sessionReply, businessReply = resp(500)) {
  const nodes = Object.fromEntries(ids.map(key => [key, {
    hidden: ["logout","signin-panel","pending-panel","profile-panel"].includes(key),
    textContent: "", className: "", disabled: false, handlers: {},
    addEventListener(type, fn) { this.handlers[type] = fn; }
  }]));
  const calls = [], forms = [];
  let sessionNow = sessionReply, profileNow = businessReply;
  const document = {
    getElementById(key) { assert.ok(nodes[key], key); return nodes[key]; },
    createElement(tag) {
      const el = {
        tagName: tag.toUpperCase(), children: [],
        appendChild(child) { this.children.push(child); },
        submit() { this.submitted = true; }
      };
      forms.push(el);
      return el;
    },
    body: { appendChild() {} }
  };
  async function fakeFetch(url, options) {
    assert.equal(options.method, "GET");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
    calls.push(url);
    if (url === "/api/auth/session") return sessionNow;
    if (url === "/api/advertisers/me") return profileNow;
    assert.fail("Unexpected endpoint " + url);
  }
  vm.runInNewContext(dashboard, { document, fetch: fakeFetch },
    { filename: "advertiser-dashboard.js", timeout: 3000 });
  return {
    nodes, calls, forms,
    setSession(value) { sessionNow = value; },
    setProfile(value) { profileNow = value; },
    async settle() {
      for (let i = 0; i < 8; i++) await Promise.resolve();
      await new Promise(resolve => setImmediate(resolve));
    }
  };
}
function denied(h) {
  assert.equal(h.nodes["profile-panel"].hidden, true, "profile hidden");
  assert.ok(privateIds.every(k => !h.nodes[k].textContent.includes("Example Co.")),
    "private profile not retained");
}
{
  const h=fixture(resp(200, session(false)));
  await h.settle();
  assert.deepEqual(h.calls, ["/api/auth/session"]);
  assert.equal(h.nodes["access-badge"].textContent, "SIGN IN");
  denied(h);
}
{
  const h=fixture(resp(503));
  await h.settle();
  assert.deepEqual(h.calls, ["/api/auth/session"]);
  denied(h);
}
{
  const h=fixture(resp(200, {authenticationEnabled:false,isAuthenticated:false}));
  await h.settle();
  assert.deepEqual(h.calls, ["/api/auth/session"]);
  assert.equal(h.nodes["access-badge"].textContent, "NOT OPEN");
  denied(h);
}
for (const status of [401, 403, 404, 500]) {
  const h=fixture(resp(200, session(true)), resp(status));
  await h.settle();
  assert.deepEqual(h.calls, ["/api/auth/session", "/api/advertisers/me"]);
  assert.equal(h.nodes["access-badge"].textContent,
    status===401?"SESSION EXPIRED":status===500?"UNAVAILABLE":"ACCESS PENDING");
  denied(h);
}
for (const data of [
  {...profile, authorityStatus: undefined},
  {...profile, authorityStatus: "PENDING"},
  {...profile, advertiserId: "not-a-guid"},
  {...profile, name: ""},
  {...profile, authorityStatus: "verified"}
]) {
  const h=fixture(resp(200, session(true)), resp(200, data));
  await h.settle();
  assert.equal(h.nodes["access-badge"].textContent, "NOT VERIFIED");
  denied(h);
}
{
  const h=fixture(resp(200, session(true)), resp(200, profile));
  await h.settle();
  assert.equal(h.nodes["access-badge"].textContent, "APPROVED");
  assert.equal(h.nodes["profile-panel"].hidden, false);
  assert.equal(h.nodes["business-name"].textContent, profile.name,
    "untrusted business name inserted as text, never HTML");
  assert.equal(h.nodes["business-id"].textContent, profile.advertiserId);
  h.setSession(resp(200, session(false)));
  await h.nodes.refresh.handlers.click();
  await h.settle();
  denied(h);
  assert.ok(privateIds.every(k => h.nodes[k].textContent === "—"),
    "refresh removes all previous business details");
}
{
  const h=fixture(resp(200, session(true)), resp(200, profile));
  await h.settle();
  h.nodes.logout.handlers.click();
  denied(h);
  const form=h.forms[0];
  assert.equal(form?.method, "POST");
  assert.equal(form.action, "/api/auth/logout");
  assert.equal(form.children[0].name, "__RequestVerificationToken");
  assert.equal(form.children[0].value, "synthetic-antiforgery");
  assert.equal(form.submitted, true);
}
console.log("TASK3_ADVERTISER_DASHBOARD_FAIL_CLOSED_PASS — synthetic session, marker, role denial, XSS-text and logout regressions");
