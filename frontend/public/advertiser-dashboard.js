(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const status = el("status"), badge = el("access-badge");
  const refresh = el("refresh"), logout = el("logout");
  let busy = false, csrf = null;
  function show(id, visible) { el(id).hidden = !visible; }
  function message(value, error = false) {
    status.textContent = value;
    status.className = "status" + (error ? " error" : "");
  }
  function clear() {
    for (const id of ["business-name", "business-id", "business-country", "business-website"])
      el(id).textContent = "—";
    show("profile-panel", false);
  }
  async function get(path) {
    const response = await fetch(path, {
      method: "GET", credentials: "same-origin", cache: "no-store",
      headers: { "Accept": "application/json" }
    });
    let body = null;
    try { body = await response.json(); } catch { /* status drives denial */ }
    return { response, body };
  }
  async function load() {
    if (busy) return;
    busy = true; refresh.disabled = true;
    show("signin-panel", false); show("pending-panel", false);
    clear(); csrf = null; logout.hidden = true;
    badge.textContent = "CHECKING"; message("Checking your account session…");
    try {
      const { response: sessionResponse, body: session } = await get("/api/auth/session");
      if (!sessionResponse.ok || !session) {
        badge.textContent = "UNAVAILABLE";
        message("Secure account sign-in is not available yet.", true);
        return;
      }
      csrf = typeof session.csrfToken === "string" ? session.csrfToken : null;
      if (session.isAuthenticated && csrf) logout.hidden = false;
      if (!session.authenticationEnabled) {
        badge.textContent = "NOT OPEN";
        message("Advertiser account sign-in is not enabled in this environment.");
        return;
      }
      if (!session.isAuthenticated) {
        badge.textContent = "SIGN IN";
        message("Sign in with your approved advertiser account.");
        show("signin-panel", true);
        return;
      }
      const { response, body } = await get("/api/advertisers/me");
      if (response.status === 401) {
        badge.textContent = "SESSION EXPIRED";
        message("Your session expired. Sign in again.", true);
        logout.hidden = true; show("signin-panel", true);
        return;
      }
      if (response.status === 403 || response.status === 404) {
        badge.textContent = "ACCESS PENDING";
        message("No verified business profile is assigned to this account.");
        show("pending-panel", true);
        return;
      }
      if (!response.ok || !body) {
        badge.textContent = "UNAVAILABLE";
        message("Your business profile is unavailable.", true);
        return;
      }
      // Never infer account approval from HTTP 200, a profile name or a
      // public interest receipt. Require the verified server-side marker.
      if (body.authorityStatus !== "VERIFIED" ||
          typeof body.name !== "string" || !body.name.trim() ||
          typeof body.advertiserId !== "string" ||
          !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(body.advertiserId)) {
        badge.textContent = "NOT VERIFIED";
        message("The server did not confirm a verified business profile.", true);
        show("pending-panel", true);
        return;
      }
      el("business-name").textContent = body.name;
      el("business-id").textContent = body.advertiserId;
      el("business-country").textContent = body.countryCode || "Not recorded";
      el("business-website").textContent = body.website || "Not recorded";
      badge.textContent = "APPROVED";
      message("Your approved business profile is available. Other services remain disabled.");
      show("profile-panel", true);
    } catch {
      badge.textContent = "UNAVAILABLE";
      message("The secure account service could not be reached.", true);
    } finally {
      busy = false; refresh.disabled = false;
    }
  }
  refresh.addEventListener("click", load);
  logout.addEventListener("click", () => {
    if (busy || !csrf) return;
    busy = true; clear(); logout.disabled = true; message("Signing out securely…");
    const form = document.createElement("form");
    form.method = "POST"; form.action = "/api/auth/logout"; form.hidden = true;
    const token = document.createElement("input");
    token.type = "hidden"; token.name = "__RequestVerificationToken"; token.value = csrf;
    form.appendChild(token); document.body.appendChild(form); form.submit();
  });
  load();
})();
