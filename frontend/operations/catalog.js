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
  const slots = board.slots || [];
  const geometry = document.getElementById("catalog-geometry");
  if (geometry) {
    geometry.innerHTML = slots.length
      ? `<table><thead><tr><th>Slot</th><th>Version</th><th>Size</th><th>Origin</th><th>Area</th><th>Status</th></tr></thead><tbody>${slots.map(slot =>
          `<tr><td>${escapeCatalog(slot.slotId)}</td><td>${escapeCatalog(slot.version)}</td><td>${escapeCatalog(slot.width)}×${escapeCatalog(slot.height)}</td><td>${escapeCatalog(slot.originX)}, ${escapeCatalog(slot.originY)}</td><td>${escapeCatalog(slot.area)}</td><td>${escapeCatalog(slot.lifecycle)}</td></tr>`).join("")}</tbody></table>`
      : "<p>No slot geometry is stored.</p>";
  }
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
  const needs = board.ownerNeeds || {};
  const needsSummary = document.getElementById("catalog-needs-summary");
  const needsTable = document.getElementById("catalog-needs");
  if (needsSummary) {
    needsSummary.textContent = `Open ${needs.open ?? 0}. Supplied ${needs.supplied ?? 0}. Quality grades stay ${board.quality?.status ?? "UNCLASSIFIED"} until a human writes them in the owner needs file.`;
  }
  if (needsTable) {
    const openNeeds = (needs.items || []).filter(item => item.status === "OPEN");
    needsTable.innerHTML = openNeeds.length
      ? `<table><thead><tr><th>Need</th><th>Waiting on the owner</th></tr></thead><tbody>${openNeeds.map(item =>
          `<tr><td>${escapeCatalog(item.needId)}</td><td>${escapeCatalog(item.need)}</td></tr>`).join("")}</tbody></table>`
      : "<p>No open owner need is stored.</p>";
  }
  const references = board.references || {};
  document.getElementById("catalog-references").textContent =
    `Registered ${references.registered ?? 0} of ${references.expected ?? 50}. Uploaded ${references.uploaded ?? 0}. Awaiting upload ${references.awaitingUpload ?? 0}. ACTIVE ${references.active ?? 0}. Sample retrieval ${board.sampleRetrieval?.status ?? "unavailable"}. Regression ${board.regressionStatus}. Quality grades stay UNCLASSIFIED.`;
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
  await loadJointAcceptance();
  return board;
}

function renderJoint(reading) {
  const target = document.getElementById("catalog-joint-result");
  if (!target || !reading) return;
  const gates = (reading.gates || []).map(item => `<li><strong>${escapeCatalog(item.gateId)} · ${escapeCatalog(item.status)}</strong><br>${escapeCatalog(item.evidence)}</li>`).join("");
  const conversations = (reading.conversations || []).map(item => `<li><strong>${escapeCatalog(item.caseId)} · ${escapeCatalog(item.intent)}</strong> · integrity ${item.integrityPassed ? "passed" : "failed"} · acceptance ${item.acceptancePassed ? "met" : "not met"}<br>${escapeCatalog(item.notice)}</li>`).join("");
  const mission = (reading.missionControl || []).map(item => `<tr><td>${escapeCatalog(item.productId)}</td><td>${escapeCatalog(item.tier)}</td><td>${escapeCatalog(item.occupancyStatus)}</td><td>${item.creatorAuthorized ? "Yes" : "No"}</td><td>${escapeCatalog(item.deviceStatus)} / ${escapeCatalog(item.platformStatus)}</td><td>${escapeCatalog(item.disclosure)}</td><td>${escapeCatalog(item.availability)}</td><td>${escapeCatalog(item.proofOfDelivery)}</td><td>${escapeCatalog(item.economicsPricingVersion)}</td></tr>`).join("");
  const retrieved = (reading.retrievedReferences || []).map(item => `<li>${escapeCatalog(item.referenceId)} — ${escapeCatalog(item.reason)}</li>`).join("") || "<li>No ACTIVE reference was retrieved.</li>";
  target.innerHTML = `<article>
    <p class="eyebrow">${escapeCatalog(reading.status)}</p>
    <p>Catalog amendment ${escapeCatalog(reading.catalogAmendment)}. Academy amendment ${escapeCatalog(reading.academyAmendment)}. Hosted acceptance ${escapeCatalog(reading.hostedAcceptance)}.</p>
    <p>Retrieval ${escapeCatalog(reading.retrievalStatus)}. Adaptation ${escapeCatalog(reading.adaptationStatus)} for ${escapeCatalog(reading.adaptedProductId)}. Economics ${escapeCatalog(reading.economicsSource)}</p>
    <h4>Gates</h4><ul>${gates}</ul>
    <h4>Conversations</h4><ol>${conversations}</ol>
    <h4>Retrieved references</h4><ul>${retrieved}</ul>
    <h4>Mission control</h4>
    <div class="table-wrap"><table><thead><tr><th>Product</th><th>Tier</th><th>Occupancy</th><th>Creator</th><th>Device / platform</th><th>Disclosure</th><th>Availability</th><th>Delivery proof</th><th>Economics</th></tr></thead><tbody>${mission}</tbody></table></div>
    <p>${escapeCatalog(reading.notice)}</p>
    <p>Model calls ${reading.modelCalls}. Campaign ready ${reading.campaignReady ? "Yes" : "No"}. Delivery ${escapeCatalog(reading.delivery)}.</p>
  </article>`;
}

