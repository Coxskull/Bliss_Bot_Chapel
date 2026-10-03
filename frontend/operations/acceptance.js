async function loadAcceptance() {
  const board = await api("/api/operations/acceptance");
  const awaiting = board.accepted
    ? ""
    : "Economics Phase 9 awaits the owner acceptance. ";
  $("#acceptance-notice").textContent = board.accepted
    ? (board.notice || "")
    : `${awaiting}A pricing rule is not changed. A settlement is not created. An empty history stays unrecorded. Economics remains the only price authority. Green does not send. Delivery remains NOT_SENT.`;
  $("#acceptance-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Historical placements ${board.placementCount ?? 0}. Campaign snapshots ${board.campaignCount ?? 0}. Recommendation rewritten ${board.recommendationRewritten ? "Yes" : "No"}.`;
  $("#acceptance-history").textContent = board.historyLine || "No historical actual is on file. None was invented.";
  const rows = board.accepted
    ? `<table><thead><tr><th>Phase</th><th>Placements</th><th>Campaigns</th><th>History</th><th>Repricing</th><th>Settlement</th><th>Recommendation rewritten</th><th>Delivery</th></tr></thead><tbody><tr><td>Phase ${escapeHtml(String(board.phase))}</td><td>${escapeHtml(String(board.placementCount))}</td><td>${escapeHtml(String(board.campaignCount))}</td><td>${escapeHtml(board.historyLine || "")}</td><td>${board.repricingAuthorized ? "Yes" : "No"}</td><td>${board.settlementAuthorized ? "Yes" : "No"}</td><td>${board.recommendationRewritten ? "Yes" : "No"}</td><td>${escapeHtml(board.delivery || "NOT_SENT")}</td></tr></tbody></table>`
    : emptyState("Economics Phase 9 is not accepted yet. No historical amount was invented.");
  $("#acceptance-rows").innerHTML = rows;
  return board;
}

async function postAcceptance(reprice) {
  const decision = await api("/api/operations/acceptance", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ phase: 9, reprice, settle: false })
  });
  $("#acceptance-result").textContent = decision.duplicate
    ? "Economics Phase 9 is already accepted. A pricing rule was not changed. Delivery remains NOT_SENT."
    : (decision.notice || "Delivery remains NOT_SENT.");
  await loadAcceptance();
}

document.addEventListener("DOMContentLoaded", () => {
  const accept = document.getElementById("acceptance-record");
  const reprice = document.getElementById("acceptance-reprice");
  if (accept) {
    accept.addEventListener("click", async () => {
      accept.disabled = true;
      try {
        await postAcceptance(false);
      } catch (error) {
        $("#acceptance-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        accept.disabled = false;
      }
    });
  }
  if (reprice) {
    reprice.addEventListener("click", async () => {
      reprice.disabled = true;
      try {
        await postAcceptance(true);
      } catch (error) {
        $("#acceptance-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        reprice.disabled = false;
      }
    });
  }
});
