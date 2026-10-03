function yesNo(value) {
  return value ? "Yes" : "No";
}

async function loadHosted() {
  const board = await api("/api/operations/hosted");
  $("#hosted-notice").textContent = "This reading records the process posture. Hosted acceptance is not claimed. A local database is not a hosted database. An identity provider was not contacted. A backup drill was not run. Green does not send. Delivery remains NOT_SENT.";
  $("#hosted-delivery").textContent = `Delivery ${board.delivery || "NOT_SENT"}. Environment ${board.environmentName || ""}. Hosted acceptance claimed ${yesNo(board.hostedAcceptanceClaimed)}. Identity contacted ${yesNo(board.identityContacted)}. Backup drill run ${yesNo(board.backupDrillRun)}.`;
  const gates = [
    ["Production gates", board.productionGatesApplied ? "Applied" : "Not applied"],
    ["Hosted database", yesNo(board.hostedDatabaseConfigured)],
    ["Server certificate verified", yesNo(board.databaseServerCertificateVerified)],
    ["Identity provider HTTPS", yesNo(board.identityProviderHttps)],
    ["Backup declared", yesNo(board.backupDeclared)],
    ["Secrets outside appsettings", yesNo(board.secretMaterialExternal)],
    ["Role claims distinct", yesNo(board.roleClaimsDistinct)]
  ];
  $("#hosted-gates").innerHTML = `<table><thead><tr><th>Posture</th><th>Reading</th></tr></thead><tbody>${gates.map(([label, value]) =>
    `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(value)}</td></tr>`).join("")}</tbody></table>`;
  const history = board.history || [];
  $("#hosted-history").innerHTML = history.length
    ? `<table><thead><tr><th>Reading</th><th>Environment</th><th>Gates</th><th>Hosted database</th><th>Identity HTTPS</th><th>Backup declared</th><th>Claimed</th><th>Identity contacted</th><th>Backup drill</th><th>Delivery</th></tr></thead><tbody>${history.map(item =>
        `<tr><td>${escapeHtml(item.readingKey)}</td><td>${escapeHtml(item.environmentName)}</td><td>${item.productionGatesApplied ? "Applied" : "Not applied"}</td><td>${yesNo(item.hostedDatabaseConfigured)}</td><td>${yesNo(item.identityProviderHttps)}</td><td>${yesNo(item.backupDeclared)}</td><td>${yesNo(item.hostedAcceptanceClaimed)}</td><td>${yesNo(item.identityContacted)}</td><td>${yesNo(item.backupDrillRun)}</td><td>${escapeHtml(item.delivery)}</td></tr>`).join("")}</tbody></table>`
    : emptyState("No hosted reading is stored. Hosted acceptance was not claimed.");
  return board;
}

async function storeHosted(claimHosted) {
  const decision = await api("/api/operations/hosted", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ readingKey: "hosted-reading-1", claimHosted })
  });
  $("#hosted-result").textContent = decision.duplicate
    ? "That hosted reading is already stored. Hosted acceptance was not claimed. Delivery remains NOT_SENT."
    : (decision.notice || "Delivery remains NOT_SENT.");
  await loadHosted();
}

document.addEventListener("DOMContentLoaded", () => {
  const store = document.getElementById("hosted-store");
  const claim = document.getElementById("hosted-claim");
  if (store) {
    store.addEventListener("click", async () => {
      store.disabled = true;
      try {
        await storeHosted(false);
      } catch (error) {
        $("#hosted-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        store.disabled = false;
      }
    });
  }
  if (claim) {
    claim.addEventListener("click", async () => {
      claim.disabled = true;
      try {
        await storeHosted(true);
      } catch (error) {
        $("#hosted-result").textContent = error.message;
        toast(error.message, true);
      } finally {
        claim.disabled = false;
      }
    });
  }
});
