(() => {
  "use strict";
  const byId = id => document.getElementById(id);
  const status = byId("status");
  const badge = byId("access-badge");
  const refreshButton = byId("refresh");
  const logoutButton = byId("logout");
  let csrfToken = null;
  let busy = false;

  const show = (id, visible) => { byId(id).hidden = !visible; };
  const message = (text, tone = "") => {
    status.textContent = text;
    status.className = "status" + (tone ? " " + tone : "");
  };
  const badgeText = text => { badge.textContent = text; };

  async function readJson(url) {
    const response = await fetch(url, {
      method: "GET",
      credentials: "same-origin",
      cache: "no-store",
      headers: { "Accept": "application/json" }
    });
    let data = null;
    try { data = await response.json(); } catch { /* status is handled below */ }
    return { response, data };
  }

  async function loadDashboard() {
    if (busy) return;
    busy = true;
    refreshButton.disabled = true;
    show("signin-panel", false);
    show("pending-panel", false);
    show("profile-panel", false);
    logoutButton.hidden = true;
    csrfToken = null;
    badgeText("CHECKING");
    message("Checking your secure account session…");
    try {
      const sessionResult = await readJson("/api/auth/session");
      if (!sessionResult.response.ok || !sessionResult.data) {
        badgeText("UNAVAILABLE");
        message("We could not confirm your account session. Please retry.", "error");
        return;
      }
      const session = sessionResult.data;
      csrfToken = typeof session.csrfToken === "string" ? session.csrfToken : null;
      if (session.isAuthenticated && csrfToken) logoutButton.hidden = false;

      if (!session.authenticationEnabled) {
        badgeText("NOT OPEN");
        message("Creator account sign-in is not enabled in this environment. The public creator interest form remains available.");
        show("signin-panel", false);
        return;
      }
      if (!session.isAuthenticated) {
        badgeText("SIGN IN");
        message("Sign in with the secure invitation issued to your approved creator account.");
        show("signin-panel", true);
        return;
      }

      const profileResult = await readJson("/api/creators/me");
      if (profileResult.response.status === 401) {
        badgeText("SESSION EXPIRED");
        message("Your session has expired. Sign in again to continue.", "error");
        show("signin-panel", true);
        logoutButton.hidden = true;
        return;
      }
      if (profileResult.response.status === 403) {
        badgeText("ACCESS PENDING");
        message("This account does not currently have approved creator access. Public interest submissions do not grant account privileges.");
        show("pending-panel", true);
        return;
      }
      if (profileResult.response.status === 404) {
        badgeText("NOT VERIFIED");
        message("No verified creator profile is linked to this account yet. ALPHA must complete the identity and creator-record review.");
        show("pending-panel", true);
        return;
      }
      if (!profileResult.response.ok || !profileResult.data) {
        badgeText("UNAVAILABLE");
        message("We could not load your creator profile. Please retry.", "error");
        return;
      }

      const profile = profileResult.data;
      if (profile.authorityStatus !== "VERIFIED" ||
          typeof profile.creatorId !== "string" ||
          typeof profile.name !== "string") {
        badgeText("NOT VERIFIED");
        message("The server did not return a verified creator profile. No workspace data is shown.");
        show("pending-panel", true);
        return;
      }

      byId("creator-name").textContent = profile.name;
      byId("creator-id").textContent = profile.creatorId;
      byId("creator-market").textContent = profile.countryCode || "Not recorded";
      byId("creator-language").textContent = profile.primaryLanguage || "Not recorded";
      byId("creator-status").textContent = profile.authorityStatus;
      badgeText("VERIFIED");
      message("Your verified creator profile is ready. Additional marketplace features remain unavailable until separately approved.", "success");
      show("profile-panel", true);
    } catch {
      badgeText("UNAVAILABLE");
      message("We could not reach the account service. Please retry in a moment.", "error");
    } finally {
      busy = false;
      refreshButton.disabled = false;
    }
  }

  async function signOut() {
    if (busy || !csrfToken) return;
    busy = true;
    logoutButton.disabled = true;
    try {
      const response = await fetch("/api/auth/logout", {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        headers: { "X-CSRF-TOKEN": csrfToken }
      });
      if (response.ok || response.redirected) {
        location.assign("/");
        return;
      }
      message("Sign-out could not be confirmed. Refresh the session and try again.", "error");
    } catch {
      message("Sign-out could not be confirmed. Please retry.", "error");
    } finally {
      busy = false;
      logoutButton.disabled = false;
    }
  }

  refreshButton.addEventListener("click", loadDashboard);
  logoutButton.addEventListener("click", signOut);
  loadDashboard();
})();
