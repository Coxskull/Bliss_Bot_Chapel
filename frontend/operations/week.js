async function loadWeek() {
  const board = await api("/api/operations/week");
  $("#week-notice").textContent = "A calendar week stores the measured coverage. It does not add a market. A missing market stays unlisted. This is not a census. The fuel gauge is unchanged. Green does not send. Delivery remains NOT_SENT.";
  $("#week-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Qualified slices ${board.qualifiedSlices ?? 0}. Fuel ${board.fuelStatus || ""}.`;
  const markets = board.markets || [];
  $("#week-markets").innerHTML = markets.length
    ? `<table><thead><tr><th>Stored market</th><th>Country</th><th>Qualified slices</th></tr></thead><tbody>${markets.map(item =>
        `<tr><td>${escapeHtml(item.market)}</td><td>${escapeHtml(item.countryLine)}</td><td>${escapeHtml(String(item.qualifiedSlices))}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No stored market is on file. None was invented.");
  const history = board.history || [];
  $("#week-history").innerHTML = history.length
    ? `<table><thead><tr><th>Week</th><th>Qualified</th><th>Markets</th><th>Stored markets</th><th>Fuel</th><th>Census</th><th>Slices changed</th><th>Delivery</th></tr></thead><tbody>${history.map(item =>
        `<tr><td>${escapeHtml(item.weekKey)}</td><td>${escapeHtml(String(item.qualifiedSlices))}</td><td>${escapeHtml(String(item.marketCount))}</td><td>${escapeHtml(item.marketLine || "")}</td><td>${escapeHtml(item.fuelStatus)}</td><td>${item.censusClaimed ? "Yes" : "No"}</td><td>${item.slicesChanged ? "Yes" : "No"}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No coverage week is stored. A missing market was not added.");
  return board;
}

async function storeWeek(addMissingMarket) {
  const decision = await api("/api/operations/week", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ weekKey: "coverage-week-1", addMissingMarket })
  });
  $("#week-result").textContent = decision.duplicate
    ? "That coverage week is already stored. No market was added. Delivery remains NOT_SENT."
    : (decision.notice || "Delivery remains NOT_SENT.");
  await loadWeek();
}

document.addEventListener("DOMContentLoaded", () => {
  const store = document.getElementById("week-store");
  const add = document.getElementById("week-add");
  if (store) {
    store.addEventListener("click", async () => {
      store.disabled = true;
      try {
        await storeWeek(false);
      } catch (error) {
        $("#week-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        store.disabled = false;
      }
    });
  }
  if (add) {
    add.addEventListener("click", async () => {
      add.disabled = true;
      try {
        await storeWeek(true);
      } catch (error) {
        $("#week-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        add.disabled = false;
      }
    });
  }
});
