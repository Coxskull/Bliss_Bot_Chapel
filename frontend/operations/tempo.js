async function loadLaneTempo() {
  const board = await api("/api/operations/tempo");
  $("#tempo-notice").textContent = board.notice || "";
  const rows = (board.lanes || []).map(lane => {
    const ceiling = lane.ceilingAmount == null
      ? "No spend ceiling is recorded. None was invented."
      : `${lane.ceilingAmount} ${lane.ceilingCurrency}. This is not an Economics price.`;
    return `<tr><td>${escapeHtml(lane.displayName)}</td><td>${escapeHtml(lane.tempo)}</td><td>${escapeHtml(ceiling)}</td><td>${escapeHtml(lane.notice)}</td></tr>`;
  }).join("");
  $("#tempo-lanes").innerHTML = `<table><thead><tr><th>Lane</th><th>Tempo</th><th>Ceiling</th><th>Notice</th></tr></thead><tbody>${rows}</tbody></table>`;
  const audits = board.audits || [];
  $("#tempo-audits").innerHTML = audits.length
    ? `<table><thead><tr><th>Lane</th><th>Tempo</th><th>Reason</th><th>Notice</th></tr></thead><tbody>${audits.map(item => `<tr><td>${escapeHtml(item.lane)}</td><td>${escapeHtml(item.tempo)}</td><td>${escapeHtml(item.reason)}</td><td>${escapeHtml(item.notice)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No lane tempo has been recorded. None was invented. Delivery remains NOT_SENT.");
}

document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("tempo-form");
  if (!form) return;
  form.addEventListener("submit", async event => {
    event.preventDefault();
    const data = new FormData(form);
    const body = {
      lane: data.get("lane"),
      tempo: data.get("tempo"),
      reason: String(data.get("reason") || "").trim()
    };
    const amount = String(data.get("ceilingAmount") || "").trim();
    const currency = String(data.get("ceilingCurrency") || "").trim();
    if (amount) body.ceilingAmount = Number(amount);
    if (currency) body.ceilingCurrency = currency;
    const submit = form.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      const board = await api("/api/operations/tempo", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
      const lane = (board.lanes || []).find(item => item.lane === String(body.lane).toUpperCase());
      $("#tempo-result").textContent = lane ? lane.notice : board.notice;
      await loadLaneTempo();
      toast("Lane tempo recorded. Delivery remains NOT_SENT.");
    } catch (error) {
      $("#tempo-result").textContent = error.message;
      toast(error.message, true);
    } finally {
      submit.disabled = false;
    }
  });
});
