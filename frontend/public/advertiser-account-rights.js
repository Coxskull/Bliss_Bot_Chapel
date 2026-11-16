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
      enabled = config?.advertiserPrivacyRequestsEnabled === true && session?.isAuthenticated === true
        && typeof session?.csrfToken === "string" && session.csrfToken.length > 0;
      submit.disabled = !enabled;
      status.textContent = enabled
        ? "Verified sign-in detected. This case intake remains subject to the environment's privacy handling approval."
        : "Account privacy requests are not enabled for this environment or sign-in. No request can be accepted here.";
      if (enabled) {
        // Reading older receipts is optional to accepting a NEW request.
        // A transient history failure must not turn a verified, CSRF-protected
        // session into a disabled privacy-rights form. The POST still passes
        // all independent server-side authorization and account checks.
        try {
          await refreshHistory();
        } catch (_) {
          status.textContent = "Account request history is unavailable. You may submit a new request, subject to account verification.";
        }
      }
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
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": session.csrfToken },
        body: JSON.stringify({ requestKey, requestType: form.elements.requestType.value })
      });
      const payload = await res.json().catch(() => ({}));
      if (res.status === 409) {
        // A rejected key may belong to an earlier request or sign-in.
        // Refresh would restore that same key from sessionStorage and loop.
        // Rotate only after the server definitively rejects it; never
        // silently retry a POST or rotate on a network/unknown failure.
        requestKey = crypto.randomUUID();
        sessionStorage.setItem(keyName, requestKey);
        throw new Error("This request key was already used. No new case was created. Review your request and submit again.");
      }
      if (!res.ok) throw new Error("The account request was not accepted. Please verify your sign-in.");
      // A successful HTTP code alone does not prove that a case was recorded.
      // A failed or malformed receipt must retain the SAME idempotency key,
      // including after a possible upstream acceptance or delayed response.
      const isUuid = value => typeof value === "string"
        && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
      if ((res.status !== 200 && res.status !== 201)
          || !isUuid(payload.caseId)
          || !isUuid(payload.requestKey)
          || payload.requestKey.toLowerCase() !== requestKey.toLowerCase()
          || payload.requestType !== form.elements.requestType.value
          || (payload.status !== "RECEIVED" && payload.status !== "VERIFYING")
          || typeof payload.createdAt !== "string"
          || !Number.isFinite(Date.parse(payload.createdAt))) {
        throw new Error("The privacy request receipt could not be verified. Keep this request and retry with the same request key.");
      }
      // The POST receipt is authoritative. A later GET/history failure must
      // never turn an accepted privacy request into a "submission failed"
      // message or encourage another request with a newly rotated key.
      const receiptMessage = `Request ${payload.status}. Reference: ${payload.caseId}. This is not a completed deletion, marketing withdrawal, or resolution.`;
      status.textContent = receiptMessage;
      requestKey = crypto.randomUUID();
      sessionStorage.setItem(keyName, requestKey);
      form.reset();
      try {
        await refreshHistory();
      } catch (_) {
        status.textContent = receiptMessage + " Case history could not be refreshed; your request was accepted. Do not resubmit it.";
      }
    } catch (error) {
      status.textContent = error.message || "Request not verified; please retry with the same request key.";
    } finally {
      submit.disabled = !enabled;
    }
  });

  initialize();
})();
