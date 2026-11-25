(() => {
  "use strict";
  const status = document.getElementById("review-status"), queue = document.getElementById("queue"), load = document.getElementById("load");
  const node = (tag, text) => { const e = document.createElement(tag); e.textContent = text; return e; };
  let busy = false;
  async function session() {
    const r = await fetch("/api/auth/session", { credentials: "same-origin", cache: "no-store" });
    if (!r.ok) throw Error("Operator session unavailable.");
    const s = await r.json();
    if (!s.isAuthenticated || !s.csrfToken) throw Error("Sign in as an approved operator.");
    return s;
  }
  async function refresh() {
    if (busy) return;
    busy = true; load.disabled = true; queue.replaceChildren();
    try {
      await session();
      const r = await fetch("/api/creators/operator-review", { credentials: "same-origin", cache: "no-store" });
      if (!r.ok) throw Error("Creator review unavailable or operator access denied.");
      const rows = await r.json();
      if (!Array.isArray(rows)) throw Error("Invalid queue response.");
      status.textContent = rows.length + " application(s) awaiting registration or channel review.";
      for (const item of rows) {
        const section = document.createElement("section"); section.className = "profile";
        for (const [label, value] of [["Creator", item.displayName], ["Country", item.countryCode], ["Submitted channel", item.channelUrl], ["Receipt", item.receiptId], ["Privacy", item.privacyNoticeVersion], ["Terms", item.termsVersion], ["Authority", item.authorityStatus]]) section.append(node("p", label + ": " + String(value ?? "")));
        const check = label => { const wrap = node("label", label); const input = document.createElement("input"); input.type = "checkbox"; wrap.prepend(input); section.append(wrap, document.createElement("br")); return input; };
        if (item.status === "ACTIVE" && item.authorityStatus === "UNVERIFIED") {
          section.append(node("p", "Open the exact submitted channel on the legitimate platform. Independently observe the current ALPHA proof in the channel description. Do not accept a screenshot, password, or proof on another channel."));
          const label = node("label", "Observed ALPHA channel proof"); const code = document.createElement("input");
          code.type = "text"; code.maxLength = 38; code.autocomplete = "off"; label.append(code); section.append(label, document.createElement("br"));
          const control = check("I independently observed the current proof on the exact submitted channel and checked for duplicate channel claims.");
          const consent = check("I reviewed the current recorded privacy and terms consent.");
          const verify = node("button", "Verify channel control"); verify.type = "button"; verify.disabled = true;
          const update = () => { verify.disabled = !control.checked || !consent.checked || !/^ALPHA-[0-9A-F]{32}$/.test(code.value.trim()) || item.processingConsent !== true || item.termsAccepted !== true; };
          for (const element of [control, consent]) element.addEventListener("change", update);
          code.addEventListener("input", update);
          verify.addEventListener("click", async () => {
            if (busy || verify.disabled) return;
            busy = true; load.disabled = true; verify.disabled = true;
            try {
              const s = await session();
              const r = await fetch("/api/creators/operator-review/verify-channel", { method: "POST", credentials: "same-origin",
                headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": s.csrfToken },
                body: JSON.stringify({ receiptId: item.receiptId, creatorId: item.creatorId, observedCode: code.value.trim(), channelControlReviewed: control.checked, consentReviewed: consent.checked }) });
              if (!r.ok) throw Error("Verification refused. Check the exact channel, current proof and approval status.");
              const result = await r.json();
              if (result.authorityStatus !== "VERIFIED" || result.workspaceAuthorized !== true || result.commercialAuthorized !== false) throw Error("Unexpected verification result. Refresh.");
              section.remove(); status.textContent = "Channel control verified. Private profile is enabled; commercial rights remain unavailable.";
            } catch (e) { status.textContent = e.message; update(); }
            finally { busy = false; load.disabled = false; }
          });
          section.append(verify); queue.append(section); continue;
        }
        const profile = check("I have manually reviewed this creator profile.");
        const consent = check("I have reviewed the recorded required consent and terms.");
        const approve = node("button", "Approve registration only"); approve.type = "button"; approve.disabled = true;
        const update = () => { approve.disabled = !profile.checked || !consent.checked || item.processingConsent !== true || item.termsAccepted !== true; };
        profile.addEventListener("change", update); consent.addEventListener("change", update);
        approve.addEventListener("click", async () => {
          if (busy || approve.disabled) return;
          busy = true; load.disabled = true; approve.disabled = true;
          try {
            const s = await session();
            const r = await fetch("/api/creators/operator-review", { method: "POST", credentials: "same-origin",
              headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": s.csrfToken },
              body: JSON.stringify({ receiptId: item.receiptId, creatorId: item.creatorId, profileReviewed: profile.checked, consentReviewed: consent.checked }) });
            if (!r.ok) throw Error("Approval refused. Refresh and review the exact receipt.");
            const result = await r.json();
            if (result.status !== "ACTIVE" || result.workspaceAuthorized !== false) throw Error("Unexpected review result. Refresh.");
            section.remove(); status.textContent = "Registration approved. Channel verification and workspace access remain separate.";
          } catch (e) { status.textContent = e.message; update(); }
          finally { busy = false; load.disabled = false; }
        });
        section.append(approve); queue.append(section);
      }
    } catch (e) { status.textContent = e.message; }
    finally { busy = false; load.disabled = false; }
  }
  load.addEventListener("click", refresh);
})();
