"use strict";
// COM-261-004: deliberately local-only and fictional-only.
// NO fetch, XHR, navigator.sendBeacon, storage, cookie writes or form POST.
// This module cannot issue consent receipts or submit registration data.
(function () {
  const allowedCategories = new Set(["podcast", "livestream", "video", "other"]);
  function validateCreatorPreview(raw) {
    const data = {
      creatorName: String(raw.creatorName || "").trim(),
      email: String(raw.email || "").trim(),
      channelUrl: String(raw.channelUrl || "").trim(),
      market: String(raw.market || "").trim(),
      category: String(raw.category || "").trim(),
      processingConsent: raw.processingConsent === true
    };
    const errors = [];
    if (!/^Synthetic [A-Za-z][A-Za-z0-9 ._-]{0,109}$/.test(data.creatorName)) {
      errors.push("Use a fictional display name beginning with Synthetic.");
    }
    if (!/^[A-Za-z0-9._%+-]+@example\.invalid$/i.test(data.email) || data.email.length > 254) {
      errors.push("Use an example.invalid fictional email only.");
    }
    if (!/^https:\/\/example\.invalid\/[-/a-z0-9._~%]+$/i.test(data.channelUrl) || data.channelUrl.length > 300) {
      errors.push("Use an HTTPS example.invalid fictional channel URL only.");
    }
    if (!/^Test [A-Za-z][A-Za-z ]{0,94}$/.test(data.market)) {
      errors.push("Use a fictional market beginning with Test.");
    }
    if (!allowedCategories.has(data.category)) {
      errors.push("Select a supported creator content category.");
    }
    if (!data.processingConsent) {
      errors.push("Check the required preview acknowledgement to test validation.");
    }
    return { ok: errors.length === 0, errors };
  }
  if (typeof module !== "undefined" && module.exports) {
    module.exports = { validateCreatorPreview };
  }
  if (typeof document === "undefined") return;
  const form = document.getElementById("creator-preview-form");
  const status = document.getElementById("result");
  if (!form || !status) return;
  const processing = document.getElementById("processing-consent");
  const marketing = document.getElementById("marketing-consent");
  // Ignore browser-restored checkbox state. Every load begins unchecked.
  processing.checked = false;
  marketing.checked = false;
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    const values = {
      creatorName: form.elements.creatorName.value,
      email: form.elements.email.value,
      channelUrl: form.elements.channelUrl.value,
      market: form.elements.market.value,
      category: form.elements.category.value,
      processingConsent: processing.checked
    };
    const result = validateCreatorPreview(values);
    status.textContent = result.ok
      ? "Fictional fields and preview acknowledgement validated locally. Registration: DISABLED. Database receipt: NONE. No data sent or stored."
      : "NOT VALIDATED:\n" + result.errors.join("\n") + "\nRegistration: DISABLED. Database receipt: NONE.";
  });
})();
