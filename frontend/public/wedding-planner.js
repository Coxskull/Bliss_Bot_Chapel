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

// Existing guide is preserved. AI is enabled ONLY by an explicit Vercel
// release flag, explicit per-visitor processing consent, and a separately
// enabled server-side Supabase budget/receipt gateway. Never touch CRM here.
const askAlphaInput = document.querySelector("#ask-alpha-input");
const askAlphaSend = document.querySelector("#ask-alpha-send");
const askAlphaThread = document.querySelector("#ask-alpha-thread");
let askAlphaTurns = 0;
const askAlphaLive = document.querySelector("#ask-alpha")?.dataset.askAlphaAi === "true";
const askAlphaAiPanel = document.querySelector("#ask-alpha-ai-consent-panel");
const askAlphaAiConsent = document.querySelector("#ask-alpha-ai-consent");
const askAlphaMarketingConsent = document.querySelector("#ask-alpha-marketing-consent");
const askAlphaAiStatus = document.querySelector("#ask-alpha-ai-status");
const askAlphaSessionId = window.crypto?.randomUUID?.() ?? null;
const askAlphaHistory = [];
let askAlphaPending = false;
if (askAlphaLive && askAlphaAiPanel && askAlphaAiConsent) {
  askAlphaAiPanel.hidden = false;
  if (askAlphaSend) askAlphaSend.textContent = "Ask AI";
  if (askAlphaSend) askAlphaSend.setAttribute("aria-label","Send question to Ask Alpha AI");
}


const askAlphaActions = Object.freeze({
  advertiser: Object.freeze({label:"Open advertiser inquiry form",href:"/join-advertiser.html"}),
  creator: Object.freeze({label:"Open creator inquiry form",href:"/join-creator.html"}),
  contact: Object.freeze({label:"View contact options",href:"/contact.html"}),
  privacy: Object.freeze({label:"Read the Privacy Notice",href:"/alpha-privacy.html"}),
  privacyRequest: Object.freeze({label:"Open privacy request form",href:"/privacy-request.html"})
});
function askAlphaGuideActions(question) {
  const q = question.toLowerCase();
  if (/privacy|personal|store|save|data|record|transcript/.test(q))
    return [askAlphaActions.privacy,askAlphaActions.privacyRequest];
  if (/human|person|representative|contact|call|email|support|meeting|refund|dispute/.test(q))
    return [askAlphaActions.contact,askAlphaActions.advertiser,askAlphaActions.creator];
  if (/price|pricing|cost|quote|budget|fee|payment/.test(q)) return [askAlphaActions.advertiser];
  if (/creator|podcast|streamer|influenc|channel|earn|monetiz/.test(q)) return [askAlphaActions.creator];
  if (/advertis|sponsor|brand|campaign|restaurant|product|business|ad placement/.test(q)) return [askAlphaActions.advertiser];
  return [askAlphaActions.advertiser,askAlphaActions.creator];
}

function askAlphaAnswer(question) {
  const q = question.toLowerCase();
  const has = (...terms) => terms.some(term => q.includes(term));
  if (question.includes("@") || /(?:\+?\d[ -]?){8,}/.test(question)) {
    return "Please do not enter email addresses or phone numbers here. This informational guide does not collect leads or send messages. Use the appropriate public inquiry form for human review.";
  }
  if (has("privacy", "personal", "store", "save", "data", "record", "transcript")) {
    return "Guided answers are generated locally in this tab. If you explicitly opt in to live AI, your question and limited conversation context may be sent for AI processing under the separate Privacy Notice. This chat does not authorize commercial actions. Do not enter private business or personal details.";
  }
  if (has("price", "pricing", "cost", "quote", "budget", "fee", "payment")) {
    return "Start with your total campaign budget. Creator and media costs, creative scope and Alpha's disclosed fee must fit an accepted human-reviewed quote. A smaller scope or alternative inventory may be considered. Ask Alpha cannot invent prices or collect payment; use the advertiser inquiry form.";
  }
  if (has("human", "person", "representative", "contact", "call", "email", "support", "meeting")) {
    return "A human representative will be required for a custom proposal or binding agreement. This guide cannot book a call, submit your contact details, or promise a reply. Use the existing Contact page or relevant public inquiry form for human review.";
  }
  if (has("creator", "podcaster", "streamer", "influencer", "channel", "earn", "monetiz")) {
    return "Alpha aims to connect suitable podcasters, livestreamers and content creators with advertisers. A creator's audience, content, availability and eligibility need review before any proposed campaign. Earnings or placements are not guaranteed.";
  }
  if (has("advertis", "sponsor", "brand", "campaign", "restaurant", "product", "business", "ad placement")) {
    return "Alpha helps brands explore relevant creator and podcast advertising placements. The process is discovery, fit review, creative planning and a separately approved campaign proposal. This page does not place or launch ads.";
  }
  if (has("result", "guarantee", "performance", "reach", "sales", "conversion")) {
    return "Campaign outcomes depend on the creator, audience and execution. Alpha does not guarantee sales, reach, conversions or a return on investment. Reported results must be supported by real measurement evidence.";
  }
  if (has("where", "city", "country", "philippine", "manila", "market", "location")) {
    return "Alpha researches opportunities across multiple markets. Availability, creator eligibility and campaign coverage are not confirmed by this guide. A specific market can be reviewed during a human-led proposal.";
  }
  if (has("what", "how", "work", "alpha", "help", "hello", "hi", "start", "join")) {
    return "Ask Alpha is the information guide for Alpha Global Media's advertiser–creator marketplace. We help explain partnerships, possible ad formats and human-reviewed next steps. You can ask about creators, advertiser campaigns, pricing or privacy.";
  }
  return "I can help with general questions about Alpha advertising, creator partnerships, pricing, campaign expectations and human assistance. For an individualized answer, use the existing public inquiry form for human review.";
}

