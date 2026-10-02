const slug = location.pathname.split("/").filter(Boolean).pop();
const business = document.querySelector("#business");
const promise = document.querySelector("#promise");
const lead = document.querySelector("#lead");
const context = document.querySelector("#context");
const facts = document.querySelector("#facts");
const concepts = document.querySelector("#concepts");
const disclosure = document.querySelector("#disclosure");
const messages = document.querySelector("#messages");
const form = document.querySelector("#chat");
const draft = document.querySelector("#draft");

function bubble(role, text) {
  const item = document.createElement("div");
  item.className = "bubble " + role;
  item.textContent = text;
  messages.append(item);
  messages.scrollTop = messages.scrollHeight;
}

function render(page) {
  const preserved = page.prospectState === "PRESERVED";
  document.title = preserved
    ? page.businessName + " · preserved"
    : page.businessName + " · Alpha demonstration";
  business.textContent = page.businessName;
  const status = document.querySelector("#pageStatus");
  const benefits = document.querySelector("#pageBenefits");
  if (status) {
    status.textContent = page.suppressed
      ? "SUPPRESSED · NOTHING SENT"
      : preserved
        ? "PRESERVED · NOTHING SENT"
        : "PRIVATE ADVERTISING DEMONSTRATION";
  }
  if (benefits) benefits.style.display = preserved || page.suppressed ? "none" : "";
  const badge = document.querySelector(".badge");
  if (badge) badge.style.display = preserved || page.suppressed ? "none" : "";
  promise.textContent = page.suppressed
    ? "Nothing is sent"
    : preserved
      ? "No demonstration was manufactured"
      : "Get More Customers in " + page.market + " with Podcast Advertising";
  const roadLine = page.contactRoads && page.contactRoads.length
    ? " Public roads stay ineligible. A public road is not permission to send."
    : "";
  const suppressedLine = page.suppressed
    ? " " + page.businessName + " is suppressed. " + page.suppressionReason + "."
    : "";
  lead.textContent = page.suppressed
    ? page.businessName + " is suppressed. " + page.suppressionReason + ". A public road is not permission to send. Delivery is " + page.delivery + "."
    : preserved
    ? page.businessName + " is preserved. The road score is " + page.opportunityScore + ". No demonstration was manufactured. Nothing was sent. Delivery is " + page.delivery + "." + roadLine
    : page.personalizationAllowed && page.decisionMakerName
    ? "This private page records " + page.decisionMakerName + ", " + page.decisionMakerRole + ", from a public source. Confidence is " + page.decisionMakerStatus + ". Delivery is " + page.delivery + "."
    : "Your community is listening. This private page shows how an approved 15-second source slice can carry an advertisement for " + page.businessName + "."
      + roadLine + suppressedLine;
  context.textContent = preserved
    ? page.market + "  |  " + (page.niche || "local business") + "  |  Nothing was sent"
    : page.market + "  |  " + page.niche + "  |  Real Audiences";
  facts.replaceChildren();
  const factRows = [
    ["Decision maker", page.decisionMakerStatus],
    ["Contact tier", page.contactTier],
    ["Delivery", page.delivery],
    ["Buying roles", page.buyingRoles.join(", ")]
  ];
  if (page.freshness && page.freshness !== "UNRECORDED") factRows.push(["Freshness", page.freshness]);
  if (page.personalizationAllowed && page.decisionMakerName) factRows.push(["Public name", page.decisionMakerName + ", " + page.decisionMakerRole]);
  if (page.opportunityScore) factRows.push(["Opportunity score", String(page.opportunityScore)]);
  if (page.publicSourceUrl) factRows.push(["Public source", page.publicSourceUrl]);
  if (page.contactRoads && page.contactRoads.length) {
    factRows.push(["Public roads", page.contactRoads.map(road => road.kind + " " + road.value + " (" + road.state + ")").join("; ")]);
    factRows.push(["Outreach", "Not eligible"]);
  }
  if (page.suppressed) factRows.push(["Suppression", page.suppressionReason]);
  factRows.forEach(([label, value]) => {
    const block = document.createElement("div");
    const strong = document.createElement("strong");
    strong.textContent = value;
    const span = document.createElement("span");
    span.textContent = label;
    block.append(strong, span);
    facts.append(block);
  });
  concepts.replaceChildren();
  page.concepts.forEach((concept, index) => {
    const card = document.createElement("article");
    card.className = "card";
    const title = document.createElement("h3");
    title.textContent = (index + 1) + ". " + concept.name;
    const video = document.createElement("video");
    video.controls = true;
    video.playsInline = true;
    video.preload = "metadata";
    video.src = concept.videoUrl;
    const caption = document.createElement("p");
    caption.textContent = concept.headline + " " + concept.subhead + " · " + concept.callToAction;
    const qr = document.createElement("img");
    qr.className = "qr";
    qr.alt = "QR code for " + concept.name;
    qr.src = concept.qrUrl;
    card.append(title, video, caption, qr);
    concepts.append(card);
  });
  disclosure.textContent = page.disclosure + " " + page.contactRoute;
  messages.replaceChildren();
  page.messages.forEach(message => bubble(message.role, message.text));
}

async function load() {
  const response = await fetch("/api/demonstrations/" + slug);
  if (!response.ok) {
    business.textContent = "This demonstration is not ready";
    lead.textContent = "Create a qualified source slice and produce the ABC Pharmacy page from Source Media.";
    return;
  }
  render(await response.json());
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  const text = draft.value.trim();
  if (!text) return;
  draft.value = "";
  bubble("VISITOR", text);
  const response = await fetch("/api/demonstrations/" + slug + "/messages", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text })
  });
  const body = await response.json();
  if (response.ok) bubble("ASSISTANT", body.reply);
  else bubble("ASSISTANT", body.error || "The message was not accepted.");
});

load();
