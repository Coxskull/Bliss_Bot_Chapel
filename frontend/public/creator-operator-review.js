(() => {
  "use strict";
  const status = document.getElementById("review-status"), queue = document.getElementById("queue"), load = document.getElementById("load");
  const node = (tag, text) => { const e = document.createElement(tag); e.textContent = text; return e; };
  let busy = false;
  async function session() {
    const r = await fetch("/api/auth/session", { credentials: "same-origin", cache: "no-store" });
    if (!r.ok) throw Error("Operator session unavailable.");
    const s = await r.json();
    if (!s.isAuthenticated || !s.csrfToken) throw Error("Sign in as an approved operator.");
    return s;
  }
  async function refresh() {
    if (busy) return;
    busy = true; load.disabled = true; queue.replaceChildren();
    try {
      await session();
      const r = await fetch("/api/creators/operator-review", { credentials: "same-origin", cache: "no-store" });
      if (!r.ok) throw Error("Creator review unavailable or operator access denied.");
      const rows = await r.json();
      if (!Array.isArray(rows)) throw Error("Invalid queue response.");
      status.textContent = rows.length + " application(s) awaiting registration or channel review.";
      for (const item of rows) {
        const section = document.createElement("section"); section.className = "profile";
        for (const [label, value] of [["Creator", item.displayName], ["Country", item.countryCode], ["Submitted channel", item.channelUrl], ["Receipt", item.receiptId], ["Privacy", item.privacyNoticeVersion], ["Terms", item.termsVersion], ["Authority", item.authorityStatus]]) section.append(node("p", label + ": " + String(value ?? "")));
        const check = label => { const wrap = node("label", label); const input = document.createElement("input"); input.type = "checkbox"; wrap.prepend(input); section.append(wrap, document.createElement("br")); return input; };
        if (item.status === "ACTIVE" && item.authorityStatus === "UNVERIFIED") {
          section.append(node("p", "Open the exact submitted channel on the legitimate platform. Independently observe the current ALPHA proof in the channel description. Do not accept a screenshot, password, or proof on another channel."));
          const label = node("label", "Observed ALPHA channel proof"); const code = document.createElement("input");
          code.type = "text"; code.maxLength = 38; code.autocomplete = "off"; label.append(code); section.append(label, document.createElement("br"));
          const control = check("I independently observed the current proof on the exact submitted channel and checked for duplicate channel claims.");
          const consent = check("I reviewed the current recorded privacy and terms consent.");
          const verify = node("button", "Verify channel control"); verify.type = "button"; verify.disabled = true;
          const update = () => { verify.disabled = !control.checked || !consent.checked || !/^ALPHA-[0-9A-F]{32}$/.test(code.value.trim()) || item.processingConsent !== true || item.termsAccepted !== true; };
          for (const element of [control, consent]) element.addEventListener("change", update);
          code.addEventListener("input", update);
          verify.addEventListener("click", async () => {
            if (busy || verify.disabled) return;
            busy = true; load.disabled = true; verify.disabled = true;
            try {
              const s = await session();
              const r = await fetch("/api/creators/operator-review/verify-channel", { method: "POST", credentials: "same-origin",
                headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": s.csrfToken },
                body: JSON.stringify({ receiptId: item.receiptId, creatorId: item.creatorId, observedCode: code.value.trim(), channelControlReviewed: control.checked, consentReviewed: consent.checked }) });
              if (!r.ok) throw Error("Verification refused. Check the exact channel, current proof and approval status.");
              const result = await r.json();
              if (result.authorityStatus !== "VERIFIED" || result.workspaceAuthorized !== true || result.commercialAuthorized !== false) throw Error("Unexpected verification result. Refresh.");
              section.remove(); status.textContent = "Channel control verified. Private profile is enabled; commercial rights remain unavailable.";
            } catch (e) { status.textContent = e.message; update(); }
            finally { busy = false; load.disabled = false; }
          });
          section.append(verify); queue.append(section); continue;
        }
        const profile = check("I have manually reviewed this creator profile.");
        const consent = check("I have reviewed the recorded required consent and terms.");
        const approve = node("button", "Approve registration only"); approve.type = "button"; approve.disabled = true;
        const update = () => { approve.disabled = !profile.checked || !consent.checked || item.processingConsent !== true || item.termsAccepted !== true; };
        profile.addEventListener("change", update); consent.addEventListener("change", update);
        approve.addEventListener("click", async () => {
          if (busy || approve.disabled) return;
          busy = true; load.disabled = true; approve.disabled = true;
          try {
            const s = await session();
            const r = await fetch("/api/creators/operator-review", { method: "POST", credentials: "same-origin",
              headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": s.csrfToken },
              body: JSON.stringify({ receiptId: item.receiptId, creatorId: item.creatorId, profileReviewed: profile.checked, consentReviewed: consent.checked }) });
            if (!r.ok) throw Error("Approval refused. Refresh and review the exact receipt.");
            const result = await r.json();
            if (result.status !== "ACTIVE" || result.workspaceAuthorized !== false) throw Error("Unexpected review result. Refresh.");
            section.remove(); status.textContent = "Registration approved. Channel verification and workspace access remain separate.";
          } catch (e) { status.textContent = e.message; update(); }
          finally { busy = false; load.disabled = false; }
        });
        section.append(approve); queue.append(section);
      }
    } catch (e) { status.textContent = e.message; }
    finally { busy = false; load.disabled = false; }
  }
  const inventoryLoad = document.getElementById("load-inventory");
  const inventoryQueue = document.getElementById("inventory-review-queue");
  const inventoryStatus = document.getElementById("inventory-review-status");
  const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  const types = ["PRE_ROLL","MID_ROLL","POST_ROLL","LOWER_THIRD","PERIMETER_OVERLAY","CORNER_OVERLAY","ROTATING_OVERLAY","SPONSORED_SEGMENT"];
  function clearInventory() { inventoryQueue.replaceChildren(); }
  async function loadInventory() {
    if (busy) return;
    busy = true; load.disabled = true; inventoryLoad.disabled = true; clearInventory();
    try {
      await session();
      const response = await fetch("/api/creators/operator-review/inventory",{credentials:"same-origin",cache:"no-store",redirect:"error"});
      if (!response.ok) throw Error("Inventory review unavailable or access denied.");
      const result = await response.json();
      if (result.bookingAuthorized !== false || result.paymentAuthorized !== false || result.deliveryAuthorized !== false
          || !Array.isArray(result.items) || result.items.length > 50
          || new Set(result.items.map(x => x?.slotId)).size !== result.items.length
          || result.items.some(x => !x || ![x.slotId,x.creatorId,x.contentItemId,x.receiptId].every(v => guid.test(v))
              || x.isAvailable !== false || ![x.creatorName,x.channelUrl,x.contentTitle,x.contentType,x.slotType].every(v => typeof v === "string")
              || ![x.startSecond,x.durationSeconds].every(v => v === null || Number.isSafeInteger(v))))
        throw Error("Invalid inventory review response.");
      inventoryStatus.textContent = result.items.length + " unavailable slot(s) returned. No automatic activation.";
      for (const row of result.items) {
        const card = document.createElement("section"); card.className = "profile";
        for (const [label,value] of [["Creator",row.creatorName],["Channel",row.channelUrl],["Content",row.contentTitle],
          ["Format",row.slotType],["Start (seconds)",row.startSecond],["Duration (seconds)",row.durationSeconds],["Slot reference",row.slotId]])
          card.append(node("p",label+": "+String(value??"Not recorded")));
        const eligible = types.includes(row.slotType) && Number.isSafeInteger(row.startSecond) && row.startSecond>=0 && row.startSecond<=86400
          && Number.isSafeInteger(row.durationSeconds) && row.durationSeconds>=1 && row.durationSeconds<=3600;
        if (!eligible) {card.append(node("p","Incomplete or unsupported slot. Keep unavailable; request correction."));inventoryQueue.append(card);continue;}
        const checks = ["I independently confirmed the creator currently wants this exact slot available.",
          "I reviewed commercial content and placement rights.","I reviewed this slot's format, timing and capacity.",
          "I reviewed current consent and terms."].map(label=>{
            const wrap=node("label",label), input=document.createElement("input");input.type="checkbox";wrap.prepend(input);card.append(wrap,document.createElement("br"));return input;
          });
        const approve=node("button","Approve availability for future matching");approve.type="button";approve.disabled=true;
        const update=()=>{approve.disabled=!checks.every(x=>x.checked);};
        for(const input of checks)input.addEventListener("change",update);
        approve.addEventListener("click",async()=>{
          if(busy||approve.disabled||!card.isConnected)return;
          if(!window.confirm("Mark this reviewed slot available for future matching? No booking, payment or campaign delivery will be authorized."))return;
          busy=true;load.disabled=true;inventoryLoad.disabled=true;approve.disabled=true;
          try{
            const s=await session();
            const payload={slotId:row.slotId,creatorId:row.creatorId,contentItemId:row.contentItemId,receiptId:row.receiptId,
              contentTitle:row.contentTitle,channelUrl:row.channelUrl,slotType:row.slotType,startSecond:row.startSecond,durationSeconds:row.durationSeconds,
              creatorPermissionReviewed:checks[0].checked,rightsReviewed:checks[1].checked,inventoryFitReviewed:checks[2].checked,currentConsentReviewed:checks[3].checked};
            const r=await fetch("/api/creators/operator-review/inventory",{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
              headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},body:JSON.stringify(payload)});
            if(!r.ok)throw Error("Review refused or no longer current.");
            const v=await r.json();
            if(v.slotId!==row.slotId||v.isAvailable!==true||v.existingPlacementsChanged!==false||v.bookingAuthorized!==false||v.paymentAuthorized!==false||v.deliveryAuthorized!==false)
              throw Error("Unexpected review receipt.");
            clearInventory();inventoryStatus.textContent="Availability reviewed and recorded. Reload the current queue before another decision.";
          }catch{clearInventory();inventoryStatus.textContent="Availability change not confirmed. Reload and review current records.";}
          finally{busy=false;load.disabled=false;inventoryLoad.disabled=false;}
        });
        card.append(approve);inventoryQueue.append(card);
      }
    }catch(e){clearInventory();inventoryStatus.textContent=e.message;}
    finally{busy=false;load.disabled=false;inventoryLoad.disabled=false;}
  }
  const placementInput=document.getElementById("invitation-placement"),previewButton=document.getElementById("invitation-preview");
  const invitationReview=document.getElementById("invitation-review"),invitationStatus=document.getElementById("invitation-review-status");
  placementInput?.addEventListener("input",()=>invitationReview.replaceChildren());
  previewButton?.addEventListener("click",async()=>{
    if(busy||!guid.test(placementInput.value.trim()))return;
    busy=true;previewButton.disabled=true;placementInput.disabled=true;invitationReview.replaceChildren();
    const placementId=placementInput.value.trim().toLowerCase();
    try{
      await session();
      const response=await fetch("/api/creators/operator-review/campaign-invitation?placementId="+encodeURIComponent(placementId),{credentials:"same-origin",cache:"no-store",redirect:"error"});
      if(!response.ok)throw Error("No currently eligible placement.");
      const result=await response.json(),row=result.placement;
      if(!row||row.placementId!==placementId||!/^[a-f0-9]{64}$/.test(result.digest)||result.bookingAuthorized!==false||result.paymentAuthorized!==false||result.deliveryAuthorized!==false)
        throw Error("Unexpected placement response.");
      const card=document.createElement("section");
      for(const [label,value] of [["Campaign",row.campaignName],["Content",row.contentTitle],["Slot",row.slotType],["Creator ID",row.creatorId],["Advertiser ID",row.advertiserId],["Start",row.startSecond],["Duration",row.durationSeconds]])
        card.append(node("p",label+": "+String(value??"Not recorded")));
      const checks=["I independently confirmed the advertiser permits sharing this exact invitation.","I confirmed permission to contact this creator through their workspace.","I reviewed this exact planned placement and its availability."].map(label=>{
        const wrap=node("label",label),input=document.createElement("input");input.type="checkbox";wrap.prepend(input);card.append(wrap,document.createElement("br"));return input;});
      const issue=node("button","Issue planning invitation");issue.type="button";issue.disabled=true;
      for(const input of checks)input.addEventListener("change",()=>{issue.disabled=!checks.every(x=>x.checked);});
      issue.addEventListener("click",async()=>{
        if(busy||!card.isConnected||issue.disabled||placementInput.value.trim().toLowerCase()!==placementId)return;
        if(!window.confirm("Issue this non-binding invitation to the verified creator's private workspace?"))return;
        busy=true;issue.disabled=true;previewButton.disabled=true;placementInput.disabled=true;
        try{
          const s=await session();
          const r=await fetch("/api/creators/operator-review/campaign-invitation",{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},body:JSON.stringify({placementId,digest:result.digest,advertiserPermissionReviewed:checks[0].checked,creatorContactPermissionReviewed:checks[1].checked,placementReviewed:checks[2].checked})});
          if(!r.ok)throw Error("Invitation refused");
          const receipt=await r.json();
          if(receipt.placementId!==placementId||!guid.test(receipt.invitationId)||receipt.invitationRecorded!==true||receipt.bookingAuthorized!==false||receipt.paymentAuthorized!==false||receipt.deliveryAuthorized!==false)throw Error("Invalid receipt");
          invitationStatus.textContent="Invitation recorded: "+receipt.invitationId;
        }catch{invitationStatus.textContent="Invitation not confirmed. Reload exact placement before acting.";}
        finally{invitationReview.replaceChildren();busy=false;previewButton.disabled=false;placementInput.disabled=false;}
      });
      card.append(issue);invitationReview.append(card);invitationStatus.textContent="Review current placement and permissions.";
    }catch(e){invitationReview.replaceChildren();invitationStatus.textContent=e.message;}
    finally{busy=false;previewButton.disabled=false;placementInput.disabled=false;}
  });
  const compInvitation=document.getElementById("compensation-invitation"),compIllustration=document.getElementById("compensation-illustration");
  const compPreview=document.getElementById("compensation-preview"),compReview=document.getElementById("compensation-review"),compStatus=document.getElementById("compensation-status");
  const clearComp=()=>compReview?.replaceChildren();
  for(const input of [compInvitation,compIllustration])input?.addEventListener("input",clearComp);
  compPreview?.addEventListener("click",async()=>{
    if(busy||!guid.test(compInvitation.value.trim())||!guid.test(compIllustration.value.trim()))return;
    const invitationId=compInvitation.value.trim().toLowerCase(),illustrationId=compIllustration.value.trim().toLowerCase();
    const lock=locked=>{compPreview.disabled=locked;compInvitation.disabled=locked;compIllustration.disabled=locked;};
    busy=true;lock(true);clearComp();
    try{
      await session();
      const url="/api/creators/operator-review/compensation-proposal?invitationId="+encodeURIComponent(invitationId)+"&illustrationId="+encodeURIComponent(illustrationId);
      const r=await fetch(url,{credentials:"same-origin",cache:"no-store",redirect:"error"});
      if(!r.ok)throw Error("No current eligible compensation proposal.");
      const v=await r.json(),row=v.proposal;
      if(!row||row.invitationId!==invitationId||row.illustrationId!==illustrationId||!guid.test(row.creatorId)||
        !guid.test(row.placementId)||!guid.test(row.quoteVersionId)||!Number.isFinite(row.creatorAmount)||row.creatorAmount<=0||
        !/^[A-Z]{3}$/.test(row.currencyCode)||!/^[a-f0-9]{64}$/.test(v.digest)||v.paymentAuthorized!==false||v.deliveryAuthorized!==false)
        throw Error("Unexpected compensation response.");
      const card=document.createElement("section");
      for(const [label,value] of [["Creator",row.creatorId],["Placement",row.placementId],["Accepted quote version",row.quoteVersionId],["Proposed creator allocation",row.currencyCode+" "+String(row.creatorAmount)]])card.append(node("p",label+": "+value));
      const checks=["I reviewed the exact current Economics allocation and its currency.","I independently confirmed permission to share this proposal with the creator."].map(label=>{
        const wrap=node("label",label),input=document.createElement("input");input.type="checkbox";wrap.prepend(input);card.append(wrap,document.createElement("br"));return input;});
      const publish=node("button","Publish private compensation proposal");publish.type="button";publish.disabled=true;
      for(const input of checks)input.addEventListener("change",()=>{publish.disabled=!checks.every(x=>x.checked);});
      publish.addEventListener("click",async()=>{
        if(busy||publish.disabled||!card.isConnected||compInvitation.value.trim().toLowerCase()!==invitationId||compIllustration.value.trim().toLowerCase()!==illustrationId)return;
        if(!window.confirm("Publish this non-binding allocation? No payment, contract or delivery will be authorized."))return;
        busy=true;lock(true);publish.disabled=true;
        try{
          const s=await session();
          const response=await fetch("/api/creators/operator-review/compensation-proposal",{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},
            body:JSON.stringify({invitationId,illustrationId,digest:v.digest,allocationReviewed:checks[0].checked,permissionToShareReviewed:checks[1].checked})});
          if(!response.ok)throw Error("Publication refused");
          const receipt=await response.json();
          if(receipt.invitationId!==invitationId||!guid.test(receipt.proposalId)||receipt.proposalRecorded!==true||receipt.paymentAuthorized!==false||receipt.deliveryAuthorized!==false)throw Error("Invalid receipt");
          compStatus.textContent="Compensation proposal recorded: "+receipt.proposalId;
        }catch{compStatus.textContent="Publication not confirmed. Reload the exact proposal before acting.";}
        finally{clearComp();busy=false;lock(false);}
      });
      card.append(publish);
      if(v.canRecordAgreementEvidence===true && v.allocationAcknowledged===true){
        const area=document.createElement("section");
        area.append(node("h3","Record separately signed agreement evidence"),node("p","Review the signed agreement outside this workspace. Record only its SHA-256 fingerprint. This is a human attestation, not electronic signature verification or payment authorization."));
        const label=node("label","Signed document SHA-256");const hash=document.createElement("input");hash.maxLength=64;hash.autocomplete="off";label.append(hash);area.append(label,document.createElement("br"));
        const reviewChecks=["I reviewed the signed agreement.","I reviewed the parties and exact placement scope.","I reviewed compensation and payment terms against this allocation.","I reviewed cancellation and commercial rights."].map(t=>{
          const wrap=node("label",t),input=document.createElement("input");input.type="checkbox";wrap.prepend(input);area.append(wrap,document.createElement("br"));return input;});
        const record=node("button","Record agreement evidence");record.type="button";record.disabled=true;
        const update=()=>{record.disabled=!/^[a-f0-9]{64}$/.test(hash.value.trim())||!reviewChecks.every(x=>x.checked);};
        hash.addEventListener("input",update);for(const input of reviewChecks)input.addEventListener("change",update);
        record.addEventListener("click",async()=>{
          if(busy||record.disabled||!card.isConnected||compInvitation.value.trim().toLowerCase()!==invitationId||compIllustration.value.trim().toLowerCase()!==illustrationId)return;
          if(!window.confirm("Record this human-reviewed signed document fingerprint? No payment or delivery is authorized."))return;
          busy=true;lock(true);record.disabled=true;
          try{
            const s=await session();
            const r=await fetch("/api/creators/operator-review/agreement-evidence",{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
              headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},
              body:JSON.stringify({invitationId,illustrationId,digest:v.digest,documentSha256:hash.value.trim(),signedAgreementReviewed:reviewChecks[0].checked,partiesAndScopeReviewed:reviewChecks[1].checked,compensationAndPaymentTermsReviewed:reviewChecks[2].checked,cancellationAndRightsReviewed:reviewChecks[3].checked})});
            if(!r.ok)throw Error("Agreement evidence refused");
            const receipt=await r.json();
            if(!guid.test(receipt.agreementEvidenceId)||!guid.test(receipt.proposalId)||receipt.agreementEvidenceRecorded!==true||receipt.signatureVerifiedBySystem!==false||receipt.payable!==false||receipt.bookingAuthorized!==false||receipt.paymentAuthorized!==false||receipt.deliveryAuthorized!==false)throw Error("Unexpected receipt");
            compStatus.textContent="Agreement evidence recorded: "+receipt.agreementEvidenceId;
          }catch{compStatus.textContent="Evidence not confirmed. Reload the current allocation before acting.";}
          finally{clearComp();busy=false;lock(false);}
        });area.append(record);card.append(area);
      }
      compReview.append(card);compStatus.textContent="Review the proposed allocation.";
    }catch(e){clearComp();compStatus.textContent=e.message;}
    finally{busy=false;lock(false);}
  });
  window.addEventListener("pagehide",clearComp);
  const historyInput=document.getElementById("allocation-history-id"),historyPreview=document.getElementById("allocation-history-preview");
  const historyReview=document.getElementById("allocation-history-review"),historyStatus=document.getElementById("allocation-history-review-status");
  const clearHistory=()=>historyReview?.replaceChildren();
  historyInput?.addEventListener("input",clearHistory);
  historyPreview?.addEventListener("click",async()=>{
    if(busy||!guid.test(historyInput.value.trim()))return;
    const historyId=historyInput.value.trim().toLowerCase(),lock=locked=>{historyPreview.disabled=locked;historyInput.disabled=locked;};
    busy=true;lock(true);clearHistory();
    try{
      await session();
      const r=await fetch("/api/creators/operator-review/allocation-history?historyId="+encodeURIComponent(historyId),{credentials:"same-origin",cache:"no-store",redirect:"error"});
      if(!r.ok)throw Error("No current eligible allocation record.");
      const v=await r.json(),row=v.allocation;
      if(!row||row.historyId!==historyId||!guid.test(row.creatorId)||!guid.test(row.placementId)||!/^[A-Z]{3}$/.test(row.currencyCode)||
        ![row.contractedAmount,row.creatorAllocation,row.alphaAllocation,row.otherAllocation].every(n=>Number.isFinite(n)&&n>=0)||
        row.creatorAllocation<=0||! /^[a-f0-9]{64}$/.test(v.digest)||v.payable!==false||v.settlementVerified!==false)throw Error("Invalid allocation response.");
      const card=document.createElement("section"),money=n=>row.currencyCode+" "+String(n);
      for(const [label,value] of [["Campaign",row.campaignName],["Placement",row.placementId],["Creator",row.creatorId],["Historical contracted amount",money(row.contractedAmount)],
        ["Creator allocation",money(row.creatorAllocation)],["ALPHA allocation",money(row.alphaAllocation)],["Other authorized allocation",money(row.otherAllocation)],["Measurement as of",row.measurementAsOf]])
        card.append(node("p",label+": "+String(value)));
      const checks=["I reviewed the exact historical allocation and its lineage.","I confirmed permission to disclose the creator's allocation privately."].map(t=>{
        const wrap=node("label",t),input=document.createElement("input");input.type="checkbox";wrap.prepend(input);card.append(wrap,document.createElement("br"));return input;});
      const publish=node("button","Share creator allocation history");publish.type="button";publish.disabled=true;
      for(const input of checks)input.addEventListener("change",()=>{publish.disabled=!checks.every(x=>x.checked);});
      publish.addEventListener("click",async()=>{
        if(busy||!card.isConnected||publish.disabled||historyInput.value.trim().toLowerCase()!==historyId)return;
        if(!window.confirm("Share this historical creator allocation? No settlement or payable will be created."))return;
        busy=true;lock(true);publish.disabled=true;
        try{
          const s=await session();
          const r=await fetch("/api/creators/operator-review/allocation-history",{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},
            body:JSON.stringify({historyId,digest:v.digest,allocationReviewed:checks[0].checked,permissionToShareReviewed:checks[1].checked})});
          if(!r.ok)throw Error("Sharing refused.");
          const receipt=await r.json();
          if(receipt.historyId!==historyId||!guid.test(receipt.publicationId)||receipt.shared!==true||receipt.payable!==false||receipt.settlementVerified!==false)throw Error("Unexpected receipt.");
          historyStatus.textContent="Historical allocation shared: "+receipt.publicationId;
        }catch{historyStatus.textContent="Sharing not confirmed. Reload the exact record before acting.";}
        finally{clearHistory();busy=false;lock(false);}
      });
      card.append(publish);historyReview.append(card);historyStatus.textContent="Review the current historical allocation.";
    }catch(e){clearHistory();historyStatus.textContent=e.message;}
    finally{busy=false;lock(false);}
  });
  window.addEventListener("pagehide",clearHistory);
  const settlementInput=document.getElementById("settlement-history-id"),settlementPreview=document.getElementById("settlement-preview");
  const settlementReview=document.getElementById("settlement-review"),settlementStatus=document.getElementById("settlement-status");
  const clearSettlement=()=>settlementReview?.replaceChildren();
  settlementInput?.addEventListener("input",clearSettlement);
  settlementPreview?.addEventListener("click",async()=>{
    if(busy||!guid.test(settlementInput.value.trim()))return;
    const historyId=settlementInput.value.trim().toLowerCase(),lock=locked=>{settlementInput.disabled=locked;settlementPreview.disabled=locked;};
    busy=true;lock(true);clearSettlement();
    try{
      await session();
      const response=await fetch("/api/creators/operator-review/settlement-evidence?historyId="+encodeURIComponent(historyId),{credentials:"same-origin",cache:"no-store",redirect:"error"});
      if(!response.ok)throw Error("No eligible current settlement record. Review the allocation and agreement evidence.");
      const v=await response.json(),row=v.settlement?.allocation;
      if(!row||row.historyId!==historyId||!guid.test(row.placementId)||!guid.test(v.settlement.agreementEvidenceId)||
        !Number.isFinite(row.creatorAllocation)||row.creatorAllocation<=0||!/^[A-Z]{3}$/.test(row.currencyCode)||
        !/^[a-f0-9]{64}$/.test(v.digest)||v.transferAuthorized!==false||v.providerVerified!==false||v.payable!==false||
        typeof v.existingEvidence!=="boolean")throw Error("Unexpected settlement response.");
      const card=document.createElement("section");for(const [label,value] of [["Campaign",row.campaignName],["Placement",row.placementId],
        ["Exact creator allocation",row.currencyCode+" "+String(row.creatorAllocation)],["Agreement evidence",v.settlement.agreementEvidenceId]])
        card.append(node("p",label+": "+String(value)));
      if(v.existingEvidence){card.append(node("p","A settlement evidence record already exists. Do not record or pay again; review any discrepancies separately."));settlementReview.append(card);settlementStatus.textContent="Existing evidence found.";return;}
      const field=(label,type,max)=>{const wrap=node("label",label),input=document.createElement("input");input.type=type;input.autocomplete="off";if(max)input.maxLength=max;wrap.append(input);card.append(wrap,document.createElement("br"));return input;};
      const transfer=field("Provider-qualified transfer reference SHA-256","text",64),documentHash=field("Transfer evidence document SHA-256","text",64),completed=field("Completed transfer date and time (local time)","datetime-local");
      completed.step="1";
      const checks=["I independently reviewed completed campaign fulfillment.","I independently verified the correct creator recipient.","I reviewed an actual completed external transfer for the exact full amount and currency shown.","I checked the transfer has not been reversed or refunded.","I checked that this transfer and placement have not already been reconciled."].map(label=>{
        const wrap=node("label",label),input=document.createElement("input");input.type="checkbox";wrap.prepend(input);card.append(wrap,document.createElement("br"));return input;});
      const record=node("button","Record completed-transfer evidence");record.type="button";record.disabled=true;
      const update=()=>{record.disabled=!/^[a-f0-9]{64}$/.test(transfer.value.trim())||!/^[a-f0-9]{64}$/.test(documentHash.value.trim())||!Number.isFinite(Date.parse(completed.value))||Date.parse(completed.value)>Date.now()||!checks.every(x=>x.checked);};
      for(const input of [transfer,documentHash,completed])input.addEventListener("input",update);
      for(const input of checks)input.addEventListener("change",update);
      record.addEventListener("click",async()=>{
        if(busy||record.disabled||!card.isConnected||settlementInput.value.trim().toLowerCase()!==historyId)return;
        if(!window.confirm("Record evidence of the external transfer already completed? This does not send money or independently verify the provider."))return;
        busy=true;lock(true);record.disabled=true;
        try{
          const s=await session();
          const r=await fetch("/api/creators/operator-review/settlement-evidence",{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",
            headers:{"Content-Type":"application/json","X-CSRF-TOKEN":s.csrfToken},
            body:JSON.stringify({historyId,digest:v.digest,amount:row.creatorAllocation,currencyCode:row.currencyCode,transferReferenceSha256:transfer.value.trim(),
              evidenceDocumentSha256:documentHash.value.trim(),completedAt:new Date(completed.value).toISOString(),fulfillmentReviewed:checks[0].checked,
              recipientReviewed:checks[1].checked,completedTransferReviewed:checks[2].checked,noReversalReviewed:checks[3].checked,noDuplicateReviewed:checks[4].checked})});
          if(!r.ok)throw Error("Settlement evidence refused.");
          const receipt=await r.json();
          if(receipt.historyId!==historyId||!guid.test(receipt.settlementEvidenceId)||receipt.evidenceRecorded!==true||receipt.providerVerified!==false||
            receipt.transferAuthorized!==false||receipt.payable!==false)throw Error("Unexpected receipt.");
          settlementStatus.textContent="Operator-reviewed external transfer evidence recorded: "+receipt.settlementEvidenceId;
        }catch{settlementStatus.textContent="Evidence not confirmed. Do not send money or retry until the current record is reviewed.";}
        finally{clearSettlement();busy=false;lock(false);}
      });card.append(record);settlementReview.append(card);settlementStatus.textContent="Review exact settlement evidence.";
    }catch(e){clearSettlement();settlementStatus.textContent=e.message;}
    finally{busy=false;lock(false);}
  });
  window.addEventListener("pagehide",clearSettlement);
  inventoryLoad.addEventListener("click",loadInventory);
  window.addEventListener("pagehide",clearInventory);
  load.addEventListener("click", refresh);
})();
