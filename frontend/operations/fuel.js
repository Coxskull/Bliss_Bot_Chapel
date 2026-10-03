async function loadFuel() {
  const board = await api("/api/operations/fuel");
  $("#fuel-notice").textContent = board.notice || "";
  $("#fuel-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Green does not send.`;
  const fuel = board.fuel || {};
  const facts = [
    ["Fuel", fuel.fuelStatus || ""],
    ["Qualified unique", fuel.qualifiedUnique],
    ["Duplicates", fuel.duplicates],
    ["Replacement", fuel.replacementRequired],
    ["Daily remaining", fuel.dailyRemaining]
  ];
  $("#fuel-facts").innerHTML = `<table><thead><tr><th>Reading</th><th>Stored value</th></tr></thead><tbody>${facts.map(([label, value]) =>
    `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(String(value))}</td></tr>`).join("")}</tbody></table>`;
  const markets = board.markets || [];
  $("#fuel-markets").innerHTML = markets.length
    ? `<table><thead><tr><th>Market</th><th>Country</th><th>Qualified slices</th><th>Notice</th></tr></thead><tbody>${markets.map(item =>
        `<tr><td>${escapeHtml(item.market)}</td><td>${escapeHtml(item.countryLine)}</td><td>${escapeHtml(String(item.qualifiedSlices))}</td><td>${escapeHtml(item.notice)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No stored market is covered. None was invented. This is not a census.");
  const withheld = board.withheld || [];
  $("#fuel-withheld").innerHTML = withheld.length
    ? `<table><thead><tr><th>Reason</th><th>Count</th></tr></thead><tbody>${withheld.map(item =>
        `<tr><td>${escapeHtml(item.reason)}</td><td>${escapeHtml(String(item.count))}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No qualified slice was withheld. A missing market was not invented.");
}
