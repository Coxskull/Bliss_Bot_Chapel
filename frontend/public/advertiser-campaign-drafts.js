(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const panel = el("draft-panel"), form = el("draft-form"), list = el("draft-list");
  const status = el("draft-status"), save = el("draft-save");
  const name = el("draft-name"), brief = el("draft-brief");
  const endpoint = "/api/advertisers/my-campaign-drafts";
  let generation = 0, csrf = null, busy = false, retry = null;
  let previewUrls = [], loadSequence = 0;
  const tools = el("draft-tools"), search = el("draft-search"), refresh = el("draft-refresh");
  const count = el("draft-count"), readback = el("draft-readback");
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  const assetFilter=document.createElement("select");
  assetFilter.setAttribute("aria-label","Filter private artwork library");
  for(const [value,label] of [["ALL","All campaigns"],["RELEASED","Verified available artwork"],["ACKNOWLEDGED","Verified receipt acknowledged"],["REVIEW","Verified artwork pending release"],["UNCHECKED","Artwork not checked"]]){
    const option=document.createElement("option");option.value=value;option.textContent=label;assetFilter.appendChild(option);
  }
  tools.appendChild(assetFilter);
  assetFilter.addEventListener("change",()=>filterCampaigns());
  function filterCampaigns() {
    const q = search.value.trim().toLowerCase();
    const cards = Array.from(list.querySelectorAll("article[data-campaign-search]"));
    let visible = 0;
    for (const card of cards) {
      card.hidden = !card.dataset.campaignSearch.includes(q) || (assetFilter.value!=="ALL" && card.dataset.assetState!==assetFilter.value);
      if (!card.hidden) visible++;
    }
    count.textContent = visible + " of " + cards.length + " saved campaigns shown" +
      (cards.length === 50 ? " (latest 50 only)." : ".");
  }
  function validRows(rows) {
    return Array.isArray(rows) && rows.length <= 50 &&
      rows.every(row => row && uuid.test(row.campaignId || "") &&
        typeof row.name === "string" && row.name.length > 0 && row.name.length <= 256 &&
        typeof row.brief === "string" && row.brief.length <= 4000 &&
        row.status === "DRAFT" && typeof row.createdAt === "string" &&
        Number.isFinite(Date.parse(row.createdAt))) &&
      new Set(rows.map(row => row.campaignId)).size === rows.length;
  }
  function disposePreviews() {
    for (const url of previewUrls) URL.revokeObjectURL(url);
    previewUrls = [];
  }
  function clear() {
    generation++; loadSequence++; csrf = null; busy = false; retry = null;
    assetFilter.value="ALL";tools.hidden = true; search.value = ""; count.textContent = ""; readback.textContent = ""; refresh.disabled = true;
    disposePreviews();
    panel.hidden = true; form.reset(); list.replaceChildren(); status.textContent = "";
    save.disabled = true;
  }
  function render(rows) {
    disposePreviews(); list.replaceChildren();
    if (!rows.length) { list.textContent = "No campaign drafts yet."; filterCampaigns(); return; }
    for (const row of rows) {
      const card = document.createElement("article");
      card.dataset.campaignSearch = (row.name + " " + row.campaignId).toLowerCase();
      card.dataset.assetState="UNCHECKED";
      for (const [tag, text] of [["h3",row.name],["p","Campaign reference: "+row.campaignId],
        ["p","Created: "+new Date(row.createdAt).toLocaleString()],["p",row.brief],["p","Status: "+row.status]]) {
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
          if (current !== generation || !card.isConnected) return;
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
          if (current !== generation || !card.isConnected) return;
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
      const assetSummary=document.createElement("p");assetSummary.textContent="Private artwork: not yet checked.";card.appendChild(assetSummary);
      const checkAsset=document.createElement("button");checkAsset.type="button";checkAsset.textContent="Check artwork availability";card.appendChild(checkAsset);
      checkAsset.addEventListener("click",()=>artwork("STATUS",null,null,true));
      let artUrl = null, artRetry = null;
      async function artwork(action, prior, rationale, metadataOnly=false) {
        if(!csrf || panel.hidden || artButton.disabled) return;
        const current=generation; artButton.disabled=true;checkAsset.disabled=true;
        for(const button of artPanel.querySelectorAll("button"))button.disabled=true;
        try {
          const send = body=>fetch(endpoint+"/creative-artwork",{method:"POST",credentials:"same-origin",cache:"no-store",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(body)});
          const payload={campaignId:row.campaignId,action};
          if(action==="ACKNOWLEDGE"){
            Object.assign(payload,{candidateId:prior.candidateId,reviewId:prior.reviewId,imageSha256:prior.imageSha256,releaseId:prior.releaseId,confirmedReceipt:true});
          }else if(action!=="STATUS"){
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
          if(result.downloadRequestRecorded !== undefined &&
              (typeof result.downloadRequestRecorded !== "boolean" ||
                (result.downloadRequestRecorded ? result.release !== "ARTWORK_DOWNLOAD_AVAILABLE" ||
                   typeof result.downloadRequestedAt !== "string" || !Number.isFinite(Date.parse(result.downloadRequestedAt))
                  : result.downloadRequestedAt != null)))throw Error("Unexpected delivery history.");
          if(result.receiptAcknowledged !== undefined &&
              (typeof result.receiptAcknowledged !== "boolean" ||
                (result.receiptAcknowledged ? !result.downloadRequestRecorded ||
                   typeof result.receiptAcknowledgedAt !== "string" || !Number.isFinite(Date.parse(result.receiptAcknowledgedAt))
                   : result.receiptAcknowledgedAt != null)))throw Error("Unexpected receipt acknowledgment.");
          if((result.downloadRequestRecorded===true && !uuid.test(result.downloadRequestReceiptId||""))
              ||(result.receiptAcknowledged===true && !uuid.test(result.receiptAcknowledgmentId||"")))throw Error("Missing exact receipt reference.");
          card.dataset.assetState=result.receiptAcknowledged?"ACKNOWLEDGED":result.release==="ARTWORK_DOWNLOAD_AVAILABLE"?"RELEASED":"REVIEW";
          assetSummary.textContent="Private artwork: "+({ACKNOWLEDGED:"receipt acknowledged",RELEASED:"available for authorized download",REVIEW:"review only; held"})[card.dataset.assetState]+". Recorded status; refresh before use.";
          filterCampaigns();
          if(metadataOnly){artPanel.replaceChildren();if(artUrl){URL.revokeObjectURL(artUrl);artUrl=null;}return;}
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
          if(result.decisionReceipt!=null){
            const receipt=result.decisionReceipt;
            if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(receipt.decisionId)
              || receipt.version!==result.approvalVersion || receipt.decision!==result.approval
              || !["APPROVE","WITHHOLD"].includes(receipt.decision)
              || typeof receipt.rationale!=="string" || !receipt.rationale.trim() || receipt.rationale.length>2000
              || typeof receipt.recordedAt!=="string" || !Number.isFinite(Date.parse(receipt.recordedAt)))throw Error("Unexpected decision receipt.");
            const summary=document.createElement("section");
            const heading=document.createElement("h5");heading.textContent=receipt.decision==="WITHHOLD"?"Recorded revision request":"Recorded artwork approval";summary.appendChild(heading);
            const date=document.createElement("p");date.textContent="Decision version "+receipt.version+" · "+new Date(receipt.recordedAt).toLocaleString();summary.appendChild(date);
            const notes=document.createElement("p");notes.textContent=receipt.rationale;notes.style.whiteSpace="pre-wrap";summary.appendChild(notes);
            const explanation=document.createElement("p");explanation.textContent=receipt.decision==="WITHHOLD"?"Your requested changes are recorded. A revised creative requires separate preparation and review.":"Your approval applies only to this exact artwork and review. It is not permission to publish or charge.";summary.appendChild(explanation);
            const save=document.createElement("button");save.type="button";save.textContent="Download my decision record";
            save.addEventListener("click",()=>{
              if(current!==generation||!card.isConnected||!summary.isConnected||!csrf||artButton.disabled)return;
              const record={schema:"alpha.advertiser.artwork-decision-receipt.v1",campaignId:result.campaignId,candidateId:result.candidateId,reviewId:result.reviewId,imageSha256:result.imageSha256,...receipt,recordType:"DECISION_ONLY",dispatchAuthorized:false,paymentAuthorized:false};
              const url=URL.createObjectURL(new Blob([JSON.stringify(record,null,2)+"\n"],{type:"application/json"}));
              const link=document.createElement("a");link.href=url;link.download="alpha-artwork-decision-"+receipt.decisionId+".json";link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
            });summary.appendChild(save);artPanel.appendChild(summary);
          }
          const rationaleInput=document.createElement("textarea");rationaleInput.maxLength=2000;rationaleInput.placeholder="Approval notes or requested changes";rationaleInput.setAttribute("aria-label","Artwork decision notes");artPanel.appendChild(rationaleInput);
          for(const [choice,label] of [["APPROVE","Approve this artwork"],["WITHHOLD","Request changes / withhold approval"]]){
            const button=document.createElement("button");button.type="button";button.textContent=label;
            button.addEventListener("click",()=>{const reason=rationaleInput.value.trim();if(!reason){status.textContent="Enter your decision notes first.";return;}artwork(choice,result,reason);});artPanel.appendChild(button);
          }
          const delivery=document.createElement("p");delivery.setAttribute("role","status");
          delivery.textContent=result.release==="HELD" ? "Delivery: held pending release."
            : result.receiptAcknowledged ? "Customer acknowledged receipt on "+new Date(result.receiptAcknowledgedAt).toLocaleString()+". This is a customer statement, not independent proof of file storage."
            : result.downloadRequestRecorded ? "Download requested on "+new Date(result.downloadRequestedAt).toLocaleString()+". Saving the file is not confirmed."
            : "Delivery: artwork available; download completion is not confirmed.";
          artPanel.appendChild(delivery);
          if(result.release==="ARTWORK_DOWNLOAD_AVAILABLE" && result.downloadRequestRecorded){
            const receiptButton=document.createElement("button");receiptButton.type="button";receiptButton.textContent="Export my artwork receipt record";
            receiptButton.addEventListener("click",()=>{
              if(current!==generation||!card.isConnected||!receiptButton.isConnected||!csrf||artButton.disabled)return;
              const record={schema:"alpha.advertiser.artwork-receipt.v1",campaignId:result.campaignId,candidateId:result.candidateId,reviewId:result.reviewId,
                releaseId:result.releaseId,imageSha256:result.imageSha256,downloadRequestReceiptId:result.downloadRequestReceiptId,
                downloadRequestedAt:result.downloadRequestedAt,receiptAcknowledgmentId:result.receiptAcknowledgmentId??null,
                receiptAcknowledgedAt:result.receiptAcknowledgedAt??null,recordType:"CUSTOMER_RECORDED_EVIDENCE",independentDeliveryVerified:false,dispatchAuthorized:false,paymentAuthorized:false};
              const url=URL.createObjectURL(new Blob([JSON.stringify(record,null,2)+"\n"],{type:"application/json"}));
              const link=document.createElement("a");link.href=url;link.download="alpha-artwork-receipt-"+result.releaseId+".json";link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
            });artPanel.appendChild(receiptButton);
          }
          if(result.release==="ARTWORK_DOWNLOAD_AVAILABLE"){
            const download=document.createElement("button");download.type="button";download.textContent="Download final approved artwork";artPanel.appendChild(download);
            const ack=document.createElement("button");ack.type="button";ack.textContent="Confirm I received the final artwork";
            ack.hidden=result.receiptAcknowledged===true;ack.disabled=!result.downloadRequestRecorded;
            ack.addEventListener("click",()=>{if(!ack.disabled&&window.confirm("Confirm that you received this exact final artwork? This records your acknowledgment only; it does not waive support or refund rights."))artwork("ACKNOWLEDGE",result);});
            artPanel.appendChild(ack);
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
                if(!result.receiptAcknowledged){ack.disabled=false;delivery.textContent="Download request recorded; verified PNG received by this browser. Saving the file is not confirmed.";}
                status.textContent="Final artwork received by browser. Save the verified PNG.";
              }catch(e){status.textContent=e.message;}finally{download.disabled=false;}
            });
          }
          status.textContent="Artwork decision: "+result.approval+". Campaign remains held.";
        }catch(e){if(current===generation && card.isConnected){card.dataset.assetState="UNCHECKED";assetSummary.textContent="Private artwork: current availability unverified.";filterCampaigns();artPanel.replaceChildren();if(artUrl){URL.revokeObjectURL(artUrl);artUrl=null;}status.textContent=e.message;}}
        finally{if(current===generation){artButton.disabled=false;checkAsset.disabled=false;}}
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
            ||!uuid.test(result.offerId)||typeof result.scope!=="string"||!result.scope.trim()||result.scope.length>2000
            ||typeof result.terms!=="string"||!result.terms.trim()||result.terms.length>2000
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
          const creativeLabels = {
            NOT_AVAILABLE: "Creative progress is not available for this order.",
            AWAITING_RECIPE: "Creative recipe not yet recorded.",
            RECIPE_RECORDED: "Creative recipe recorded. Current AI consent or preparation checks may still be required.",
            CONSENT_RECORDED: "Current AI consent recorded. No matching image run recorded yet.",
            IMAGE_RUN_RECORDED: "Image production record exists. Completion and quality are not yet confirmed.",
            IMAGE_RECORDED_PENDING_REVIEW: "Generated image recorded. Staff review is pending.",
            STAFF_REVISION_RECORDED: "Staff requested a creative revision. The revised artwork has not yet been confirmed.",
            ADVERTISER_REVIEW_PENDING: "Staff review passed. Open private artwork review to check the current version.",
            ADVERTISER_REVISION_RECORDED: "Advertiser revision request recorded. A new creative version requires separate preparation and review.",
            ADVERTISER_APPROVAL_RECORDED: "Advertiser artwork approval recorded. Check private artwork review for the separate release and delivery status."
          };
          if (result.creativeProgress !== undefined && !Object.hasOwn(creativeLabels,result.creativeProgress))
            throw Error("Unexpected creative progress.");
          offerPanel.replaceChildren();
          for(const [tag,value] of [["h4","Service offer — USD 149 — "+result.status],["p",result.scope],["p",result.terms]]){
            const node=document.createElement(tag);node.textContent=value;offerPanel.appendChild(node);
          }
          const creative = document.createElement("p");creative.textContent=creativeLabels[result.creativeProgress] || "Creative progress unavailable.";
          offerPanel.appendChild(creative);
          if(result.status==="OFFERED"){
            const accept=document.createElement("button");accept.type="button";accept.textContent="Accept exact service scope and terms ($149)";
            accept.addEventListener("click",()=>{if(window.confirm("Accept the displayed service scope and terms for USD 149? No payment will be taken by this action."))serviceOffer("ACCEPT",result);});offerPanel.appendChild(accept);
          }else{
            const info=document.createElement("p");info.textContent="Accepted order reference: "+result.offerId;offerPanel.appendChild(info);
            const progress=document.createElement("p");progress.setAttribute("role","status");
            progress.textContent=progressLabels[result.orderProgress] || "Order progress unavailable. Contact ALPHA before making any payment.";
            offerPanel.appendChild(progress);
            const receipt=document.createElement("button");receipt.type="button";receipt.textContent="Save order acceptance record (TXT)";
            receipt.addEventListener("click",()=>{
              if(current!==generation||!csrf||panel.hidden||!card.isConnected||!receipt.isConnected||offerButton.disabled)return;
              const content=[
                "ALPHA GLOBAL MEDIA LLC — ORDER ACCEPTANCE RECORD",
                "Not a tax invoice, payment receipt, payment instruction or delivery confirmation.",
                "Order reference: "+result.offerId,
                "Campaign reference: "+row.campaignId,
                "Campaign name: "+row.name,
                "Offer fingerprint (SHA-256): "+result.offerSha256,
                "Accepted service price: USD 149.00",
                "Order status: ACCEPTED",
                "Readback saved (UTC): "+new Date().toISOString(),
                "",
                "Accepted scope:",result.scope,"","Accepted terms:",result.terms,
                "","Recorded order progress:",progress.textContent,
                "","Recorded creative progress:",creative.textContent,
                "","This record does not confirm current payment, refund, artwork delivery or campaign authorization."
              ].join("\n");
              const url=URL.createObjectURL(new Blob([content],{type:"text/plain;charset=utf-8"}));
              previewUrls.push(url);
              const link=document.createElement("a");link.href=url;
              link.download="alpha-order-"+result.offerId+".txt";link.textContent="Download order record";
              receipt.replaceWith(link);
            });
            offerPanel.appendChild(receipt);
            const help=document.createElement("a");help.href="/support.html";help.textContent="Contact ALPHA support about this order";offerPanel.appendChild(help);
            const refresh=document.createElement("button");refresh.type="button";refresh.textContent="Refresh order progress";
            refresh.addEventListener("click",()=>serviceOffer("STATUS"));offerPanel.appendChild(refresh);
          }
        }catch(e){if(current===generation&&card.isConnected){offerPanel.replaceChildren();status.textContent=e.message;}}
        finally{if(current===generation)offerButton.disabled=false;}
      }
      offerButton.addEventListener("click",()=>serviceOffer("STATUS"));
      list.appendChild(card);
    }
    filterCampaigns();
  }
  async function get(path) {
    return fetch(path,{credentials:"same-origin",cache:"no-store",headers:{"Accept":"application/json"}});
  }
  async function load() {
    const current = generation, sequence = ++loadSequence;
    const stillCurrent = () => current === generation && sequence === loadSequence && !el("profile-panel").hidden;
    csrf = null; save.disabled = true; refresh.disabled = true;
    disposePreviews(); list.replaceChildren(); count.textContent = "";
    readback.textContent = "Loading saved campaigns…";
    try {
      const session = await get("/api/auth/session");
      if (!stillCurrent()) return;
      if (!session.ok) throw Error("session");
      const data = await session.json();
      if (!stillCurrent()) return;
      if (!data.isAuthenticated || typeof data.csrfToken !== "string" || !data.csrfToken) throw Error("session");
      panel.hidden = false; tools.hidden = false;
      const response = await get(endpoint);
      if (!stillCurrent()) return;
      if (!response.ok) throw Error("readback");
      const rows = await response.json();
      if (!stillCurrent()) return;
      if (!validRows(rows)) throw Error("readback");
      csrf = data.csrfToken; render(rows); save.disabled = busy;
      readback.textContent = "Saved campaigns refreshed from your account.";
    } catch {
      if (!stillCurrent()) return;
      csrf = null; save.disabled = true;
      disposePreviews(); list.replaceChildren(); count.textContent = "";
      panel.hidden = false; tools.hidden = false;
      readback.textContent = "Unable to verify saved campaigns. Refresh saved campaigns or sign in again. Saving is paused until verification succeeds.";
    } finally { if (stillCurrent()) refresh.disabled = busy; }
  }
  search.addEventListener("input",filterCampaigns);
  refresh.addEventListener("click",()=>{if(!busy)load();});
  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (busy || !csrf || panel.hidden) return;
    const n = name.value.trim(), b = brief.value.trim();
    if (!n || !b) return;
    if (!retry || retry.name !== n || retry.brief !== b)
      retry = {requestKey:crypto.randomUUID(),name:n,brief:b};
    const current = generation; busy = true; save.disabled = true; refresh.disabled = true;
    status.textContent = "Saving draft…";
    try {
      const response = await fetch(endpoint,{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(retry)});
      if (current !== generation) return;
      if (!response.ok) {status.textContent = "Draft not accepted. Refresh account status if your access changed. Retry the same draft if needed.";return;}
      const result = await response.json();
      if (current !== generation || !retry) return;
      if (result.campaignId !== retry.requestKey || result.status !== "DRAFT") throw new Error("Unexpected receipt");
      retry = null; form.reset(); status.textContent = "Draft saved. No campaign launched and no charges incurred.";
      await load();
    } catch { if (current === generation) status.textContent = "Unable to confirm save. Retry the unchanged draft."; }
    finally { if (current === generation) {busy = false; save.disabled = !csrf; refresh.disabled = false;} }
  });
  document.addEventListener("alpha-profile-cleared",clear);
  document.addEventListener("alpha-profile-approved",load);
  clear();
  if (!el("profile-panel").hidden) load();
})();