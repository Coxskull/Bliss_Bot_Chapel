(() => {
  "use strict";
  const form = document.querySelector("#alpha-intake-form");
  const status = document.querySelector("#form-status");
  const submit = document.querySelector("#submit-button");
  if (!form || !status || !submit) return;

  const noticeVersion = "AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1";
  let pendingKey = null;
  let pendingDraft = null;
  let saved = false;
  const field = name => form.elements.namedItem(name);
  const value = name => String(field(name)?.value ?? "").trim();
  const checked = name => Boolean(field(name)?.checked);
  const checkedValues = name => [...form.querySelectorAll('input[name="' + name + '"]:checked')].map(el => el.value);
  const csvValues = name => value(name).split(",").map(v => v.trim()).filter(Boolean);
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
      contact_consent: checked("contact_consent"),
      notice_version: noticeVersion
    };
    if (!draft.processing_acknowledged || !draft.adult_confirmed ||
        (kind !== "privacy" && !draft.contact_consent)) {
      show("Please confirm your age, review the Privacy Notice, and choose whether ALPHA may follow up about this inquiry.", "error");
      return;
    }
    if (kind === "advertiser") {
      draft.budget_amount = value("budget_amount");
      draft.budget_currency = value("budget_currency");
      draft.campaign_objective = value("campaign_objective");
      draft.preferred_media_formats = checkedValues("preferred_media_formats");
      draft.audience_requirements = value("audience_requirements");
      draft.preferred_languages = csvValues("preferred_languages");
      if (draft.preferred_media_formats.length === 0) {
        show("Choose at least one media format you would consider.", "error");
        return;
      }
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
          /^[0-9a-f-]{36}$/i.test(result.receipt_id ?? "")) {
        saved = true;
        form.querySelectorAll("input,textarea,select,button").forEach(el => { el.disabled = true; });
        show("Your interest request has been saved. Receipt: " + result.receipt_id +
          ". Status: NOT YET VERIFIED. Please keep this receipt for your records.", "success");
      } else {
        show(result?.error || "Your form was not confirmed. Please retry.", "error");
      }
    } catch {
      show("We could not verify a saved submission. Please retry from this page; the same request key will be reused.", "error");
    } finally {
      if (!saved) {
        submit.disabled = false;
        submit.textContent = kind === "privacy" ? "Submit privacy request →" : "Submit interest →";
      }
    }
  });
})();
