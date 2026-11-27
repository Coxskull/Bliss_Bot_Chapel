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
  inventoryLoad.addEventListener("click",loadInventory);
  window.addEventListener("pagehide",clearInventory);
  load.addEventListener("click", refresh);
})();
