(() => {
  "use strict";
  const queue = document.getElementById("queue"), status = document.getElementById("status");
  const refresh = document.getElementById("refresh");
  const endpoint = "/api/advertisers/operator-review";
  let busy = false, csrf = null;
  const add = (parent, tag, value) => {
    const node = document.createElement(tag);
    node.textContent = value; parent.appendChild(node); return node;
  };
  function render(row) {
    const card = document.createElement("article"); queue.appendChild(card);
    add(card, "h2", row.businessName);
    add(card, "p", "Country: " + (row.countryCode || "Not recorded"));
    add(card, "p", "Website: " + (row.website || "Not recorded"));
    add(card, "p", "Receipt: " + row.receiptId);
    add(card, "p", "Business ID: " + row.advertiserId);
    add(card, "p", "Issuer subject: " + row.subject);
    add(card, "p", "Privacy: " + row.privacyNoticeVersion + " · Terms: " + row.termsVersion);
    add(card, "p", "Required consent: " + (row.processingConsent && row.termsAccepted ? "Accepted" : "Missing"));
    function checkbox(label) {
      const wrapper = document.createElement("label");
      const input = document.createElement("input"); input.type = "checkbox";
      wrapper.appendChild(input); wrapper.appendChild(document.createTextNode(" " + label));
      card.appendChild(wrapper); card.appendChild(document.createElement("br")); return input;
    }
    const identity = checkbox("I reviewed and verified the business identity.");
    const consent = checkbox("I reviewed the original required consent and terms.");
    const approve = add(card, "button", "Approve membership");
    approve.type = "button"; approve.disabled = true;
    function sync() { approve.disabled = busy || !csrf || !identity.checked || !consent.checked || !row.processingConsent || !row.termsAccepted; }
    identity.addEventListener("change", sync); consent.addEventListener("change", sync);
    approve.addEventListener("click", async () => {
      if (approve.disabled || busy) return;
      if (!window.confirm("Approve only this exact business membership? This permits read-only customer access when membership-based access is enabled.")) return;
      busy = true; refresh.disabled = true; approve.disabled = true;
      try {
        const response = await fetch(endpoint, { method: "POST", credentials: "same-origin", cache: "no-store",
          headers: {"Content-Type":"application/json", "X-CSRF-TOKEN":csrf},
          body: JSON.stringify({receiptId:row.receiptId, advertiserId:row.advertiserId, subject:row.subject,
            businessIdentityReviewed:identity.checked, consentReviewed:consent.checked}) });
        if (!response.ok) throw new Error("Approval not accepted. Refresh the queue before retrying.");
        const result = await response.json();
        if (result.status !== "ACTIVE" || result.receiptId !== row.receiptId || result.advertiserId !== row.advertiserId)
          throw new Error("Approval response could not be verified. Refresh before retrying.");
        identity.disabled = consent.disabled = true; approve.remove();
        if (result.identityProvisioning === "MEMBERSHIP_PROFILE_ENABLED") {
          add(card, "p", "Membership ACTIVE. The verified applicant can refresh their own dashboard. Other business APIs remain disabled.");
          status.textContent = "Membership approved; read-only profile access enabled for the verified applicant.";
        } else {
          add(card, "p", "Membership ACTIVE. Separate identity provisioning is still required for this account.");
          status.textContent = "Membership approved; workspace activation is not yet complete.";
        }
      } catch (error) { status.textContent = error.message; }
      finally { busy = false; refresh.disabled = false; if (approve.isConnected) sync(); }
    });
  }
  async function load() {
    if (busy) return;
    busy = true; refresh.disabled = true; csrf = null; queue.replaceChildren();
    status.textContent = "Checking operator permissions…";
    try {
      const sessionResponse = await fetch("/api/auth/session",{credentials:"same-origin",cache:"no-store"});
      if (!sessionResponse.ok) throw new Error("Session unavailable.");
      const session = await sessionResponse.json();
      if (!session.isAuthenticated || typeof session.csrfToken !== "string") throw new Error("Sign in with an authorized operator account.");
      const response = await fetch(endpoint,{credentials:"same-origin",cache:"no-store"});
      if (!response.ok) throw new Error("Operator review is unavailable or this account is not authorized.");
      const rows = await response.json(); if (!Array.isArray(rows)) throw new Error("Invalid queue response.");
      csrf = session.csrfToken; rows.forEach(render);
      status.textContent = rows.length ? "Showing up to 50 pending registrations." : "No pending registrations.";
    } catch (error) { csrf = null; queue.replaceChildren(); status.textContent = error.message; }
    finally { busy = false; refresh.disabled = false; }
  }
  const receiptRefresh = document.getElementById("starter-refresh");
  const receiptStatus = document.getElementById("starter-status");
  const receiptList = document.getElementById("starter-receipts");
  if (receiptRefresh && receiptStatus && receiptList) {
    receiptRefresh.addEventListener("click", async () => {
      if (receiptRefresh.disabled) return;
      receiptRefresh.disabled = true; receiptList.replaceChildren();
      receiptStatus.textContent = "Loading payment notifications…";
      try {
        const response = await fetch(endpoint + "/starter-receipts", {credentials:"same-origin",cache:"no-store"});
        if (!response.ok) throw new Error("Payment notifications unavailable or operator access not authorized.");
        const data = await response.json();
        if (!Array.isArray(data.receipts) || data.fulfillmentAuthorized !== false || data.receipts.length > 50)
          throw new Error("Receipt response could not be verified.");
        for (const row of data.receipts) {
          if (row.status !== "SIGNED_NOTIFICATION_PENDING_ORDER_RECONCILIATION") throw new Error("Unexpected receipt status.");
          const card = document.createElement("article"); receiptList.appendChild(card);
          add(card, "h3", row.quoteReference || "Unlinked payment — review required");
          add(card, "p", "Checkout: " + row.checkoutSessionId);
          add(card, "p", "Payment Intent: " + row.paymentIntentId);
          add(card, "p", "Amount: " + (row.amountCents / 100).toFixed(2) + " " + row.currency.toUpperCase());
          add(card, "p", "Received: " + row.receivedAt);
          add(card, "p", "Signed notification only. Pending merchant and order reconciliation.");
        }
        receiptStatus.textContent = data.receipts.length ? "Showing up to 50 recent notifications." : "No matching payment notifications recorded.";
      } catch (error) { receiptList.replaceChildren(); receiptStatus.textContent = error.message; }
      finally { receiptRefresh.disabled = false; }
    });
  }
  refresh.addEventListener("click", load); load();
})();