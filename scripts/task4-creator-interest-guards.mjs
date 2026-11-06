import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { mapGhlContact, ghlNotice } from "../supabase/functions/alpha-public-intake/ghl-payload.mjs";

// Task #4: no live submissions, credentials, provider calls, or account creation.
// This is a static+pure-function acceptance guard around the existing creator
// interest boundary; authenticated creator registration is a SEPARATE open gate.
const read = (p) => readFileSync(new URL("../" + p, import.meta.url), "utf8");
const html = read("frontend/public/join-creator.html");
const browser = read("frontend/public/alpha-intake.js");
const edge = read("supabase/functions/alpha-public-intake/index.ts");
const ghlsync = read("supabase/functions/alpha-public-intake/ghl-sync.mjs");
const privacy = read("frontend/public/alpha-privacy.html");
const creatorAccount = read("frontend/public/creator-account.html");
const creatorDashboard = read("frontend/public/creator-dashboard.html");
const creatorDashboardJs = read("frontend/public/creator-dashboard.js");
const creatorDashboardCss = read("frontend/public/creator-dashboard.css");

let assertions = 0;
const ok = (condition, label) => { assert.ok(condition, label); assertions++; };
const has = (source, part, label) => ok(source.includes(part), label);
const absent = (source, part, label) => ok(!source.includes(part), label);

has(html, 'data-kind="creator"', "creator-only intake type is explicit");
has(html, 'data-endpoint="https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake"', "intake posts to existing server boundary");
for (const field of ['full_name','email','website','market','category']) {
  has(html, 'name="' + field + '"', field + " present");
}
has(html, 'name="processing_acknowledged" required', "processing acknowledgement required");
has(html, 'name="adult_confirmed" required', "adult confirmation required");
has(html, 'name="marketing_opt_in"', "optional marketing choice provided");
absent(html, 'name="marketing_opt_in" checked', "marketing is not preselected");
has(html, "/alpha-privacy.html", "privacy notice linked");
has(html, "PUBLIC INQUIRY · NOT AN ACCOUNT", "creator understands public inquiry is not an authenticated account");
has(html, "does not create an ALPHA account", "creator is told no account is created");

has(browser, "crypto.randomUUID()", "request key generated locally");
has(browser, "pendingKey = null", "edited drafts reset idempotency key");
has(browser, "response.status === 201", "success requires HTTP 201");
has(browser, 'result?.status === "RECEIVED_NOT_YET_VERIFIED"', "success requires unverified receipt state");
has(browser, "result.receipt_id", "server receipt required");
has(browser, 'credentials: "omit"', "cross-origin user cookies not forwarded");
for (const leak of ["SUPABASE_SERVICE_ROLE_KEY","ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN","localStorage","sessionStorage"]) {
  absent(browser, leak, "no browser secret/storage: " + leak);
}
has(edge, 'const kind = payload.kind;', "server evaluates request kind");
has(edge, '(kind === "creator" &&', "server performs creator-specific required input checks");
has(edge, 'payload.processing_acknowledged === true', "server enforces acknowledgement");
has(edge, 'payload.adult_confirmed === true', "server enforces adult confirmation");
has(edge, 'if (!origin || !allowed.has(origin))', "server rejects foreign browser origins");
has(edge, '.eq("request_id", record.id).single()', "server independently reads linked receipt");
has(edge, 'deliverToGhl(client, record.id)', "CRM handoff happens server-side");
has(edge, 'status: "RECEIVED_NOT_YET_VERIFIED"', "server response cannot claim verified identity");
for (const forbidden of ["auth.signUp(", "auth.admin.createUser(", "createUser(", "role: \"creator_verified\""]) {
  absent(edge, forbidden, "public intake cannot create verified creator identity");
}
has(ghlsync, 'Deno.env.get("ALPHA_GHL_SYNC_ENABLED") !== "true"', "CRM delivery fail-closed toggle");
has(ghlsync, 'client.rpc("alpha_ghl_claim"', "CRM delivery uses claim lease");
has(privacy, ghlNotice, "published notice matches CRM consent version");

