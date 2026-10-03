async function loadMetrics() {
  const board = await api("/api/operations/metrics");
  $("#metrics-notice").textContent = "A marketplace reading stores the measured advertiser count, the measured creator count, and the stored slot count. A revenue amount is not on file. This is not a census. The balance page is unchanged. Green does not send. Delivery remains NOT_SENT.";
  $("#metrics-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Advertisers ${board.advertiserCount ?? 0}. Creators ${board.creatorCount ?? 0}. Slots ${board.slotCount ?? 0}.`;
  $("#metrics-pressure").textContent = `${board.pressure || ""} ${board.revenueLine || ""} Balance page: ${board.balanceRevenue || ""} ${board.balanceInventory || ""}`;
  const advertisers = board.advertisers || [];
  const creators = board.creators || [];
  const names = advertisers.map(name => `<tr><td>Advertiser</td><td>${escapeHtml(name)}</td></tr>`).concat(
    creators.map(name => `<tr><td>Creator</td><td>${escapeHtml(name)}</td></tr>`));
  $("#metrics-names").innerHTML = names.length
    ? `<table><thead><tr><th>Side</th><th>Stored name</th></tr></thead><tbody>${names.join("")}</tbody></table>`
    : emptyState("No stored name is on file. None was invented.");
  const history = board.history || [];
  $("#metrics-history").innerHTML = history.length
    ? `<table><thead><tr><th>Reading</th><th>Advertisers</th><th>Creators</th><th>Slots</th><th>Revenue rows</th><th>Pressure</th><th>Census</th><th>Revenue recorded</th><th>Slots changed</th><th>Delivery</th></tr></thead><tbody>${history.map(item =>
        `<tr><td>${escapeHtml(item.metricKey)}</td><td>${escapeHtml(String(item.advertiserCount))}</td><td>${escapeHtml(String(item.creatorCount))}</td><td>${escapeHtml(String(item.slotCount))}</td><td>${escapeHtml(String(item.revenueRowCount))}</td><td>${escapeHtml(item.pressure)}</td><td>${item.censusClaimed ? "Yes" : "No"}</td><td>${item.revenueRecorded ? "Yes" : "No"}</td><td>${item.slotsChanged ? "Yes" : "No"}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No marketplace reading is stored. A revenue amount was not added.");
  return board;
}

async function storeMetrics(revenueAmount, addSlot) {
  const body = { metricKey: "metrics-reading-1", addSlot };
  if (revenueAmount !== null && revenueAmount !== undefined) body.revenueAmount = revenueAmount;
  const decision = await api("/api/operations/metrics", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  $("#metrics-result").textContent = decision.duplicate
    ? "That marketplace reading is already stored. No revenue row was added. Delivery remains NOT_SENT."
    : (decision.notice || "Delivery remains NOT_SENT.");
  await loadMetrics();
}

document.addEventListener("DOMContentLoaded", () => {
  const store = document.getElementById("metrics-store");
  const revenue = document.getElementById("metrics-revenue");
  if (store) {
    store.addEventListener("click", async () => {
      store.disabled = true;
      try {
        await storeMetrics(null, false);
      } catch (error) {
        $("#metrics-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        store.disabled = false;
      }
    });
  }
  if (revenue) {
    revenue.addEventListener("click", async () => {
      revenue.disabled = true;
      try {
        await storeMetrics(1, false);
      } catch (error) {
        $("#metrics-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        revenue.disabled = false;
      }
    });
  }
});
