async function loadRoutes() {
  const board = await api("/api/operations/routes");
  $("#route-notice").textContent = board.notice || "";
  $("#route-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Green does not send.`;
  const facts = [
    ["Stored prospects", board.stored],
    ["Suppressed", board.suppressed],
    ["Stale evidence", board.stale],
    ["Missing evidence", board.missingEvidence],
    ["No road", board.noRoad],
    ["Preview", board.preview],
    ["Withheld", board.withheld],
    ["Census claimed", board.censusClaimed ? "Yes" : "No. This is not a census."]
  ];
  $("#route-facts").innerHTML = `<table><thead><tr><th>Reading</th><th>Stored value</th></tr></thead><tbody>${facts.map(([label, value]) =>
    `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(String(value))}</td></tr>`).join("")}</tbody></table>`;
  const routes = board.routes || [];
  $("#route-rows").innerHTML = routes.length
    ? `<table><thead><tr><th>Business</th><th>Route</th><th>Adapter</th><th>Transmission</th><th>Notice</th></tr></thead><tbody>${routes.map(item =>
        `<tr><td>${escapeHtml(item.businessName)}</td><td>${escapeHtml(item.route)}</td><td>${escapeHtml(item.adapter)}</td><td>${escapeHtml(item.transmission)}</td><td>${escapeHtml(item.notice)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No stored prospect is on the route. None was invented. This is not a census.");
  const audits = board.audits || [];
  $("#route-audits").innerHTML = audits.length
    ? `<table><thead><tr><th>Key</th><th>Words</th><th>Adapter</th><th>Transmission</th><th>Notice</th></tr></thead><tbody>${audits.map(item =>
        `<tr><td>${escapeHtml(item.idempotencyKey)}</td><td>${escapeHtml(item.authorizationLine)}</td><td>${escapeHtml(item.adapter)}</td><td>${escapeHtml(item.transmission)}</td><td>${escapeHtml(item.notice)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No transmission request is recorded. Nothing was sent.");
}

document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("route-transmission-form");
  if (!form) return;
  form.addEventListener("submit", async event => {
    event.preventDefault();
    const submit = form.querySelector('button[type="submit"]');
    const data = new FormData(form);
    submit.disabled = true;
    try {
      const decision = await api("/api/operations/routes/transmission", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          authorization: String(data.get("authorization") || ""),
          idempotencyKey: String(data.get("idempotencyKey") || "")
        })
      });
      $("#route-result").textContent = decision.notice || "";
      await loadRoutes();
    } catch (error) {
      $("#route-result").textContent = error.message;
      toast(error.message, true);
    } finally {
      submit.disabled = false;
    }
  });
});