// Creator account setup is invitation-only; preserve the existing public
// interest form and keep the authenticated dashboard on the Railway origin.
has(creatorAccount, "INVITATION ONLY", "creator account setup is invitation-only");
has(creatorAccount, "/join-creator.html", "account setup points to existing interest form");
has(creatorAccount, "/api/auth/login?returnUrl=%2Fcreator-dashboard.html", "sign-in returns to creator dashboard");
has(creatorAccount, "does not create users", "account explainer cannot create identities");
has(creatorDashboard, 'name="robots" content="noindex,nofollow"', "creator dashboard is not indexed");
has(creatorDashboard, "/api/auth/login?returnUrl=%2Fcreator-dashboard.html", "dashboard login return path is local");
has(creatorDashboard, 'id="profile-panel"', "profile is hidden until authorized response");
has(creatorDashboard, "Not yet enabled", "commercial features remain unavailable");
has(creatorDashboardJs, 'readJson("/api/auth/session")', "dashboard checks server session");
has(creatorDashboardJs, 'fetch(url,', "API calls use a single same-origin helper");
has(creatorDashboardJs, 'credentials: "same-origin"', "dashboard never sends cookies cross-origin");
has(creatorDashboardJs, 'readJson("/api/creators/me")', "dashboard reads only caller's creator profile");
has(creatorDashboardJs, 'token.name = "__RequestVerificationToken"', "logout form uses ASP.NET antiforgery field");
has(creatorDashboardJs, 'token.value = csrfToken', "logout form sends server-issued antiforgery token");
has(creatorDashboardJs, 'form.action = "/api/auth/logout"', "logout stays on the application origin");
has(creatorDashboardJs, 'form.submit()', "logout completes top-level OIDC redirect");
has(creatorDashboardJs, 'profile.authorityStatus !== "VERIFIED"', "dashboard fails closed for unverified records");
for (const leak of ["localStorage","sessionStorage","innerHTML","SUPABASE_SERVICE_ROLE_KEY","ALPHA_GHL_PRIVATE_INTEGRATION_TOKEN","auth.signUp("]) {
  absent(creatorDashboardJs + creatorAccount, leak, "creator UI does not cache tokens, render unsafe HTML or create public auth users: " + leak);
}
has(creatorDashboardCss, "@media(max-width:700px)", "dashboard has mobile layout");

const fixture = {
  kind: "creator", notice_version: ghlNotice,
  full_name: "Fixture Creator", email: "fixture@creator.test",
  website: "https://example.org/fictional-public-profile", organization: "",
  processing_acknowledged: true, adult_confirmed: true,
  description: "Private data never for CRM", market: "Fixture City",
  category: "Podcast", marketing_opt_in: false
};
const location = "aBcD1234EFgh5678IJkl";
const mapped = mapGhlContact(fixture, location);
assert.deepEqual(mapped, {
  locationId: location,
  name: fixture.full_name,
  email: fixture.email,
  source: "ALPHA GLOBAL MEDIA · Creator Interest",
  createNewIfDuplicateAllowed: false,
  website: fixture.website
});
assertions++;
for (const key of ["description","market","category","marketing_opt_in","tags","phone","dnd","password","roles"]) {
  ok(!(key in mapped), "private or privileged field excluded from CRM: " + key);
}
for (const altered of [
  {kind:"privacy"}, {notice_version:"UNAPPROVED"}, {processing_acknowledged:false},
  {adult_confirmed:false}, {email:"synthetic@example.invalid"}
]) {
  assert.throws(() => mapGhlContact({...fixture,...altered}, location));
  assertions++;
}
console.log("TASK4_CREATOR_INTEREST_BOUNDARY_GUARDS_PASS assertions=" + assertions);
console.log("No network or database calls. Authenticated marketplace signup NOT tested.");
