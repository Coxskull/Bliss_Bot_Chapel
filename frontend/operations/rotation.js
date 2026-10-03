async function loadRotation() {
  const board = await api("/api/operations/rotation");
  $("#rotation-notice").textContent = "A later period stores the measured open slots and the creator decision. It does not fill a theoretical slot. No revenue row is on file. This is not a census. Economics remains the only price authority. Green does not send. Delivery remains NOT_SENT.";
  $("#rotation-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Stored advertisers ${board.advertiserCount ?? 0}. Stored slots ${board.slotCount ?? 0}. ${board.revenueLine || ""}`;
  const names = board.advertisers || [];
  $("#rotation-advertisers").innerHTML = names.length
    ? `<table><thead><tr><th>Stored advertiser</th></tr></thead><tbody>${names.map(name =>
        `<tr><td>${escapeHtml(name)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No stored advertiser is on file. None was invented.");
  const history = board.history || [];
  $("#rotation-history").innerHTML = history.length
    ? `<table><thead><tr><th>Period</th><th>Theoretical</th><th>Placed</th><th>Open</th><th>Stored slots</th><th>Creator</th><th>Revenue</th><th>Census</th><th>Slots changed</th><th>Delivery</th></tr></thead><tbody>${history.map(item =>
        `<tr><td>${escapeHtml(item.periodKey)}</td><td>${escapeHtml(String(item.theoreticalSlots))}</td><td>${escapeHtml(String(item.placedAdvertisers))}</td><td>${escapeHtml(String(item.openSlots))}</td><td>${escapeHtml(String(item.slotCount))}</td><td>${item.creatorApproved ? "Approved" : "Withheld"}</td><td>${escapeHtml(item.revenueLine)}</td><td>${item.censusClaimed ? "Yes" : "No"}</td><td>${item.slotsChanged ? "Yes" : "No"}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No later period is stored. Open slots were not filled.");
  return board;
}

async function storePeriod(periodKey, creatorApproved) {
  const decision = await api("/api/operations/rotation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ pair: 6, creatorApproved, periodKey })
  });
  $("#rotation-result").textContent = decision.duplicate
    ? "That later period is already stored. The open slots were not filled. Delivery remains NOT_SENT."
    : (decision.notice || "Delivery remains NOT_SENT.");
  await loadRotation();
}

document.addEventListener("DOMContentLoaded", () => {
  const store = document.getElementById("rotation-store");
  const withhold = document.getElementById("rotation-withhold");
  if (store) {
    store.addEventListener("click", async () => {
      store.disabled = true;
      try {
        await storePeriod("later-period-1", true);
      } catch (error) {
        $("#rotation-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        store.disabled = false;
      }
    });
  }
  if (withhold) {
    withhold.addEventListener("click", async () => {
      withhold.disabled = true;
      try {
        await storePeriod("later-period-withheld", false);
      } catch (error) {
        $("#rotation-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        withhold.disabled = false;
      }
    });
  }
});
