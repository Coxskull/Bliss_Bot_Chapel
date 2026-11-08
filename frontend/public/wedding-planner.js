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

function askAlphaAnswer(question) {
  const q = question.toLowerCase();
  const has = (...terms) => terms.some(term => q.includes(term));
  if (question.includes("@") || /(?:\+?\d[ -]?){8,}/.test(question)) {
    return "Please do not enter email addresses or phone numbers here. This informational guide does not collect leads or send messages. A verified contact form will be needed for a private follow-up.";
  }
  if (has("privacy", "personal", "store", "save", "data", "record", "transcript")) {
    return "This guided conversation runs inside your current browser tab. It does not send your questions to a server, save a transcript, or contact anyone. Do not enter private business or personal details.";
  }
  if (has("price", "pricing", "cost", "quote", "budget", "fee", "payment")) {
    return "To request a standalone advertising plan, submit the advertiser inquiry with your all-in budget and currency, objective, market, preferred media and language. A human must confirm the planning deliverables, delivery capacity, price, currency and applicable terms in writing before paid work begins. This guide cannot issue a binding quote or collect payment. The inquiry form does not reserve inventory or promise a creator placement.";
  }
  if (has("human", "person", "representative", "contact", "call", "email", "support", "meeting")) {
    return "For a custom advertising-planning request, use the advertiser inquiry form. It records an inquiry for human review; it is not a purchase, support ticket or marketing opt-in, and response timing is not guaranteed.";
  }
  if (has("creator", "podcaster", "streamer", "influencer", "channel", "earn", "monetiz")) {
    return "Alpha welcomes emerging creators through established personalities and major media, including podcasts, livestreams, video, blogs, radio, television, newspapers and magazines. There is no follower-count minimum in this guide. Audience evidence, content suitability, rights and availability must be reviewed before a match; creator compensation is agreed voluntarily and is never assumed to be discounted. A placement or earning is not guaranteed.";
  }
  if (has("advertis", "sponsor", "brand", "campaign", "restaurant", "product", "business", "ad placement")) {
    return "Alpha accepts advertiser inquiries for human review. Use the advertiser form to describe your budget, objective, target market, preferred media, audience requirements and language. A planning proposal depends on confirmed scope, delivery capacity, costs, price, currency and written terms. This page does not book inventory or place or launch ads.";
  }
  if (has("result", "guarantee", "performance", "reach", "sales", "conversion")) {
    return "Campaign outcomes depend on the creator, audience and execution. Alpha does not guarantee sales, reach, conversions or a return on investment. Reported results must be supported by real measurement evidence.";
  }
  if (has("where", "city", "country", "philippine", "manila", "market", "location")) {
    return "Alpha’s launch markets include Santo Domingo, Panama City, Medellín, Manila, Kuala Lumpur and Jakarta. A proposal should use the local market, currency, audience evidence and the advertiser’s preferred language and communication style. This guide cannot confirm current availability or provide a translated human follow-up; those require the connected intake and local review.";
  }
  if (has("what", "how", "work", "alpha", "help", "hello", "hi", "start", "join")) {
    return "Ask Ask Alpha is a general information guide. For an advertising-planning request, use the public advertiser inquiry form; it does not purchase a service or promise a response. You can ask general questions about advertising, creators, pricing or privacy here.";
  }
  return "I can help with general questions about Alpha advertising, creator partnerships, pricing, campaign expectations and human assistance. For an individualized answer, a human review will be needed once intake is available.";
}

function appendAskAlphaMessage(who, body) {
  const bubble = document.createElement("p");
  bubble.className = "ask-alpha-bubble " + (who === "visitor" ? "ask-alpha-visitor" : "ask-alpha-guide");
  const heading = document.createElement("strong");
  heading.textContent = who === "visitor" ? "You" : "Ask Alpha · guided answer";
  const text = document.createElement("span");
  text.textContent = body;
  bubble.append(heading, text);
  askAlphaThread.append(bubble);
  askAlphaThread.scrollTop = askAlphaThread.scrollHeight;
}

function sendAskAlpha(question) {
  const cleaned = question.trim().slice(0, 400);
  if (!cleaned || askAlphaTurns >= 12) return;
  appendAskAlphaMessage("visitor", cleaned);
  appendAskAlphaMessage("guide", askAlphaAnswer(cleaned));
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
