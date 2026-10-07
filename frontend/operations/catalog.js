function escapeCatalog(value) {
  return String(value ?? "").replace(/[&<>"']/g, character => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  })[character]);
}

async function loadCatalog() {
  const board = await api("/api/operations/blueprint");
  const notice = document.getElementById("catalog-notice");
  const delivery = document.getElementById("catalog-delivery");
  if (!notice) return board;
  notice.textContent = board.notice;
  delivery.textContent = `Delivery ${board.delivery}. Model calls ${board.modelCalls}. Campaign ready ${board.campaignReady ? "Yes" : "No"}. Geometry ${board.geometryStatus}. Amendment ${board.amendmentStatus}.`;
  const products = board.products || [];
  document.getElementById("catalog-products").innerHTML = products.length
    ? `<table><thead><tr><th>Product</th><th>Tier</th><th>Exclusivity</th><th>Lifecycle</th><th>Occupancy</th><th>Showcase</th></tr></thead><tbody>${products.map(item =>
        `<tr><td>${escapeCatalog(item.productId)}</td><td>${escapeCatalog(item.tier)}</td><td>${escapeCatalog(item.exclusivity)}</td><td>${escapeCatalog(item.lifecycle)}</td><td>${escapeCatalog(item.occupancyStatus)}</td><td>${escapeCatalog(item.showcaseId)}</td></tr>`).join("")}</tbody></table>`
    : "<p>No inventory product is stored.</p>";
  const showcases = board.showcases || [];
  document.getElementById("catalog-showcases").innerHTML = showcases.map(item => {
    const image = item.assetPresent
      ? `<img class="catalog-thumb" src="/api/operations/blueprint/showcases/${encodeURIComponent(item.showcaseId)}/image" alt="${escapeCatalog(item.showcaseId)} draft illustration" loading="lazy">`
      : `<div class="catalog-thumb catalog-missing">Awaiting file</div>`;
    return `<article class="catalog-tile">${image}<p class="eyebrow">${escapeCatalog(item.showcaseId)} · ${escapeCatalog(item.lifecycle)}</p><p>${escapeCatalog(item.notice)}</p><p>Asset on file: ${item.assetPresent ? "Yes" : "No"}. Authoritative contract: ${item.authoritative ? "Educational guide only" : "No"}.</p></article>`;
  }).join("");
  const references = board.references || {};
  document.getElementById("catalog-references").textContent =
    `Registered ${references.registered ?? 0} of ${references.expected ?? 50}. Uploaded ${references.uploaded ?? 0}. Awaiting upload ${references.awaitingUpload ?? 0}. ACTIVE ${references.active ?? 0}. Sample retrieval ${board.sampleRetrieval?.status ?? "unavailable"}. Regression ${board.regressionStatus}. Uploaded files stay CANDIDATE until a human approves them.`;
  const items = (references.items || []).slice().sort((left, right) => (left.nicheNumber || 0) - (right.nicheNumber || 0));
  const grid = document.getElementById("catalog-reference-grid");
  if (grid) {
    grid.innerHTML = items.map(item => {
      const image = item.assetPresent
        ? `<img class="catalog-thumb" src="/api/operations/blueprint/references/${encodeURIComponent(item.referenceId)}/image" alt="${escapeCatalog(item.nicheName)} candidate prototype" loading="lazy">`
        : `<div class="catalog-thumb catalog-missing">Awaiting file</div>`;
      const note = item.mappingNote ? `<small>${escapeCatalog(item.mappingNote)}</small>` : "";
      return `<figure class="catalog-tile">${image}<figcaption><strong>${escapeCatalog(item.referenceId)} · ${escapeCatalog(item.nicheName)}</strong><small>${escapeCatalog(item.expectedFile)} · ${escapeCatalog(item.lifecycle)} · ${escapeCatalog(item.uploadStatus)}</small>${note}</figcaption></figure>`;
    }).join("");
  }
  const unassigned = document.getElementById("catalog-unassigned");
  if (unassigned) {
    const extras = board.unassigned || [];
    unassigned.innerHTML = extras.length
      ? extras.map(item => `<figure class="catalog-tile"><img class="catalog-thumb" src="/api/operations/blueprint/unassigned/${encodeURIComponent(item.fileName)}" alt="${escapeCatalog(item.fileName)} unassigned upload" loading="lazy"><figcaption><strong>${escapeCatalog(item.fileName)}</strong><small>${escapeCatalog(item.reason)}</small></figcaption></figure>`).join("")
      : "<p>No unassigned inbox image is stored.</p>";
  }
  return board;
}

async function askCatalog(message) {
  const body = await api("/api/operations/blueprint/ask", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message })
  });
  const turn = body.turn || {};
  const target = document.getElementById("catalog-turn");
  if (target) {
    target.innerHTML = `<article><p class="eyebrow">${escapeCatalog(turn.intent)}</p><p>${escapeCatalog(turn.reply)}</p><p>Showcase displayed ${turn.showcaseDisplayed ? "Yes" : "No"}. Invented product ${turn.inventedProduct ? "Yes" : "No"}. Invented price ${turn.inventedPrice ? "Yes" : "No"}. Human escalation ${turn.humanEscalation ? "Yes" : "No"}. Delivery ${escapeCatalog(turn.delivery)}.</p></article>`;
  }
  await loadCatalog();
}

document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-catalog-ask]").forEach(button => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      try {
        await askCatalog(button.getAttribute("data-catalog-ask"));
      } catch (error) {
        const target = document.getElementById("catalog-turn");
        if (target) target.textContent = error.message;
      } finally {
        button.disabled = false;
      }
    });
  });
});
