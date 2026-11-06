const menu = document.querySelector(".menu-button");
const nav = document.querySelector(".site-header nav");

menu?.addEventListener("click", () => {
  const open = nav?.classList.toggle("open") ?? false;
  menu.setAttribute("aria-expanded", String(open));
});

document.querySelectorAll(".site-header nav a").forEach(link => {
  link.addEventListener("click", () => {
    nav?.classList.remove("open");
    menu?.setAttribute("aria-expanded", "false");
  });
});

// Public, read-only guided Ask Alpha conversation. No network requests, tracking,
// contact capture, model provider, state persistence, or commercial commands.
const askAlphaInput = document.querySelector("#ask-alpha-input");
const askAlphaSend = document.querySelector("#ask-alpha-send");
const askAlphaThread = document.querySelector("#ask-alpha-thread");
let askAlphaTurns = 0;

const askAlphaActions = Object.freeze({
  advertiser: Object.freeze({ label: "Open advertiser inquiry form", href: "/join-advertiser.html" }),
  creator: Object.freeze({ label: "Open creator inquiry form", href: "/join-creator.html" }),
  contact: Object.freeze({ label: "View contact options", href: "/contact.html" }),
  privacy: Object.freeze({ label: "Read the Privacy Notice", href: "/alpha-privacy.html" }),
  privacyRequest: Object.freeze({ label: "Open privacy request form", href: "/privacy-request.html" })
});

const answer = (text, actionKeys = []) => ({
  text,
  actions: actionKeys.map(key => askAlphaActions[key])
});

function askAlphaAnswer(question) {
  const q = question.toLowerCase();
  const has = (...terms) => terms.some(term => q.includes(term));
  if (question.includes("@") || /(?:\+?\d[ -]?){8,}/.test(question)) {
    return answer(
      "Please do not enter email addresses or phone numbers here. This guide does not collect leads or send messages. Use the relevant public inquiry form if you want ALPHA to review an advertiser or creator inquiry.",
      ["advertiser", "creator"]
    );
  }
  if (has("privacy", "personal", "store", "save", "data", "record", "transcript")) {
    return answer(
      "This guided conversation runs in your current browser tab. It does not send your questions to a server, save a transcript, or contact anyone. Public inquiry forms have a separate Privacy Notice and privacy request form.",
      ["privacy", "privacyRequest"]
    );
  }
  if (has("price", "pricing", "cost", "quote", "budget", "fee", "payment")) {
    return answer(
      "Campaign pricing depends on an approved placement, audience, creative scope and an accepted quote. The advertiser inquiry form is available now; a person may review scope and availability before any proposal. There is no public checkout, and this guide cannot issue a quote or collect payment.",
      ["advertiser"]
    );
  }
  if (has("human", "person", "representative", "contact", "call", "email", "support", "meeting")) {
    return answer(
      "For a human review, use the Contact page or the relevant public inquiry form. These forms are requests for information, not support tickets, and a reply is not guaranteed.",
      ["contact", "advertiser", "creator"]
    );
  }
  if (has("creator", "podcaster", "streamer", "influencer", "channel", "earn", "monetiz")) {
    return answer(
      "ALPHA aims to connect suitable podcasters, livestreamers and content creators with advertisers. A creator's audience, content, availability and eligibility need review before any proposed campaign. Earnings or placements are not guaranteed.",
      ["creator"]
    );
  }
  if (has("advertis", "sponsor", "brand", "campaign", "restaurant", "product", "business", "ad placement")) {
    return answer(
      "ALPHA helps brands explore relevant creator and podcast advertising placements. A public advertiser inquiry is available for human review; it does not place or launch ads.",
      ["advertiser"]
    );
  }
  if (has("result", "guarantee", "performance", "reach", "sales", "conversion")) {
    return answer(
      "Campaign outcomes depend on the creator, audience and execution. ALPHA does not guarantee sales, reach, conversions or a return on investment. Reported results must be supported by real measurement evidence."
    );
  }
  if (has("where", "city", "country", "philippine", "manila", "market", "location")) {
    return answer(
      "ALPHA researches opportunities across multiple markets. Availability, creator eligibility and campaign coverage are not confirmed by this guide. A specific market can be reviewed through a human-led inquiry.",
      ["advertiser", "creator"]
    );
  }
  if (has("what", "how", "work", "alpha", "help", "hello", "hi", "start", "join", "next step")) {
    return answer(
      "Ask Alpha is the information guide for ALPHA GLOBAL MEDIA's advertiser–creator marketplace. It explains partnerships, possible ad formats and human-reviewed next steps. Choose the advertiser or creator inquiry form below to introduce your request.",
      ["advertiser", "creator"]
    );
  }
  return answer(
    "I can help with general questions about ALPHA advertising, creator partnerships, pricing, campaign expectations and human assistance. For an individual response, submit a public inquiry for human review; this guide does not collect contact details.",
    ["advertiser", "creator"]
  );
}

function appendAskAlphaMessage(who, body, actions = []) {
  const bubble = document.createElement("p");
  bubble.className = "ask-alpha-bubble " + (who === "visitor" ? "ask-alpha-visitor" : "ask-alpha-guide");
  const heading = document.createElement("strong");
  heading.textContent = who === "visitor" ? "You" : "Ask Alpha · guided answer";
  const text = document.createElement("span");
  text.textContent = body;
  bubble.append(heading, text);
  for (const action of actions) {
    const link = document.createElement("a");
    link.className = "ask-alpha-action";
    link.href = action.href;
    link.textContent = action.label;
    bubble.append(link);
  }
  askAlphaThread.append(bubble);
  askAlphaThread.scrollTop = askAlphaThread.scrollHeight;
}

function sendAskAlpha(question) {
  const cleaned = question.trim().slice(0, 400);
  if (!cleaned || askAlphaTurns >= 12) return;
  appendAskAlphaMessage("visitor", cleaned);
  const result = askAlphaAnswer(cleaned);
  appendAskAlphaMessage("guide", result.text, result.actions);
  askAlphaTurns++;
  askAlphaInput.value = "";
  if (askAlphaTurns >= 12) {
    askAlphaInput.disabled = true;
    askAlphaSend.disabled = true;
    appendAskAlphaMessage("guide", "This guided session has reached its 12-question limit. Reload the page to start a fresh informational session.");
  }
}

if (askAlphaInput && askAlphaSend && askAlphaThread) {
  askAlphaSend.addEventListener("click", () => sendAskAlpha(askAlphaInput.value));
  askAlphaInput.addEventListener("keydown", event => {
    if (event.key === "Enter") {
      event.preventDefault();
      sendAskAlpha(askAlphaInput.value);
    }
  });
  document.querySelectorAll("[data-ask-alpha-prompt]").forEach(button => {
    button.addEventListener("click", () => sendAskAlpha(button.dataset.askAlphaPrompt || ""));
  });
}