async function loadJointAcceptance() {
  const reading = await api("/api/operations/blueprint/joint-acceptance");
  renderJoint(reading);
  return reading;
}

function renderAdaptation(result) {
  const target = document.getElementById("catalog-adapt-result");
  if (!target) return;
  const adaptation = result.adaptation || {};
  const similarity = result.similarity || {};
  const slots = (adaptation.slots || []).map(slot => {
    const size = slot.width && slot.height ? ` ${slot.width}×${slot.height}` : "";
    return escapeCatalog(slot.slotId) + size;
  }).join(", ");
  const roles = (result.workers || []).map(worker => escapeCatalog(worker.role)).join(", ");
  target.innerHTML = `<article><p class="eyebrow">${escapeCatalog(adaptation.status)} · ${escapeCatalog(similarity.status)}</p><p>${escapeCatalog(adaptation.notice)}</p><p>Slots ${slots || "none"}. Prototype scaled ${adaptation.scaledFromPrototype ? "Yes" : "No"}.</p><p>${escapeCatalog(similarity.notice)}</p><p>Workers ${roles}. None of these roles sets campaign ready.</p><p>Model calls ${result.modelCalls}. Campaign ready ${result.campaignReady ? "Yes" : "No"}. Delivery ${escapeCatalog(result.delivery)}.</p></article>`;
}

async function adaptCatalog(body) {
  const result = await api("/api/operations/blueprint/adapt", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  renderAdaptation(result);
  document.getElementById("catalog-adapt-result")?.scrollIntoView({ block: "center" });
  return result;
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
    const products = (turn.productIds || []).join(", ") || "none";
    const showcases = (turn.showcaseIds || []).join(", ") || "none";
    target.innerHTML = `<article><p class="eyebrow">${escapeCatalog(turn.intent)}</p><p>${escapeCatalog(turn.reply)}</p><p>Products ${escapeCatalog(products)}. Showcases ${escapeCatalog(showcases)}. Showcase displayed ${turn.showcaseDisplayed ? "Yes" : "No"}. Geometry ${escapeCatalog(turn.geometryStatus)}. Invented product ${turn.inventedProduct ? "Yes" : "No"}. Invented price ${turn.inventedPrice ? "Yes" : "No"}. Human escalation ${turn.humanEscalation ? "Yes" : "No"}. Delivery ${escapeCatalog(turn.delivery)}.</p></article>`;
  }
  await loadCatalog();
}

document.addEventListener("DOMContentLoaded", () => {
  const adapt = document.getElementById("catalog-adapt");
  const copied = document.getElementById("catalog-adapt-copy");
  if (adapt) {
    adapt.addEventListener("click", async () => {
      adapt.disabled = true;
      try {
        await adaptCatalog({
          productId: "ARE-P01",
          scalePrototype: false,
          brandName: "Norte Salud",
          headline: "Retira tu receta",
          face: "A local pharmacist",
          product: "A neighborhood pharmacy"
        });
      } catch (error) {
        const target = document.getElementById("catalog-adapt-result");
        if (target) target.textContent = error.message;
      } finally {
        adapt.disabled = false;
      }
    });
  }
  if (copied) {
    copied.addEventListener("click", async () => {
      copied.disabled = true;
      try {
        await adaptCatalog({
          productId: "ARE-P01",
          scalePrototype: false,
          brandName: "VidaCare Pharmacy",
          headline: "Care for a Brighter You",
          face: "VidaCare pharmacist",
          product: "VidaCare"
        });
      } catch (error) {
        const target = document.getElementById("catalog-adapt-result");
        if (target) target.textContent = error.message;
      } finally {
        copied.disabled = false;
      }
    });
  }
  const joint = document.getElementById("catalog-joint");
  if (joint) {
    joint.addEventListener("click", async () => {
      joint.disabled = true;
      try {
        await loadJointAcceptance();
        document.getElementById("catalog-joint-result")?.scrollIntoView({ block: "start" });
      } catch (error) {
        const target = document.getElementById("catalog-joint-result");
        if (target) target.textContent = error.message;
      } finally {
        joint.disabled = false;
      }
    });
  }
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
