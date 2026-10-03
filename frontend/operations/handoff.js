async function loadHandoff(tenant) {
  const query = tenant ? `?tenant=${encodeURIComponent(tenant)}` : "";
  const board = await api(`/api/operations/handoff${query}`);
  $("#handoff-notice").textContent = "A qualified advertiser and a stored creator are handed to DeterministicRuleEvaluator. The handoff does not open a match certificate. A preserved advertiser is not handed off. A duplicate pair is not a second row. Another tenant's handoff is not shown. This is not a win. Green does not send. Delivery remains NOT_SENT.";
  $("#handoff-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Green does not send. The rule is ${board.ruleName || "not recorded"}.`;
  const advertisers = board.advertisers || [];
  const selected = board.tenant || tenant || advertisers.find(item => item.qualified)?.tenantKey || "";
  if (!board.tenant && selected && selected !== tenant) {
    return loadHandoff(selected);
  }
  const advertiser = document.getElementById("handoff-advertiser");
  if (advertiser) {
    const qualified = advertisers.filter(item => item.qualified);
    advertiser.innerHTML = qualified.map(item =>
      `<option value="${escapeHtml(item.tenantKey)}">${escapeHtml(item.businessName)}</option>`).join("");
    if (selected) advertiser.value = selected;
  }
  const creator = document.getElementById("handoff-creator");
  if (creator && creator.options.length === 0) {
    creator.innerHTML = (board.creators || []).map(item =>
      `<option value="${escapeHtml(item.id)}">${escapeHtml(item.name)} · ${escapeHtml(item.countryCode || "country unrecorded")}</option>`).join("");
  }
  const withheld = advertisers.filter(item => !item.qualified);
  $("#handoff-withheld").innerHTML = withheld.length
    ? `<table><thead><tr><th>Advertiser</th><th>Score</th><th>State</th><th>Handoff</th></tr></thead><tbody>${withheld.map(item =>
        `<tr><td>${escapeHtml(item.businessName)}</td><td>${escapeHtml(String(item.score))}</td><td>${escapeHtml(item.state || "unrecorded")}</td><td>Not qualified. The evaluator was not called.</td></tr>`).join("")}</tbody></table>`
    : emptyState("Every stored advertiser on this page is qualified. None was invented.");
  const rows = board.handoffs || [];
  $("#handoff-rows").innerHTML = rows.length
    ? `<table><thead><tr><th>Advertiser</th><th>Source</th><th>Creator</th><th>Rule</th><th>Evaluator</th><th>Status</th><th>Win</th><th>Delivery</th></tr></thead><tbody>${rows.map(item =>
        `<tr><td>${escapeHtml(item.businessName)}</td><td>${escapeHtml(item.sourceUrl)}</td><td>${escapeHtml(item.creatorName)}</td><td>${escapeHtml(item.ruleName)}</td><td>${item.evaluatorInvoked ? "DeterministicRuleEvaluator" : "Not called"}</td><td>${escapeHtml(item.matchStatus)}</td><td>${item.winClaimed ? "Yes" : "No"}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No handoff is recorded for this advertiser.");
  return board;
}

document.addEventListener("DOMContentLoaded", () => {
  const button = document.getElementById("handoff-submit");
  const advertiser = document.getElementById("handoff-advertiser");
  if (advertiser) {
    advertiser.addEventListener("change", async () => {
      $("#handoff-result").textContent = "";
      try {
        await loadHandoff(advertiser.value);
      } catch (error) {
        toast(error.message, true);
      }
    });
  }
  if (button) {
    button.addEventListener("click", async () => {
      const tenant = document.getElementById("handoff-advertiser")?.value || "";
      const creatorId = document.getElementById("handoff-creator")?.value || "";
      button.disabled = true;
      try {
        const decision = await api("/api/operations/handoff", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ tenant, creatorId })
        });
        $("#handoff-result").textContent = decision.duplicate
          ? "That handoff is already recorded. The accepted evaluator was not called again. Delivery remains NOT_SENT."
          : (decision.notice || "Delivery remains NOT_SENT.");
        await loadHandoff(tenant);
      } catch (error) {
        $("#handoff-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        button.disabled = false;
      }
    });
  }
});
