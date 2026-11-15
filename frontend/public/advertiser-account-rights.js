"use strict";
(function () {
  const form = document.getElementById("account-rights-form");
  const submit = document.getElementById("account-right-submit");
  const status = document.getElementById("account-right-status");
  const history = document.getElementById("account-right-history");
  const endpoint = "/api/advertisers/privacy-requests";
  const keyName = "alpha.advertiser-privacy-case.request-key.v1";
  let requestKey = sessionStorage.getItem(keyName) || crypto.randomUUID();
  let session;
  let enabled = false;
  sessionStorage.setItem(keyName, requestKey);

  function showHistory(cases) {
    history.replaceChildren();
    if (!Array.isArray(cases) || cases.length === 0) {
      const empty = document.createElement("li");
      empty.textContent = "No account requests recorded for this verified sign-in.";
      history.appendChild(empty);
      return;
    }
    for (const item of cases) {
      const row = document.createElement("li");
      row.textContent = `${item.requestType} · ${item.status} · Ref ${item.caseId} · ${item.createdAt}`;
      history.appendChild(row);
    }
  }

  async function refreshHistory() {
    const res = await fetch(endpoint, { credentials: "same-origin", cache: "no-store" });
    if (!res.ok) throw new Error("Account case history is unavailable.");
    showHistory(await res.json());
  }

  async function initialize() {
    try {
      const [configRes, sessionRes] = await Promise.all([
        fetch("/api/auth/supabase-config", { credentials: "same-origin", cache: "no-store" }),
        fetch("/api/auth/session", { credentials: "same-origin", cache: "no-store" })
      ]);
      const config = configRes.ok ? await configRes.json() : null;
      session = sessionRes.ok ? await sessionRes.json() : null;
      enabled = config?.advertiserRegistrationEnabled === true && session?.isAuthenticated === true
        && typeof session?.csrfToken === "string" && session.csrfToken.length > 0;
      submit.disabled = !enabled;
      status.textContent = enabled
        ? "Verified sign-in detected. This case intake remains subject to the environment's privacy handling approval."
        : "Account privacy requests are not enabled for this environment or sign-in. No request can be accepted here.";
      if (enabled) await refreshHistory();
    } catch (_) {
      submit.disabled = true;
      enabled = false;
      status.textContent = "Account privacy request configuration is unavailable. No request was submitted.";
    }
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!enabled || !session?.csrfToken || !form.reportValidity()) return;
    submit.disabled = true;
    status.textContent = "Submitting account privacy request…";
    try {
      const res = await fetch(endpoint, {
        method: "POST", credentials: "same-origin",
        headers: { "Content-Type": "application/json", "RequestVerificationToken": session.csrfToken },
        body: JSON.stringify({ requestKey, requestType: form.elements.requestType.value })
      });
      const payload = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(res.status === 409
        ? "This request key was already used. Refresh before retrying a different request."
        : "The account request was not accepted. Please verify your sign-in.");
      status.textContent = `Request ${payload.status}. Reference: ${payload.caseId}. This is not a completed deletion, marketing withdrawal, or resolution.`;
      requestKey = crypto.randomUUID();
      sessionStorage.setItem(keyName, requestKey);
      form.reset();
      await refreshHistory();
    } catch (error) {
      status.textContent = error.message || "Request not verified; please retry with the same request key.";
    } finally {
      submit.disabled = !enabled;
    }
  });

  initialize();
})();
