"use strict";
// AGM-LAUNCH-015 Stage H - browser-only interaction preview.
// Deliberately NO fetch, cookies, localStorage, sessionStorage or backend calls.
// Nothing here is an identity credential, legal consent record or authorization.
(() => {
  const checkbox = document.getElementById("opt-in");
  const accept = document.getElementById("accept");
  const withdraw = document.getElementById("withdraw");
  const status = document.getElementById("state");
  const timeline = document.getElementById("timeline");
  let previewState = "NONE";
  checkbox.checked = false;
  function render() {
    accept.disabled = previewState !== "NONE" || !checkbox.checked;
    withdraw.disabled = previewState !== "PREVIEW_OPT_IN";
    checkbox.disabled = previewState !== "NONE";
    status.textContent = previewState === "NONE"
      ? "No preview opt-in selected. Real visitor consent: NOT RECORDED."
      : previewState === "PREVIEW_OPT_IN"
        ? "Preview opt-in selected for this tab. Real visitor consent: NOT RECORDED."
        : "Preview permission withdrawn. Real visitor consent: NOT RECORDED.";
    timeline.textContent = previewState === "NONE"
      ? "No demonstration actions yet."
      : previewState === "PREVIEW_OPT_IN"
        ? "Demo event: CREATIVE_PREVIEW_ONLY · AGM-CONSENT-PREVIEW-V1 · PREVIEW_OPT_IN. No server record."
        : "Demo event: PREVIEW_WITHDRAWAL. Further permission requires a fresh, verified workflow, not reuse of this tab.";
  }
  checkbox.addEventListener("change",render);
  accept.addEventListener("click",() => {
    if (previewState !== "NONE" || !checkbox.checked) return;
    previewState = "PREVIEW_OPT_IN";
    checkbox.checked = false;
    render();
  });
  withdraw.addEventListener("click",() => {
    if (previewState !== "PREVIEW_OPT_IN") return;
    previewState = "PREVIEW_REVOKED";
    render();
  });
  render();
})();
