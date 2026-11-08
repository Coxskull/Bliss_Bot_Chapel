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
    return "Alpha’s matching should start with your total budget and currency, then your goal, target market, preferred media, language and timing. A sustainable option must cover creator or media-owner compensation, Alpha’s disclosed service fee, production, rights, payment costs and applicable tax. If a preferred placement is over budget, the next step is to compare a smaller scope or lower-cost verified alternative while keeping creator compensation voluntary. Today this guide has no live, verified inventory or connected budget-matching intake, so it cannot return a placement or quote. It will not invent a price or collect payment.";
  }
  if (has("human", "person", "representative", "contact", "call", "email", "support", "meeting")) {
    return "A human representative will be required for a custom proposal or binding agreement. This guide cannot book a call, submit your contact details, or promise a reply. Verified human-contact options will appear once the commercial intake is activated.";
  }
  if (has("creator", "podcaster", "streamer", "influencer", "channel", "earn", "monetiz")) {
    return "Alpha welcomes emerging creators through established personalities and major media, including podcasts, livestreams, video, blogs, radio, television, newspapers and magazines. There is no follower-count minimum in this guide. Audience evidence, content suitability, rights and availability must be reviewed before a match; creator compensation is agreed voluntarily and is never assumed to be discounted. A placement or earning is not guaranteed.";
  }
  if (has("advertis", "sponsor", "brand", "campaign", "restaurant", "product", "business", "ad placement")) {
    return "Alpha can serve microbusinesses, small advertisers, growing companies and major organizations. Matching should begin with the advertiser’s all-in budget, then compare relevant creator and traditional-media options across local markets. When a preferred option exceeds the budget, offer a smaller scope or a lower-cost verified alternative before closing the conversation. The process still requires supply verification, fit review, a written quote and human approval. This page does not place or launch ads.";
  }
  if (has("result", "guarantee", "performance", "reach", "sales", "conversion")) {
    return "Campaign outcomes depend on the creator, audience and execution. Alpha does not guarantee sales, reach, conversions or a return on investment. Reported results must be supported by real measurement evidence.";
  }
  if (has("where", "city", "country", "philippine", "manila", "market", "location")) {
    return "Alpha’s launch markets include Santo Domingo, Panama City, Medellín, Manila, Kuala Lumpur and Jakarta. A proposal should use the local market, currency, audience evidence and the advertiser’s preferred language and communication style. This guide cannot confirm current availability or provide a translated human follow-up; those require the connected intake and local review.";
  }
  if (has("what", "how", "work", "alpha", "help", "hello", "hi", "start", "join")) {
    return "Ask Alpha is the information guide for Alpha Global Media's advertiser–creator marketplace. We help explain partnerships, possible ad formats and human-reviewed next steps. You can ask about creators, advertiser campaigns, pricing or privacy.";
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
