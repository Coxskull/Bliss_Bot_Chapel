const meter = document.querySelector("#meter");
const rows = document.querySelector("#rows");
const notice = document.querySelector("#notice");
let latestQualified = null;
let latestFile = null;

function cell(text) {
  const item = document.createElement("td");
  item.textContent = text;
  return item;
}

function render(library) {
  meter.replaceChildren();
  [
    ["Daily benchmark", library.dailyTarget],
    ["Qualified unique", library.qualifiedUnique],
    ["Duplicates", library.duplicates],
    ["Replacement", library.replacementRequired],
    ["Daily remaining", library.dailyRemaining],
    ["Weekly benchmark", library.weeklyTarget],
    ["Fuel", library.fuelStatus]
  ].forEach(([label, value]) => {
    const article = document.createElement("article");
    const strong = document.createElement("strong");
    strong.textContent = value;
    if (label === "Fuel") strong.className = value === "HEALTHY" ? "ok" : "warn";
    const span = document.createElement("span");
    span.textContent = label;
    article.append(strong, span);
    meter.append(article);
  });
  rows.replaceChildren();
  latestQualified = null;
  library.clips.forEach(clip => {
    if (clip.status === "QUALIFIED" && !latestQualified) latestQualified = clip.id;
    const row = document.createElement("tr");
    row.append(
      cell(clip.originalFileName),
      cell(clip.market + ", " + clip.country),
      cell(String(clip.durationSeconds)),
      cell(clip.status),
      cell(String(clip.quotaCredit)),
      cell(clip.provenance)
    );
    rows.append(row);
  });
}

async function load() {
  const response = await fetch("/api/demonstrations/library");
  render(await response.json());
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const payload = await response.json();
  if (!response.ok) throw new Error(payload.error || "Request failed");
  return payload;
}

document.querySelector("#create").addEventListener("click", async () => {
  notice.textContent = "Creating an original 15-second studio slice…";
  try {
    const clip = await postJson("/api/demonstrations/source-media/studio-slice", {
      market: "Panama City",
      country: "Panama",
      seconds: 15
    });
    latestFile = clip;
    notice.textContent = clip.status + " · quota credit " + clip.quotaCredit;
    await load();
  } catch (error) {
    notice.textContent = error.message;
  }
});

document.querySelector("#again").addEventListener("click", async () => {
  notice.textContent = "Submitting another original slice with the same picture…";
  try {
    const clip = await postJson("/api/demonstrations/source-media/studio-slice", {
      market: "Panama City",
      country: "Panama",
      seconds: 15
    });
    notice.textContent = clip.originalFileName + " → " + clip.status + " · quota credit " + clip.quotaCredit;
    await load();
  } catch (error) {
    notice.textContent = error.message;
  }
});

document.querySelector("#produce").addEventListener("click", async () => {
  notice.textContent = "Compositing overlays, QR codes, and disclosure…";
  try {
    const page = await postJson("/api/demonstrations/abc-pharmacy/produce", {
      sourceClipId: latestQualified,
      conceptCount: 4
    });
    notice.textContent = "Demonstration ready. Delivery is " + page.delivery + ".";
    const open = document.createElement("a");
    open.href = "/outreach/" + page.slug;
    open.textContent = " Open the private message";
    notice.append(open);
  } catch (error) {
    notice.textContent = error.message;
  }
});

load();
