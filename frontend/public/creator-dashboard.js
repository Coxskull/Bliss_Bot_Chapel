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
  function showInventory(profile) {
    const rows = profile.inventory;
    const valid = Array.isArray(rows) && rows.length <= 50 &&
      rows.every(row => row && typeof row.slotId === "string" &&
        typeof row.contentItemId === "string" && typeof row.contentTitle === "string" &&
        typeof row.contentType === "string" && typeof row.slotType === "string" &&
        typeof row.isAvailable === "boolean" &&
        [row.startSecond,row.durationSeconds].every(v => v === null || Number.isSafeInteger(v))) &&
      new Set(rows.map(row => row.slotId)).size === rows.length;
    if (!valid) return;
    byId("inventory-count").textContent = rows.length + " recorded slots" +
      (rows.length === 50 ? " (first 50)." : ".");
    const container = byId("inventory-rows");
    for (const row of rows) {
      const card = document.createElement("article");
      for (const [tag,text] of [["h4",row.contentTitle],["p",row.contentType + " · " + row.slotType],
        ["p","Slot reference: " + row.slotId],["p","Start: " + (row.startSecond ?? "Not recorded") +
        " · Duration (seconds): " + (row.durationSeconds ?? "Not recorded")],
        ["p",row.isAvailable ? "Recorded available — not a booking confirmation" : "Recorded unavailable"]]) {
        const node = document.createElement(tag); node.textContent = text; card.appendChild(node);
      }
      container.appendChild(card);
    }
    show("inventory-panel",true);
  }
  const clearProfile = () => {
    show("inventory-panel",false);
    byId("inventory-count").textContent = "";
    byId("inventory-rows").replaceChildren();
    // Private details must not survive a session refresh, failed role check
    // or sign-out in a hidden DOM node.
    for (const id of [
      "creator-name", "creator-id", "creator-market",
      "creator-language", "creator-status", "application-receipt", "application-state", "channel-proof-code", "channel-proof-url", "channel-proof-expiry"
    ]) {
      byId(id).textContent = "—";
    }
  };

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
    show("application-panel", false);
    show("channel-proof-panel", false);
    show("profile-panel", false);
    clearProfile();
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
        message("Sign in with your existing ALPHA creator account to view your application or verified profile.");
        show("signin-panel", true);
        return;
      }

      // Pending registration sessions have no roles. The existing server
      // registration endpoint verifies the issuer-bound creator lane and
      // derives ownership from the signed session, never from a URL ID.
      if (Array.isArray(session.roles) && session.roles.length === 0) {
        const application = await readJson("/api/creators/registration");
        const item = application.data;
        if (application.response.ok && item &&
            ["PENDING_REVIEW", "ACTIVE", "REVOKED"].includes(item.status) &&
            typeof item.receiptId === "string" &&
            /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(item.receiptId)) {
          byId("application-receipt").textContent = item.receiptId;
          byId("application-state").textContent = item.status === "ACTIVE" ? "Registration approved" : item.status === "REVOKED" ? "Revoked" : "Pending review";
          badgeText(item.status === "ACTIVE" ? "APPROVED" : item.status === "REVOKED" ? "REVOKED" : "PENDING REVIEW");
          message(item.status === "ACTIVE"
            ? "Your registration is approved. Channel verification and workspace access remain separate."
            : item.status === "REVOKED" ? "Your registration access has been revoked. Contact ALPHA support."
            : "Your creator application has been received and is awaiting human review.");
          show("application-panel", true);
          if (item.status === "ACTIVE" && item.verifiedProfile?.authorityStatus === "VERIFIED"
              && item.verifiedProfile.creatorId === item.creatorId && typeof item.creatorId === "string"
              && typeof item.verifiedProfile.name === "string") {
            const p = item.verifiedProfile;
            byId("creator-name").textContent = p.name;
            byId("creator-id").textContent = p.creatorId;
            byId("creator-market").textContent = p.countryCode || "Not recorded";
            byId("creator-language").textContent = p.primaryLanguage || "Not recorded";
            byId("creator-status").textContent = p.authorityStatus;
            badgeText("VERIFIED"); message("Channel control verified. Your private creator profile is ready.", "success");
            show("profile-panel", true);
            showInventory(p);
          } else if (item.status === "ACTIVE" && typeof item.channelProofCode === "string"
              && /^ALPHA-[0-9A-F]{32}$/.test(item.channelProofCode)
              && typeof item.submittedChannelUrl === "string" && typeof item.proofExpiresAt === "string") {
            byId("channel-proof-code").textContent = item.channelProofCode;
            byId("channel-proof-url").textContent = item.submittedChannelUrl;
            byId("channel-proof-expiry").textContent = item.proofExpiresAt;
            show("channel-proof-panel", true);
          }
          return;
        }
        if (application.response.status === 401) {
          badgeText("SESSION EXPIRED"); show("signin-panel", true); logoutButton.hidden = true;
          message("Sign in again to view your application.", "error");
        } else if (application.response.status === 404 || application.response.status === 403) {
          badgeText("ACCESS PENDING"); show("pending-panel", true);
          message("No accessible creator application. Use creator sign-in or complete registration.");
        } else {
          badgeText("UNAVAILABLE"); message("Could not verify your application status.", "error");
        }
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
      showInventory(profile);
    } catch {
      badgeText("UNAVAILABLE");
      message("We could not reach the account service. Please retry in a moment.", "error");
    } finally {
      busy = false;
      refreshButton.disabled = false;
    }
  }

  function signOut() {
    if (busy || !csrfToken) return;
    busy = true;
    show("profile-panel", false);
    show("application-panel", false);
    show("channel-proof-panel", false);
    clearProfile();
    logoutButton.disabled = true;
    message("Signing out securely…");
    // OIDC logout is a top-level redirect to the identity provider. A fetch()
    // would follow that cross-origin redirect and can fail CORS after the
    // application cookie has already been cleared. Submit a same-origin form
    // so the browser completes the full cookie + provider sign-out redirect.
    const form = document.createElement("form");
    form.method = "POST";
    form.action = "/api/auth/logout";
    form.hidden = true;
    const token = document.createElement("input");
    token.type = "hidden";
    token.name = "__RequestVerificationToken";
    token.value = csrfToken;
    form.appendChild(token);
    document.body.appendChild(form);
    form.submit();
  }

  refreshButton.addEventListener("click", loadDashboard);
  logoutButton.addEventListener("click", signOut);
  loadDashboard();
})();
