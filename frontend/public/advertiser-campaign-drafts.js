(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const panel = el("draft-panel"), form = el("draft-form"), list = el("draft-list");
  const status = el("draft-status"), save = el("draft-save");
  const name = el("draft-name"), brief = el("draft-brief");
  const endpoint = "/api/advertisers/my-campaign-drafts";
  let generation = 0, csrf = null, busy = false, retry = null;
  let previewUrls = [];
  function disposePreviews() {
    for (const url of previewUrls) URL.revokeObjectURL(url);
    previewUrls = [];
  }
  function clear() {
    generation++; csrf = null; busy = false; retry = null;
    disposePreviews();
    panel.hidden = true; form.reset(); list.replaceChildren(); status.textContent = "";
    save.disabled = true;
  }
  function render(rows) {
    disposePreviews(); list.replaceChildren();
    if (!rows.length) { list.textContent = "No campaign drafts yet."; return; }
    for (const row of rows) {
      const card = document.createElement("article");
      for (const [tag, text] of [["h3",row.name],["p",row.brief],["p","Status: "+row.status]]) {
        const node = document.createElement(tag); node.textContent = text; card.appendChild(node);
      }
      const preview = document.createElement("button");
      preview.type = "button"; preview.textContent = "Prepare creative QA preview";
      card.appendChild(preview);
      preview.addEventListener("click", async () => {
        if (!csrf || panel.hidden || busy || preview.disabled) return;
        const current = generation; preview.disabled = true;
        status.textContent = "Preparing read-only QA manifest. No AI model will be called.";
        try {
          const response = await fetch(endpoint + "/creative-handoff-preview", {
            method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},
            body:JSON.stringify({campaignId:row.campaignId})
          });
          if (current !== generation) return;
          if (!response.ok) {
            status.textContent = "Creative QA preview is not yet enabled for this account.";
            return;
          }
          const manifest = await response.json();
          if (manifest.schema !== "alpha.advertiser.campaign-creative-handoff.v1"
              || manifest.campaignId !== row.campaignId || manifest.modelCall !== "NOT_REQUESTED"
              || manifest.release !== "HELD" || manifest.delivery !== "NOT_SENT"
              || manifest.signedAuthorization !== false)
            throw new Error("QA manifest returned unexpected authorization state");
          const blob = new Blob([JSON.stringify(manifest,null,2)],{type:"application/json"});
          const url = URL.createObjectURL(blob); previewUrls.push(url);
          const link = document.createElement("a");
          link.href = url; link.download = "alpha-campaign-" + row.campaignId + "-held-qa.json";
          link.textContent = "Download private QA-only manifest";
          card.appendChild(document.createTextNode(" "));
          card.appendChild(link);
          status.textContent = "QA manifest ready. No image, Factory mission, payment or delivery was triggered.";
          preview.remove();
        } catch {
          if (current === generation) status.textContent = "Creative QA handoff unavailable; no external action was taken.";
        } finally {if (current === generation && preview.isConnected) preview.disabled = false;}
      });
      const consentButton = document.createElement("button"); consentButton.type = "button";
      consentButton.textContent = "Review prepared recipe and AI consent"; card.appendChild(consentButton);
      const consentPanel = document.createElement("section"); card.appendChild(consentPanel);
      async function consent(action, prior) {
        if (!csrf || panel.hidden || consentButton.disabled) return;
        const current = generation; consentButton.disabled = true;
        for (const button of consentPanel.querySelectorAll("button")) button.disabled = true;
        try {
          const payload = {campaignId:row.campaignId,action};
          if (action !== "REVIEW") Object.assign(payload,{scopeSha256:prior.scopeSha256,noticeVersion:prior.noticeVersion,
            expectedReceiptId:prior.receiptId,requestKey:crypto.randomUUID()});
          const response = await fetch(endpoint+"/creative-provider-consent",{method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(payload)});
          if (current !== generation) return;
          if (!response.ok) throw Error("Prepared recipe unavailable or changed. No provider call was requested.");
          const result = await response.json();
          if (result.campaignId !== row.campaignId || result.provider !== "OpenAI" || result.model !== "gpt-image-2.5-flare"
              || result.noticeVersion !== "alpha-creative-provider-consent-v1" || !/^[0-9a-f]{64}$/.test(result.scopeSha256)
              || typeof result.generationRecipe !== "string" || result.dispatchAuthorized !== false
              || result.billingAuthorization !== "NOT_GRANTED" || result.providerCall !== "NOT_REQUESTED" || result.release !== "HELD"
              || !["NOT_COLLECTED","GRANTED","REVOKED"].includes(result.consentStatus)) throw Error("Unexpected consent response.");
          consentPanel.replaceChildren();
          const title = document.createElement("h4"); title.textContent = "AI image recipe — " + result.consentStatus; consentPanel.appendChild(title);
          const recipe = document.createElement("p"); recipe.textContent = result.generationRecipe; consentPanel.appendChild(recipe);
          const notice = document.createElement("p"); notice.textContent = "Optional: permit ALPHA to send the displayed recipe to OpenAI for one image-generation request using "+result.model+". Do not approve if it includes information you do not want shared. Approval does not authorize a payment or start generation. You can withdraw before future processing; withdrawal cannot undo processing already performed."; consentPanel.appendChild(notice);
          const approve = document.createElement("button"); approve.type="button"; approve.textContent="Allow this recipe to be sent to OpenAI";
          const revoke = document.createElement("button"); revoke.type="button"; revoke.textContent="Withdraw AI provider consent";
          if (result.consentStatus === "GRANTED") consentPanel.appendChild(revoke); else consentPanel.appendChild(approve);
          approve.addEventListener("click",()=>consent("GRANT",result)); revoke.addEventListener("click",()=>consent("REVOKE",result));
          status.textContent = "Recipe consent: " + result.consentStatus + ". No image generated and no charge authorized.";
        } catch(e) { if (current === generation) {consentPanel.replaceChildren();status.textContent=e.message;} }
        finally { if(current===generation) consentButton.disabled=false; }
      }
      consentButton.addEventListener("click",()=>consent("REVIEW"));
      const artButton = document.createElement("button"); artButton.type="button"; artButton.textContent="Review prepared artwork"; card.appendChild(artButton);
      const artPanel = document.createElement("section"); card.appendChild(artPanel);
      let artUrl = null, artRetry = null;
      async function artwork(action, prior, rationale) {
        if(!csrf || panel.hidden || artButton.disabled) return;
        const current=generation; artButton.disabled=true;
        for(const button of artPanel.querySelectorAll("button"))button.disabled=true;
        try {
          const send = body=>fetch(endpoint+"/creative-artwork",{method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(body)});
          const payload={campaignId:row.campaignId,action};
          if(action!=="STATUS"){
            const change={candidateId:prior.candidateId,reviewId:prior.reviewId,imageSha256:prior.imageSha256,expectedVersion:prior.approvalVersion,rationale};
            const fingerprint=JSON.stringify({action,...change});
            if(!artRetry || artRetry.fingerprint!==fingerprint) artRetry={fingerprint,key:crypto.randomUUID()};
            Object.assign(payload,change,{requestKey:artRetry.key});
          }
          const response=await send(payload);
          if(!response.ok)throw Error("Artwork is not ready for private review, or its approval state changed.");
          const result=await response.json();
          if(current!==generation || !card.isConnected)return;
          if(result.campaignId!==row.campaignId || !/^[0-9a-f]{64}$/.test(result.imageSha256)
              || !result.candidateId || !result.reviewId || !Number.isInteger(result.approvalVersion)
              || !["HELD","ARTWORK_DOWNLOAD_AVAILABLE"].includes(result.release)
              || (result.release==="HELD" ? result.delivery!=="NOT_SENT" : result.delivery!=="DOWNLOAD_NOT_CONFIRMED" || !result.releaseId)
              || result.dispatchAuthorized!==false
              || result.paymentAuthorized!==false || !["PENDING","APPROVE","WITHHOLD"].includes(result.approval))
            throw Error("Unexpected artwork receipt.");
          const imageResponse=await send({campaignId:row.campaignId,action:"PREVIEW",candidateId:result.candidateId,
            reviewId:result.reviewId,imageSha256:result.imageSha256});
          if(!imageResponse.ok || !imageResponse.headers.get("content-type")?.startsWith("image/png"))throw Error("Artwork preview unavailable.");
          const blob=await imageResponse.blob();
          if(blob.size===0 || blob.size>16*1024*1024)throw Error("Unexpected artwork size.");
          const hash=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",await blob.arrayBuffer())),x=>x.toString(16).padStart(2,"0")).join("");
          if(hash!==result.imageSha256)throw Error("Artwork integrity verification failed.");
          if(current!==generation || !card.isConnected)return;
          if(artUrl)URL.revokeObjectURL(artUrl);
          artUrl=URL.createObjectURL(blob);previewUrls.push(artUrl);
          artPanel.replaceChildren();artRetry=null;
          const title=document.createElement("h4");title.textContent="Private artwork — "+result.approval;artPanel.appendChild(title);
          const image=document.createElement("img");image.src=artUrl;image.alt="Private campaign artwork for review";image.style.maxWidth="100%";
          try{await image.decode();}catch{URL.revokeObjectURL(artUrl);artUrl=null;throw Error("Artwork could not be displayed; approval unavailable.");}
          if(current!==generation || !card.isConnected)return;
          artPanel.appendChild(image);
          const note=document.createElement("p");note.textContent="Review only. Approval is not payment, final commercial delivery, publication or permission to launch advertising.";artPanel.appendChild(note);
          const rationaleInput=document.createElement("textarea");rationaleInput.maxLength=2000;rationaleInput.placeholder="Approval notes or requested changes";rationaleInput.setAttribute("aria-label","Artwork decision notes");artPanel.appendChild(rationaleInput);
          for(const [choice,label] of [["APPROVE","Approve this artwork"],["WITHHOLD","Request changes / withhold approval"]]){
            const button=document.createElement("button");button.type="button";button.textContent=label;
            button.addEventListener("click",()=>{const reason=rationaleInput.value.trim();if(!reason){status.textContent="Enter your decision notes first.";return;}artwork(choice,result,reason);});artPanel.appendChild(button);
          }
          if(result.release==="ARTWORK_DOWNLOAD_AVAILABLE"){
            const download=document.createElement("button");download.type="button";download.textContent="Download final approved artwork";artPanel.appendChild(download);
            download.addEventListener("click",async()=>{
              if(download.disabled || !csrf)return;download.disabled=true;
              try{
                const response=await send({campaignId:row.campaignId,action:"DOWNLOAD",candidateId:result.candidateId,reviewId:result.reviewId,imageSha256:result.imageSha256});
                if(!response.ok || !response.headers.get("content-type")?.startsWith("image/png"))throw Error("Final download not available.");
                const blob=await response.blob();
                if(blob.size===0 || blob.size>16*1024*1024)throw Error("Unexpected final artwork size.");
                const digest=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",await blob.arrayBuffer())),x=>x.toString(16).padStart(2,"0")).join("");
                if(digest!==result.imageSha256 || current!==generation || !card.isConnected)throw Error("Final artwork verification failed.");
                const url=URL.createObjectURL(blob);previewUrls.push(url);
                const link=document.createElement("a");link.href=url;link.download="alpha-artwork-"+row.campaignId+".png";link.textContent="Save verified final PNG";artPanel.appendChild(link);
                status.textContent="Final artwork received by browser. Save the verified PNG.";
              }catch(e){status.textContent=e.message;}finally{download.disabled=false;}
            });
          }
          status.textContent="Artwork decision: "+result.approval+". Campaign remains held.";
        }catch(e){if(current===generation && card.isConnected){artPanel.replaceChildren();if(artUrl){URL.revokeObjectURL(artUrl);artUrl=null;}status.textContent=e.message;}}
        finally{if(current===generation)artButton.disabled=false;}
      }
      artButton.addEventListener("click",()=>artwork("STATUS"));
      const offerButton=document.createElement("button");offerButton.type="button";offerButton.textContent="Review $149 service offer";card.appendChild(offerButton);
      const offerPanel=document.createElement("section");card.appendChild(offerPanel);
      async function serviceOffer(action,prior){
        if(!csrf || offerButton.disabled || panel.hidden)return;
        const current=generation;offerButton.disabled=true;
        for(const button of offerPanel.querySelectorAll("button"))button.disabled=true;
        try{
          const payload={campaignId:row.campaignId,action};
          if(action==="ACCEPT")Object.assign(payload,{offerId:prior.offerId,offerSha256:prior.offerSha256,acceptedExactTerms:true});
          const response=await fetch(endpoint+"/starter-offer",{method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(payload)});
          if(!response.ok)throw Error("Service offer unavailable or no longer current.");
          const result=await response.json();if(current!==generation||!card.isConnected)return;
          if(result.campaignId!==row.campaignId||result.amountCents!==14900||result.currency!=="usd"||!result.offerId
            ||!/^[0-9a-f]{64}$/.test(result.offerSha256)||!["OFFERED","ACCEPTED"].includes(result.status)||result.paymentAuthorized!==false||result.release!=="HELD")throw Error("Unexpected offer receipt.");
          const progressLabels = {
            AWAITING_ACCEPTANCE: "Waiting for your acceptance.",
            AWAITING_PAYMENT_INSTRUCTIONS: "Order accepted. Waiting for ALPHA to issue payment instructions.",
            PAYMENT_INSTRUCTIONS_RECORDED: "Payment instructions recorded by ALPHA. Contact your operator if you have not received them. Do not pay twice.",
            RECONCILIATION_RECORDED: "Payment reconciliation recorded by ALPHA. This is historical evidence, not confirmation of current Stripe payment or refund status.",
            REVIEW_REQUIRED: "Order needs manual review. Contact ALPHA before making any payment."
          };
          if (result.orderProgress !== undefined &&
              (!Object.hasOwn(progressLabels,result.orderProgress)
                || (result.status === "OFFERED") !== (result.orderProgress === "AWAITING_ACCEPTANCE")))
            throw Error("Unexpected order progress.");
          offerPanel.replaceChildren();
          for(const [tag,value] of [["h4","Service offer — USD 149 — "+result.status],["p",result.scope],["p",result.terms]]){
            const node=document.createElement(tag);node.textContent=value;offerPanel.appendChild(node);
          }
          if(result.status==="OFFERED"){
            const accept=document.createElement("button");accept.type="button";accept.textContent="Accept exact service scope and terms ($149)";
            accept.addEventListener("click",()=>{if(window.confirm("Accept the displayed service scope and terms for USD 149? No payment will be taken by this action."))serviceOffer("ACCEPT",result);});offerPanel.appendChild(accept);
          }else{
            const info=document.createElement("p");info.textContent="Accepted order reference: "+result.offerId;offerPanel.appendChild(info);
            const progress=document.createElement("p");progress.setAttribute("role","status");
            progress.textContent=progressLabels[result.orderProgress] || "Order progress unavailable. Contact ALPHA before making any payment.";
            offerPanel.appendChild(progress);
            const refresh=document.createElement("button");refresh.type="button";refresh.textContent="Refresh order progress";
            refresh.addEventListener("click",()=>serviceOffer("STATUS"));offerPanel.appendChild(refresh);
          }
        }catch(e){if(current===generation&&card.isConnected){offerPanel.replaceChildren();status.textContent=e.message;}}
        finally{if(current===generation)offerButton.disabled=false;}
      }
      offerButton.addEventListener("click",()=>serviceOffer("STATUS"));
      list.appendChild(card);
    }
  }
  async function get(path) {
    return fetch(path,{credentials:"same-origin",cache:"no-store",headers:{"Accept":"application/json"}});
  }
  async function load() {
    const current = generation;
    try {
      const session = await get("/api/auth/session");
      if (!session.ok) return;
      const data = await session.json();
      if (!data.isAuthenticated || typeof data.csrfToken !== "string") return;
      const response = await get(endpoint);
      if (!response.ok || current !== generation) return;
      const rows = await response.json();
      if (!Array.isArray(rows) || current !== generation) return;
      csrf = data.csrfToken; render(rows); panel.hidden = false; save.disabled = busy;
    } catch { /* fail closed */ }
  }
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (busy || !csrf || panel.hidden) return;
    const n = name.value.trim(), b = brief.value.trim();
    if (!n || !b) return;
    if (!retry || retry.name !== n || retry.brief !== b)
      retry = {requestKey:crypto.randomUUID(),name:n,brief:b};
    const current = generation; busy = true; save.disabled = true;
    status.textContent = "Saving draft…";
    try {
      const response = await fetch(endpoint,{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(retry)});
      if (current !== generation) return;
      if (!response.ok) {status.textContent = "Draft not accepted. Refresh account status if your access changed. Retry the same draft if needed.";return;}
      const result = await response.json();
      if (result.campaignId !== retry.requestKey || result.status !== "DRAFT") throw new Error("Unexpected receipt");
      retry = null; form.reset(); status.textContent = "Draft saved. No campaign launched and no charges incurred.";
      await load();
    } catch { if (current === generation) status.textContent = "Unable to confirm save. Retry the unchanged draft."; }
    finally { if (current === generation) {busy = false; save.disabled = !csrf;} }
  });
  document.addEventListener("alpha-profile-cleared",clear);
  document.addEventListener("alpha-profile-approved",load);
  clear();
  if (!el("profile-panel").hidden) load();
})();