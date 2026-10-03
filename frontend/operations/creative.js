async function loadCreative(workspaceId) {
  const query = workspaceId ? `?workspaceId=${encodeURIComponent(workspaceId)}` : "";
  const board = await api(`/api/operations/creative${query}`);
  $("#creative-notice").textContent = "A human records one creative decision inside an open workspace. A discovered business is not opened. Campaign ready is refused. A price is not invented. Six roles are not called. A match is not written. Green does not send. Delivery remains NOT_SENT.";
  $("#creative-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Model calls ${board.modelCalls ?? 0}. Campaign ready ${board.campaignReady ? "yes" : "no"}.`;
  const workspaces = board.workspaces || [];
  const selected = board.workspaceId || workspaceId || workspaces[0]?.id || "";
  if (!board.workspaceId && selected && selected !== workspaceId) {
    return loadCreative(selected);
  }
  const select = document.getElementById("creative-workspace");
  if (select) {
    select.innerHTML = workspaces.map(item =>
      `<option value="${escapeHtml(item.id)}">${escapeHtml(item.advertiserName)}</option>`).join("");
    if (selected) select.value = selected;
  }
  const decisions = board.decisions || [];
  $("#creative-rows").innerHTML = decisions.length
    ? `<table><thead><tr><th>Title</th><th>Status</th><th>Actor</th><th>Campaign ready</th><th>Match written</th><th>Price invented</th><th>Model calls</th><th>Delivery</th></tr></thead><tbody>${decisions.map(item =>
        `<tr><td>${escapeHtml(item.title)}</td><td>${escapeHtml(item.status)}</td><td>${escapeHtml(item.actorType)}</td><td>${item.campaignReady ? "Yes" : "No"}</td><td>${item.matchWritten ? "Yes" : "No"}</td><td>${item.priceInvented ? "Yes" : "No"}</td><td>${escapeHtml(String(item.modelCalls))}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No creative decision is recorded for this workspace.");
  const messages = board.messages || [];
  $("#creative-messages").innerHTML = messages.length
    ? `<table><thead><tr><th>Actor</th><th>Message in this workspace</th></tr></thead><tbody>${messages.map(item =>
        `<tr><td>${escapeHtml(item.actorType)}</td><td>${escapeHtml(item.body)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No conversation is stored for this workspace.");
  const audits = board.audits || [];
  $("#creative-audits").innerHTML = audits.length
    ? `<table><thead><tr><th>Audit</th><th>Actor</th><th>Outcome</th></tr></thead><tbody>${audits.map(item =>
        `<tr><td>${escapeHtml(item.action)}</td><td>${escapeHtml(item.actorType)}</td><td>${escapeHtml(item.outcome)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No creative audit is stored for this workspace.");
  return board;
}

document.addEventListener("DOMContentLoaded", () => {
  const select = document.getElementById("creative-workspace");
  const button = document.getElementById("creative-approve");
  if (select) {
    select.addEventListener("change", async () => {
      $("#creative-result").textContent = "";
      try {
        await loadCreative(select.value);
      } catch (error) {
        toast(error.message, true);
      }
    });
  }
  if (button) {
    button.addEventListener("click", async () => {
      const workspaceId = document.getElementById("creative-workspace")?.value || "";
      const title = document.getElementById("creative-title")?.value || "";
      button.disabled = true;
      try {
        const decision = await api("/api/operations/creative", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            workspaceId,
            title,
            decision: "APPROVE",
            actorType: "OPERATOR",
            idempotencyKey: "creative-table-card"
          })
        });
        $("#creative-result").textContent = decision.duplicate
          ? "That creative decision is already recorded. The human was not asked again. Delivery remains NOT_SENT."
          : (decision.notice || "Delivery remains NOT_SENT.");
        await loadCreative(workspaceId);
      } catch (error) {
        $("#creative-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        button.disabled = false;
      }
    });
  }
});
