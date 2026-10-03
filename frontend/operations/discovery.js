async function loadDiscovery() {
  const board = await api("/api/operations/discovery");
  $("#discovery-notice").textContent = board.notice || "";
  $("#discovery-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Green does not send.`;
  const facts = [
    ["Stored prospects", board.stored],
    ["Scored 100", board.scored],
    ["Preserved", board.preserved],
    ["Census claimed", board.censusClaimed ? "Yes" : "No. This is not a census."]
  ];
  $("#discovery-facts").innerHTML = `<table><thead><tr><th>Reading</th><th>Stored value</th></tr></thead><tbody>${facts.map(([label, value]) =>
    `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(String(value))}</td></tr>`).join("")}</tbody></table>`;
  const prospects = board.prospects || [];
  $("#discovery-prospects").innerHTML = prospects.length
    ? `<table><thead><tr><th>Business</th><th>Market</th><th>Score</th><th>State</th><th>Public source</th><th>Notice</th></tr></thead><tbody>${prospects.map(item =>
        `<tr><td>${escapeHtml(item.businessName)}</td><td>${escapeHtml(item.market)}</td><td>${escapeHtml(String(item.score))}</td><td>${escapeHtml(item.state)}</td><td>${escapeHtml(item.sourceUrl)}</td><td>${escapeHtml(item.notice)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No stored public-source prospect is listed. None was invented. This is not a census.");
  const withheld = board.withheld || [];
  $("#discovery-withheld").innerHTML = withheld.length
    ? `<table><thead><tr><th>Reason</th><th>Count</th></tr></thead><tbody>${withheld.map(item =>
        `<tr><td>${escapeHtml(item.reason)}</td><td>${escapeHtml(String(item.count))}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No stored prospect was withheld. A missing business was not invented.");
}