function appendAskAlphaMessage(who, body, actions = []) {
  const bubble = document.createElement("p");
  bubble.className = "ask-alpha-bubble " + (who === "visitor" ? "ask-alpha-visitor" : "ask-alpha-guide");
  const heading = document.createElement("strong");
  heading.textContent = who === "visitor" ? "You" :
    who === "ai" ? "Ask Alpha · AI response (informational)" : "Ask Alpha · guided answer";
  const text = document.createElement("span");
  text.textContent = body;
  bubble.append(heading, text);
  if (who === "guide") for (const action of actions) {
    if (!Object.values(askAlphaActions).includes(action)) continue;
    const link = document.createElement("a");
    link.className = "ask-alpha-action";
    link.href = action.href;
    link.textContent = action.label;
    bubble.append(link);
  }
  askAlphaThread.append(bubble);
  askAlphaThread.scrollTop = askAlphaThread.scrollHeight;
}

async function sendAskAlpha(question) {
  const cleaned = String(question || "").trim().slice(0,400);
  if (!cleaned || askAlphaTurns >= 12 || askAlphaPending) return;
  // Never send contact data into an informal chat: public forms handle it.
  if (cleaned.includes("@") || /(?:\+?\d[ -]?){8,}/.test(cleaned) ||
      /\b(?:password|passcode|one[- ]?time(?: code| password)?|otp|cvv|cvc|pin|secret(?: key)?|api[- ]?key)\s*[:=]\s*\S{2,}/i.test(cleaned)) {
    appendAskAlphaMessage("guide",
      "Please avoid personal contact or payment details in chat. Use the appropriate public inquiry form.");
    askAlphaInput.value = "";
    return;
  }
  appendAskAlphaMessage("visitor",cleaned);
  askAlphaInput.value = "";
  askAlphaTurns++;
  if (!askAlphaLive || !askAlphaAiConsent?.checked || !askAlphaSessionId) {
    appendAskAlphaMessage("guide",askAlphaAnswer(cleaned),askAlphaGuideActions(cleaned));
    if (askAlphaLive && askAlphaAiStatus) {
      askAlphaAiStatus.textContent = "No AI request made. You can use guided help without agreeing to AI processing.";
    }
  } else {
    askAlphaPending = true;
    askAlphaSend.disabled = true;
    askAlphaInput.disabled = true;
    const q = cleaned.toLowerCase();
    const audience = /support|refund|dispute|complaint|problem|privacy/.test(q) ? "support" :
      /creator|podcast|livestream|influenc|register/.test(q) ? "creator" :
      /advertis|brand|campaign|sponsor|proposal/.test(q) ? "advertiser" : "general";
    try {
      // Reuse the exact established public-intake Supabase function, not an
      // additional backend. No API key, authorization, or user PII stored here.
      const response = await fetch(
        "https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake",{
          method:"POST",mode:"cors",credentials:"omit",cache:"no-store",
          headers:{"Content-Type":"application/json"},
          body:JSON.stringify({
            action:"ask_alpha",notice_version:"AGM-ASK-ALPHA-AI-2026-11-08",
            ai_processing_consent:true,
            marketing_opt_in:askAlphaMarketingConsent?.checked === true,
            session_id:askAlphaSessionId,request_key:crypto.randomUUID(),
            audience,message:cleaned,history:askAlphaHistory.slice(-4)
          }),signal:AbortSignal.timeout(20000)
        }
      );
      const result = await response.json();
      if (response.ok && result?.status === "ANSWERED" &&
          typeof result.answer === "string" &&
          /^[0-9a-f-]{36}$/i.test(result.receipt_id || "")) {
        appendAskAlphaMessage("ai",result.answer);
        askAlphaHistory.push({role:"user",content:cleaned},
          {role:"assistant",content:result.answer.slice(0,400)});
        while (askAlphaHistory.length > 4) askAlphaHistory.shift();
        if (askAlphaAiStatus) askAlphaAiStatus.textContent =
          "AI request recorded: " + result.receipt_id + ". Not an official support case or approved proposal.";
      } else {
        // A known operational hold is not a visitor fault or a confirmed AI reply.
        // Use the existing local guide, never retry automatically or expose raw server errors.
        const held = response.status === 503 && result?.error === "AI_TEMPORARILY_UNAVAILABLE";
        appendAskAlphaMessage("guide",askAlphaAnswer(cleaned),askAlphaGuideActions(cleaned));
        if (askAlphaAiStatus) askAlphaAiStatus.textContent = held
          ? "Live AI is temporarily unavailable. No AI provider request was sent. Guided help remains available."
          : "AI answer or receipt not confirmed. Guided help remains available; no automatic retry was made.";
      }
    } catch {
      appendAskAlphaMessage("guide",askAlphaAnswer(cleaned),askAlphaGuideActions(cleaned));
      if (askAlphaAiStatus) askAlphaAiStatus.textContent =
        "Unable to verify the AI receipt. No automatic retry or submission was made.";
    } finally {
      askAlphaPending = false;
      askAlphaSend.disabled = askAlphaTurns >= 12;
      askAlphaInput.disabled = askAlphaTurns >= 12;
    }
  }
  if (askAlphaTurns >= 12) {
    askAlphaInput.disabled = true;
    askAlphaSend.disabled = true;
    appendAskAlphaMessage("guide",
      "This tab reached its 12-question limit. A new session requires a page reload.");
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
