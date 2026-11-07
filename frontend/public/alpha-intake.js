(() => {
  "use strict";
  const form = document.querySelector("#alpha-intake-form");
  const status = document.querySelector("#form-status");
  const submit = document.querySelector("#submit-button");
  if (!form || !status || !submit) return;

  const noticeVersion = form?.dataset.kind === "support"
    ? "AGM-SUPPORT-2026-11-08" : "AGM-INTEREST-2026-11-05-GHL";
  let pendingKey = null;
  let pendingDraft = null;
  let saved = false;
  const field = name => form.elements.namedItem(name);
  const value = name => String(field(name)?.value ?? "").trim();
  const checked = name => Boolean(field(name)?.checked);
  const show = (message, type) => {
    status.hidden = false;
    status.textContent = message;
    status.className = "status " + type;
    status.focus();
  };
  form.addEventListener("input", () => {
    if (saved) return;
    // Edited drafts are new intents, rather than replay of old parameters.
    pendingKey = null;
    pendingDraft = null;
  });

  form.addEventListener("submit", async e => {
    e.preventDefault();
    if (saved) return;
    if (!form.reportValidity()) return;
    if (!window.crypto?.randomUUID || !window.fetch) {
      show("Your browser cannot securely submit this form. Please use an updated browser.", "error");
      return;
    }
    const kind = form.dataset.kind;
    const draft = {
      kind,
      full_name: value("full_name"),
      email: value("email"),
      organization: value("organization"),
      website: value("website"),
      market: value("market"),
      category: value("category"),
      description: value("description"),
      middle_name: value("middle_name"),
      processing_acknowledged: checked("processing_acknowledged"),
      adult_confirmed: checked("adult_confirmed"),
      marketing_opt_in: checked("marketing_opt_in"),
      notice_version: noticeVersion
    };
    if (!draft.processing_acknowledged || !draft.adult_confirmed) {
      show("Please confirm your age and read the Privacy Notice before submitting.", "error");
      return;
    }
    if (JSON.stringify(draft) !== pendingDraft || !pendingKey) {
      pendingKey = crypto.randomUUID();
      pendingDraft = JSON.stringify(draft);
    }
    const endpoint = form.dataset.endpoint;
    if (!endpoint || !endpoint.startsWith("https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake")) {
      show("The verified submission service is unavailable.", "error");
      return;
    }
    submit.disabled = true;
    submit.textContent = "Submitting…";
    status.hidden = true;
    try {
      const response = await fetch(endpoint, {
        method: "POST",
        mode: "cors",
        credentials: "omit",
        cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...draft, request_key: pendingKey }),
        signal: AbortSignal.timeout(15000)
      });
      const result = await response.json();
      if (response.status === 201 &&
          result?.status === "RECEIVED_NOT_YET_VERIFIED" &&
          /^[0-9a-f-]{36}$/i.test(result.receipt_id ?? "") &&
          (kind !== "support" || (/^[0-9a-f-]{36}$/i.test(result.case_id ?? "") &&
                                  result.case_status === "OPEN"))) {
        saved = true;
        form.querySelectorAll("input,textarea,select,button").forEach(el => { el.disabled = true; });
        show(kind === "support" && /^[0-9a-f-]{36}$/i.test(result.case_id ?? "")
          ? "Your support case was recorded. Case: " + result.case_id + ". Receipt: " +
            result.receipt_id + ". Status: " + result.case_status +
            ". A human response is not yet confirmed. Keep both references."
          : "Your interest request has been saved. Receipt: " + result.receipt_id +
            ". Status: NOT YET VERIFIED. Please keep this receipt for your records.", "success");
      } else {
        show(result?.error || "Your form was not confirmed. Please retry.", "error");
      }
    } catch {
      show("We could not verify a saved submission. Please retry from this page; the same request key will be reused.", "error");
    } finally {
      if (!saved) {
        submit.disabled = false;
        submit.textContent = kind === "privacy" ? "Submit privacy request →"
          : kind === "support" ? "Submit support case →" : "Submit interest →";
      }
    }
  });
})();
