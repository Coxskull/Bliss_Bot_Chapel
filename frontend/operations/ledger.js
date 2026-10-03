async function loadLedger() {
  const board = await api("/api/operations/ledger");
  $("#ledger-notice").textContent = board.notice || "";
  $("#ledger-budget-notice").textContent = board.budgetNotice || "";
  $("#ledger-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Green does not send.`;
  const services = (board.services || []).map(service =>
    `<tr><td>${escapeHtml(service.displayName)}</td><td>${escapeHtml(service.classificationLine)}</td><td>${escapeHtml(service.status)}</td><td>${escapeHtml(service.costLine)}</td></tr>`).join("");
  $("#ledger-services").innerHTML = `<table><thead><tr><th>Service</th><th>Class</th><th>Status</th><th>Cost</th></tr></thead><tbody>${services}</tbody></table>`;
  const budgets = (board.budgets || []).map(scope =>
    `<tr><td>${escapeHtml(scope.displayName)}</td><td>${scope.degraded ? "Degraded" : "Open"}</td><td>${escapeHtml(scope.notice)}</td></tr>`).join("");
  $("#ledger-budgets").innerHTML = `<table><thead><tr><th>Scope</th><th>Place</th><th>Notice</th></tr></thead><tbody>${budgets}</tbody></table>`;
  const audits = board.audits || [];
  $("#ledger-audits").innerHTML = audits.length
    ? `<table><thead><tr><th>Action</th><th>Service</th><th>Status</th><th>Notice</th></tr></thead><tbody>${audits.map(item => `<tr><td>${escapeHtml(item.action)}</td><td>${escapeHtml(item.serviceKey)}</td><td>${escapeHtml(item.status)}</td><td>${escapeHtml(item.notice)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No subscription audit is recorded. None was invented. Nothing was purchased.");
}

document.addEventListener("DOMContentLoaded", () => {
  const budget = document.getElementById("ledger-budget-form");
  if (budget) {
    budget.addEventListener("submit", async event => {
      event.preventDefault();
      const data = new FormData(budget);
      const body = {
        scope: data.get("scope"),
        ceilingCurrency: String(data.get("ceilingCurrency") || "").trim(),
        reason: String(data.get("reason") || "").trim()
      };
      const ceiling = String(data.get("ceilingAmount") || "").trim();
      const spend = String(data.get("recordedSpend") || "").trim();
      if (ceiling) body.ceilingAmount = Number(ceiling);
      if (spend) body.recordedSpend = Number(spend);
      await submitLedger(budget, "/api/operations/ledger/budget", body, "DAILY");
    });
  }

  const refusal = document.getElementById("ledger-refuse-review");
  if (refusal) {
    refusal.addEventListener("click", async () => {
      refusal.disabled = true;
      try {
        await api("/api/operations/ledger/review", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            serviceName: "Enrichment suite",
            classification: "NEW_PAID_SUBSCRIPTION"
          })
        });
      } catch (error) {
        $("#ledger-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        refusal.disabled = false;
        await loadLedger();
      }
    });
  }
});

async function submitLedger(form, url, body, scope) {
  const submit = form.querySelector('button[type="submit"]');
  submit.disabled = true;
  try {
    const board = await api(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const row = (board.budgets || []).find(item => item.scope === scope);
    $("#ledger-result").textContent = row ? row.notice : board.budgetNotice;
    await loadLedger();
    toast("Factory budget recorded. Nothing was purchased. Delivery remains NOT_SENT.");
  } catch (error) {
    $("#ledger-result").textContent = error.message;
    toast(error.message, true);
  } finally {
    submit.disabled = false;
  }
}
