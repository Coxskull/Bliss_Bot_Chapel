(() => {
  "use strict";
  const el = id => document.getElementById(id);
  const status = el("status"), panel = el("panel"), file = el("manifest"), summary = el("summary");
  const review = el("review"), prepare = el("prepare"), recipe = el("recipe"), save = el("save");
  const fields = ["family","headline","cta"].map(el);
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  const hex = /^[0-9a-f]{64}$/;
  const brandPanel = el("brand-panel"), draftButton = el("brand-draft"), confirmButton = el("brand-confirm"), approval = el("brand-approved");
  const brandKeys = ["brandName","niche","market","language","colors","font","goals","restrictions"];
  const brandInputs = ["name","niche","market","language","colors","font","goals","restrictions"].map(k=>el("brand-"+k));
  const readBrand = () => Object.fromEntries(brandKeys.map((k,i)=>[k,brandInputs[i].value.trim()]));
  const imagePanel = el("image-reservation"), imageReview = el("image-review"), imageReserve = el("image-reserve"),
    budgetApproval = el("image-budget-approved");
  let imageTarget = null, imageGrant = null, offerWorkspace = null;
  let restoredOrder=null;
  let artworkRun = null, artworkCandidate = null, artworkVersion = 0, artworkUrl = null, artworkRetry = null, revisionReviewId=null;
  const artworkPanel = el("artwork-panel"), artwork = el("artwork"), artworkReview = el("artwork-review");
  const artworkFields = ["decision","custom","quality","original","geo","defect","rationale","preserve","repair"].map(k=>el("artwork-"+k));
  let manifest = null, brand = null, brandState = null, pendingBrand = null, busy = false, epoch = 0;
  let queuedCampaigns = [], queueLoading = false;
  const queueSelect=el("campaign-select"),queueRefresh=el("campaign-refresh"),queueOpen=el("campaign-open"),queueStatus=el("queue-status");
  const unchanged = () => brandState && JSON.stringify(readBrand()) === JSON.stringify(brandState.fields);
  const get = path => fetch(path,{credentials:"same-origin",cache:"no-store",headers:{"Accept":"application/json"}});
  async function session() {
    const s = await get("/api/auth/session");
    if (!s.ok) throw Error("Sign in with an authorized operator account.");
    const data = await s.json();
    if (!data.isAuthenticated || typeof data.csrfToken !== "string" || !data.csrfToken)
      throw Error("Sign in with an authorized operator account.");
    const ready = await get("/api/internal/creative-readiness");
    if (!ready.ok) throw Error("Staff creative preparation is unavailable or access is not authorized.");
    return data.csrfToken;
  }
  const placementPanel=el("placement-preview-panel"),placementCanvas=el("placement-canvas"),
    placementLayout=el("placement-layout"),placementSlot=el("placement-slot"),placementButton=el("placement-preview");
  function clearPlacement(){
    placementCanvas.hidden=true;placementCanvas.width=1920;placementCanvas.height=1080;
    el("placement-status").textContent="";
  }
  function selectedLayout(){
    const engine=window.AlphaAdvertisingRealEstate;
    if(!engine?.validate()||!["2","4","6"].includes(placementLayout.value))return null;
    const layout=engine.inventory[placementLayout.value];
    return layout?.canvasWidth===1920&&layout.canvasHeight===1080&&layout.slots.length===Number(placementLayout.value)?layout:null;
  }
  function placementOptions(){
    clearPlacement();const layout=selectedLayout();
    placementSlot.replaceChildren(...(layout?.slots||[]).map(slot=>new Option(slot.id+" — "+slot.width+" × "+slot.height,slot.id)));
  }
  function previewPlacement(){
    clearPlacement();
    if(busy||!artworkUrl||artwork.hidden||!review.checked)return;
    try{
      const layout=selectedLayout(),slot=layout?.slots.find(s=>s.id===placementSlot.value);
      if(!slot||!artwork.naturalWidth||!artwork.naturalHeight)throw Error("Verified artwork and layout required.");
      const ctx=placementCanvas.getContext("2d",{alpha:false});if(!ctx)throw Error("Canvas unavailable.");
      ctx.fillStyle="#0F172A";ctx.fillRect(0,0,1920,1080);
      const p=layout.protectedCreatorArea;
      ctx.fillStyle="#334155";ctx.fillRect(p.x,p.y,p.width,p.height);
      ctx.fillStyle="#FFFFFF";ctx.font="bold 34px sans-serif";ctx.textAlign="center";ctx.textBaseline="middle";
      ctx.fillText("CREATOR CONTENT PLACEHOLDER",p.x+p.width/2,p.y+p.height/2,p.width-24);
      for(const s of layout.slots){
        ctx.fillStyle="#1E293B";ctx.fillRect(s.x,s.y,s.width,s.height);
        ctx.fillStyle="#CBD5E1";ctx.font="22px sans-serif";ctx.fillText(s.id,s.x+s.width/2,s.y+s.height/2,s.width-12);
      }
      const scale=Math.min(1,slot.width/artwork.naturalWidth,slot.height/artwork.naturalHeight);
      const w=artwork.naturalWidth*scale,h=artwork.naturalHeight*scale;
      ctx.save();ctx.beginPath();ctx.rect(slot.x,slot.y,slot.width,slot.height);ctx.clip();
      ctx.fillStyle="#0F172A";ctx.fillRect(slot.x,slot.y,slot.width,slot.height);
      ctx.drawImage(artwork,slot.x+(slot.width-w)/2,slot.y+(slot.height-h)/2,w,h);
      ctx.restore();
      ctx.fillStyle="#7F1D1D";ctx.fillRect(p.x,p.y,p.width,48);
      ctx.fillStyle="#FFFFFF";ctx.font="bold 28px sans-serif";ctx.fillText("PRIVATE MOCKUP • NOT APPROVED • NOT SENT",p.x+p.width/2,p.y+24,p.width-12);
      placementCanvas.hidden=false;
      el("placement-status").textContent="Source "+artwork.naturalWidth+" × "+artwork.naturalHeight+"; slot "+slot.width+" × "+slot.height+". "+(artwork.naturalWidth===slot.width&&artwork.naturalHeight===slot.height?"Native dimensions match.":"Scaled down or letterboxed for preview only. Native placement artwork still required.")+" No placement or release authorized.";
    }catch(e){clearPlacement();el("placement-status").textContent=e.message;}
  }
  placementLayout.addEventListener("change",placementOptions);
  placementSlot.addEventListener("change",clearPlacement);
  placementButton.addEventListener("click",previewPlacement);
  function sync() {
    file.disabled = busy; review.disabled = busy;
    el("checkout-issue").disabled=busy || !manifest || !offerWorkspace || !review.checked
        || (restoredOrder!==null && (restoredOrder.milestone!=="AWAITING_PAYMENT_INSTRUCTIONS" || el("checkout-offer-id").value.trim()!==restoredOrder.offerId))
        || !uuid.test(el("checkout-offer-id").value.trim()) || !el("checkout-merchant").checked || !el("checkout-clear").checked;
    el("offer-save").disabled=busy || !manifest || !offerWorkspace || !review.checked || !el("offer-approved").checked
        || restoredOrder!==null
        || !el("offer-tax").checked || !el("offer-scope").value.trim() || !el("offer-terms").value.trim();
    queueRefresh.disabled=busy || queueLoading;queueSelect.disabled=busy || queueLoading;
    queueOpen.disabled=busy || queueLoading || !queuedCampaigns.some(x=>x.campaignId===queueSelect.value);
    prepare.disabled = busy || !manifest || !review.checked;
    save.disabled = busy || !manifest || !review.checked || !brand || fields.some(f => !f.value.trim());
    fields.forEach(f => f.disabled = busy);
    brandInputs.forEach(f=>f.disabled=busy);
    approval.disabled = busy || !brandState || brandState.status !== "DRAFT" || !unchanged();
    draftButton.disabled = busy || !manifest || !review.checked || !brandState
      || ["brandName","niche","market","language","goals"].some(k=>!readBrand()[k])
      || (brandState.version > 0 && unchanged());
    confirmButton.disabled = busy || !manifest || !review.checked || approval.disabled || !approval.checked;
    if (!unchanged() || imageTarget) save.disabled = true;
    el("revision-save").disabled=busy || !manifest || !review.checked || !brand || !unchanged() || !imageTarget
        || !artworkUrl || !revisionReviewId || fields.some(f=>!f.value.trim());
    imageReview.disabled = busy || !imageTarget || !unchanged() || !review.checked;
    budgetApproval.disabled = busy || !imageGrant;
    imageReserve.disabled = busy || !imageTarget || !imageGrant || !budgetApproval.checked || !unchanged() || !review.checked;
    placementPanel.hidden=!artworkUrl||!review.checked;
    placementButton.disabled=busy||placementPanel.hidden;
    if(placementPanel.hidden)clearPlacement();
    el("artwork-preview").disabled = busy || !artworkRun || !review.checked;
    el("artwork-attach").disabled = busy || !artworkRun || !artworkUrl || !!artworkCandidate || !review.checked;
    el("artwork-record").disabled = busy || !artworkCandidate || !artworkUrl || !review.checked || !el("artwork-rationale").value.trim();
    artworkFields.forEach(f=>f.disabled=busy);
    el("delivery-readback").disabled=busy || !artworkCandidate || !artworkUrl || !review.checked || !uuid.test(el("reconciliation-id").value.trim());
    el("release-artwork").disabled=busy || !artworkCandidate || !artworkUrl || !review.checked
        || !uuid.test(el("reconciliation-id").value.trim()) || !el("release-payment").checked || !el("release-scope").checked;
  }
  function reset() {
    manifest = brand = brandState = pendingBrand = null; review.checked = approval.checked = false;
    restoredOrder=null;el("offer-scope").readOnly=el("offer-terms").readOnly=el("checkout-offer-id").readOnly=false;offerWorkspace=null;el("starter-offer").hidden=true;el("offer-scope").value=el("offer-terms").value="";
    el("offer-approved").checked=el("offer-tax").checked=false;el("offer-status").textContent="";
    el("checkout-offer-id").value=el("checkout-url").value=el("checkout-status").textContent="";
    el("checkout-merchant").checked=el("checkout-clear").checked=false;
    clearImage(null);
    brandPanel.hidden = true; brandInputs.forEach(f=>f.value=""); el("brand-status").textContent="";
    summary.hidden = recipe.hidden = true; fields.forEach(f => f.value = "");
    ["campaign-name","campaign-id","brief"].forEach(id => el(id).textContent = "");
    el("result").textContent = "No preparation requested."; sync();
  }
  async function readQueue() {
    if(busy || queueLoading)return;
    const search=el("campaign-search").value.trim().toLowerCase();
    if(search && !uuid.test(search)){queueStatus.textContent="Enter a valid campaign UUID.";return;}
    queueLoading=true;sync();
    try{
      await session();
      const response=await get("/api/internal/creative-preparation/queue?includeProgress=true"+(search?"&campaignId="+encodeURIComponent(search):""));
      if(!response.ok)throw Error("Campaign queue unavailable (HTTP "+response.status+").");
      const result=await response.json();
      if(!Array.isArray(result.campaigns)||result.campaigns.length>50||!Array.isArray(result.families)
          ||result.dispatchAuthorized!==false||result.release!=="HELD"||result.delivery!=="NOT_SENT")throw Error("Invalid campaign queue response.");
      queuedCampaigns=result.campaigns.filter(x=>uuid.test(x.campaignId)&&hex.test(x.briefSha256)
          &&typeof x.campaignName==="string"&&typeof x.creativeBrief==="string");
      queueSelect.replaceChildren(new Option("Select campaign",""));
      const milestones={RECIPE_NOT_RECORDED:"Needs recipe",RECIPE_RECORDED:"Recipe saved",IMAGE_RUN_RECORDED:"Image run recorded",IMAGE_RECORDED:"Image recorded",STAFF_REVIEW_PENDING:"Needs staff review",STAFF_REVISION_RECORDED:"Staff revision",STAFF_REVIEW_RECORDED:"Staff review recorded",ADVERTISER_REVIEW_PENDING:"Needs advertiser review",ADVERTISER_REVISION_RECORDED:"Advertiser requested changes",ADVERTISER_APPROVAL_RECORDED:"Advertiser approval recorded",NOT_LOADED:"Progress not loaded"};
      const revisionFirst=row=>["ADVERTISER_REVISION_RECORDED","STAFF_REVISION_RECORDED"].includes(row.creativeMilestone)?0:1;
      queuedCampaigns.sort((a,b)=>revisionFirst(a)-revisionFirst(b));
      for(const row of queuedCampaigns)queueSelect.add(new Option((milestones[row.creativeMilestone]||"Progress unavailable")+" — "+row.campaignName+" — "+row.campaignId,row.campaignId));
      const old=fields[0].value;fields[0].replaceChildren(new Option("Select eligible template",""));
      for(const family of result.families.filter(x=>typeof x==="string"&&x.length>0&&x.length<=128))fields[0].add(new Option(family,family));
      fields[0].value=old;
      const revisions=queuedCampaigns.filter(row=>revisionFirst(row)===0).length;
      const loaded=queuedCampaigns.filter(row=>Object.hasOwn(milestones,row.creativeMilestone)&&row.creativeMilestone!=="NOT_LOADED").length;
      queueStatus.textContent=queuedCampaigns.length+" eligible campaign(s); "+loaded+" with recorded progress; "+revisions+" with recorded revision feedback. Search by exact campaign ID for other progress. Milestones are not release or payment authorization.";
    }catch(e){queuedCampaigns=[];queueSelect.replaceChildren(new Option("Queue unavailable",""));queueStatus.textContent=e.message;}
    finally{queueLoading=false;sync();}
  }
  queueRefresh.addEventListener("click",readQueue);queueSelect.addEventListener("change",sync);
  queueOpen.addEventListener("click",async()=>{
    if(queueOpen.disabled)return;
    const row=queuedCampaigns.find(x=>x.campaignId===queueSelect.value);if(!row)return;
    const current=++epoch;reset();file.value="";
    const bytes=new TextEncoder().encode(row.campaignId+"\n"+row.campaignName+"\n"+row.creativeBrief);
    const digest=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",bytes)),x=>x.toString(16).padStart(2,"0")).join("");
    if(current!==epoch)return;
    if(digest!==row.briefSha256){status.textContent="Campaign checksum mismatch.";return;}
    manifest={campaignId:row.campaignId,briefSha256:digest};
    el("campaign-name").textContent=row.campaignName;el("campaign-id").textContent=row.campaignId;el("brief").textContent=row.creativeBrief;
    summary.hidden=false;status.textContent="Review the current campaign before preparing.";sync();
  });
  file.addEventListener("change", async () => {
    const current = ++epoch; reset();
    const f = file.files[0]; if (!f) return;
    try {
      if (f.size > 32768) throw Error("Manifest exceeds the permitted size.");
      const m = JSON.parse(await f.text());
      if (!m || m.schema !== "alpha.advertiser.campaign-creative-handoff.v1"
          || !uuid.test(m.campaignId) || !hex.test(m.briefSha256)
          || typeof m.campaignName !== "string" || !m.campaignName.trim() || m.campaignName.length > 256
          || typeof m.creativeBrief !== "string" || !m.creativeBrief.trim() || m.creativeBrief.length > 4000
          || m.status !== "DRAFT" || m.authorityStatus !== "UNVERIFIED" || m.release !== "HELD"
          || m.delivery !== "NOT_SENT" || m.modelCall !== "NOT_REQUESTED" || m.signedAuthorization !== false)
        throw Error("Not a valid held advertiser QA manifest.");
      const bytes = new TextEncoder().encode(m.campaignId + "\n" + m.campaignName + "\n" + m.creativeBrief);
      const digest = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",bytes)),b=>b.toString(16).padStart(2,"0")).join("");
      if (digest !== m.briefSha256) throw Error("Campaign checksum does not match.");
      if (current !== epoch) return;
      manifest = {campaignId:m.campaignId,briefSha256:digest};
      el("campaign-name").textContent = m.campaignName;
      el("campaign-id").textContent = m.campaignId;
      el("brief").textContent = m.creativeBrief;
      summary.hidden = false; status.textContent = "Manifest checksum matches. Review the brief before preparing.";
    } catch(e) { if (current === epoch) status.textContent = e instanceof SyntaxError ? "Invalid JSON manifest." : e.message; }
    finally { if (current === epoch) sync(); }
  });
  async function submit(action) {
    const revising=action==="revision",withRecipe = action === "recipe" || revising, brandAction = action === "SAVE_DRAFT" || action === "CONFIRM_DRAFT";
    if (busy || !manifest || !review.checked || (revising ? el("revision-save").disabled : withRecipe && save.disabled)
        || (action === "SAVE_DRAFT" && draftButton.disabled) || (action === "CONFIRM_DRAFT" && confirmButton.disabled)) return;
    const current = epoch; busy = true; sync(); status.textContent = "Preparing the selected campaign…";
    try {
      const csrf = await session();
      if (current !== epoch) return;
      const payload = {...manifest,operatorReviewedCampaign:true};
      if (brandAction) {
        const details = {brandAction:action,brandExpectedVersion:brandState.version,
          ...(action==="SAVE_DRAFT" ? {brandFields:readBrand()} : {customerBrandApproved:true})};
        const fingerprint = JSON.stringify(details);
        if (!pendingBrand || pendingBrand.fingerprint !== fingerprint)
          pendingBrand = {fingerprint,key:crypto.randomUUID()};
        Object.assign(payload,details,{brandIdempotencyKey:pendingBrand.key});
      }
      if (withRecipe) Object.assign(payload,{brandDnaId:brand,family:fields[0].value.trim(),headline:fields[1].value.trim(),cta:fields[2].value.trim()});
      if(revising)Object.assign(payload,{previousBriefId:imageTarget.briefId,revisionReviewId});
      const response = await fetch("/api/internal/creative-preparation",{method:"POST",credentials:"same-origin",cache:"no-store",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(payload)});
      if (current !== epoch) return;
      if (!response.ok) throw Error("Preparation not accepted (HTTP " + response.status + "). Check current campaign, permissions and consent.");
      const result = await response.json();
      if (result.campaignId !== manifest.campaignId || result.briefSha256 !== manifest.briefSha256
          || !uuid.test(result.workspaceId) || result.providerCall !== "NOT_REQUESTED"
          || result.release !== "HELD" || result.delivery !== "NOT_SENT" || result.dispatchAuthorized !== false
          || result.preparation !== (withRecipe ? "RECIPE_SAVED" : action === "SAVE_DRAFT" ? "BRAND_DRAFT_SAVED" : action === "CONFIRM_DRAFT" ? "BRAND_CONFIRMED" : "WORKSPACE_READY")
          || (withRecipe && (!uuid.test(result.productionBriefId) || !hex.test(result.recipeSha256))))
        throw Error("Unexpected response; preparation not confirmed.");
      if (!Number.isInteger(result.brandVersion) || result.brandVersion < 0
          || !["NOT_PREPARED","DRAFT","CONFIRMED"].includes(result.brandStatus)
          || (result.brandVersion > 0 && (!result.brandFields || brandKeys.some(k=>typeof result.brandFields[k]!=="string"))))
        throw Error("Brand response could not be verified.");
      const order=result.currentOrder;
      if(order!=null && (!uuid.test(order.offerId)||!hex.test(order.offerSha256)||order.amountCents!==14900||order.currency!=="usd"
          ||typeof order.scope!=="string"||!order.scope.trim()||order.scope.length>1000
          ||typeof order.terms!=="string"||!order.terms.trim()||order.terms.length>700
          ||!["AWAITING_ACCEPTANCE","AWAITING_PAYMENT_INSTRUCTIONS","PAYMENT_INSTRUCTIONS_RECORDED","RECONCILIATION_RECORDED","REVIEW_REQUIRED"].includes(order.milestone)))
        throw Error("Stored order could not be verified.");
      restoredOrder=order??null;
      offerWorkspace=result.workspaceId;el("starter-offer").hidden=false;
      if(restoredOrder){
        el("offer-scope").value=order.scope;el("offer-terms").value=order.terms;
        el("checkout-offer-id").value=order.offerId;
        el("offer-approved").checked=el("offer-tax").checked=false;
        el("checkout-merchant").checked=el("checkout-clear").checked=false;
        el("checkout-url").value="";
        el("offer-status").textContent="Existing immutable offer: "+order.offerId;
        el("checkout-status").textContent=({AWAITING_ACCEPTANCE:"Await customer acceptance.",AWAITING_PAYMENT_INSTRUCTIONS:"Accepted order; independently check Stripe before preparing instructions.",PAYMENT_INSTRUCTIONS_RECORDED:"Instructions already recorded. Do not issue another.",RECONCILIATION_RECORDED:"Reconciliation recorded. Continue creative preparation; verify current payment before release.",REVIEW_REQUIRED:"Resolve order discrepancies before proceeding."})[order.milestone];
      }
      el("offer-scope").readOnly=el("offer-terms").readOnly=!!restoredOrder;
      el("checkout-offer-id").readOnly=!!restoredOrder;
      const values = Object.fromEntries(brandKeys.map(k=>[k,result.brandFields?.[k]??""]));
      brandState = {version:result.brandVersion,status:result.brandStatus,fields:values};
      brandInputs.forEach((f,i)=>f.value=values[brandKeys[i]]);
      pendingBrand = null; approval.checked = false; brandPanel.hidden = false;
      el("brand-status").textContent = "Brand status: " + result.brandStatus + " | Revision " + result.brandVersion;
      brand = result.brandStatus === "CONFIRMED" && uuid.test(result.brandDnaId) ? result.brandDnaId : null;
      recipe.hidden = !brand;
      let recipeNotice="";
      if(result.recipeFields!=null){
        const saved=result.recipeFields;
        if(!brand || !uuid.test(result.productionBriefId) || !hex.test(result.recipeSha256)
            || typeof saved.family!=="string" || !saved.family.trim() || saved.family.length>128
            || typeof saved.headline!=="string" || !saved.headline.trim() || saved.headline.length>500
            || typeof saved.cta!=="string" || !saved.cta.trim() || saved.cta.length>500)
          throw Error("Stored recipe fields could not be verified.");
        fields[0].value=saved.family;fields[1].value=saved.headline;fields[2].value=saved.cta;
        recipeNotice=fields[0].value===saved.family?" Stored production copy restored.":" Stored copy restored; select a currently eligible template before saving a revision.";
      }
      clearImage(brand && uuid.test(result.productionBriefId) && hex.test(result.recipeSha256)
        ? {workspaceId:result.workspaceId,briefId:result.productionBriefId} : null);
      el("result").textContent = "Workspace: " + result.workspaceId + " | Brand: " + result.brandStatus
        + (withRecipe ? " | Recipe: " + result.productionBriefId + " | SHA-256: " + result.recipeSha256 : "")
        + " | Release HELD; no provider call or delivery." + recipeNotice;
      status.textContent = withRecipe ? (revising?"Revised recipe saved. Customer must grant fresh provider consent.":"Recipe saved.") : brandAction ? result.preparation === "BRAND_CONFIRMED" ? "Brand confirmed. Prepare the recipe." : "Brand draft saved. Confirm only after advertiser approval." : brand ? "Workspace ready; confirmed brand available." : "Workspace ready; enter the advertiser's brand information.";
    } catch(e) { status.textContent = e.message;if(revising)el("revision-status").textContent=e.message; }
    finally { busy = false; sync(); }
  }
  function clearImage(target) {
    imageTarget = target; imageGrant = null; imagePanel.hidden = !target;
    clearArtwork();
    budgetApproval.checked = false; el("image-readiness").textContent = ""; el("image-receipt").textContent = "";
  }
  async function imageAction(action) {
    if (!imageTarget || !manifest || !review.checked || busy || (action==="RESERVE" && imageReserve.disabled)) return;
    const current = epoch; busy = true; sync();
    try {
      const csrf = await session();
      const payload = {...manifest,...imageTarget,action};
      if (action==="RESERVE") Object.assign(payload,{consentReceiptId:imageGrant,operatorBudgetApproved:true});
      const response = await fetch("/api/internal/creative-image-reservation",{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(payload)});
      if (current !== epoch) return;
      if (!response.ok) throw Error("Reservation unavailable (HTTP "+response.status+"). Check advertiser consent, pilot budget and billing status.");
      const result = await response.json();
      if (result.dispatchAuthorized !== false || result.release !== "HELD" || result.delivery !== "NOT_SENT")
        throw Error("Unexpected reservation response.");
      imageGrant = null; budgetApproval.checked = false;
      if (uuid.test(result.runId)) {
        if (result.workspaceId !== imageTarget.workspaceId || result.briefId !== imageTarget.briefId || !hex.test(result.inputSha256))
          throw Error("Run receipt does not match this recipe.");
        el("image-receipt").textContent = JSON.stringify(result,null,2);
        el("image-readiness").textContent = "Existing image run: "+result.status+". No additional dispatch authorized.";
        clearArtwork();
        if (result.status==="SUCCEEDED_PENDING_REVIEW") {artworkRun=result.runId;artworkPanel.hidden=false;}
      } else {
        if (result.campaignId !== manifest.campaignId || !uuid.test(result.consentReceiptId)) throw Error("Consent receipt could not be verified.");
        const ready = result.imagePilotConfigured === true && result.readiness?.imageReady === true;
        if (ready) imageGrant = result.consentReceiptId;
        el("image-readiness").textContent = ready ? "Consent current. Reserve USD "+result.readiness.imageReservationUsd+" for one image."
          : result.readiness?.billingAnomaly===true ? "Consent recorded; a prior provider billing anomaly requires reconciliation."
          : result.imagePilotConfigured!==true ? "Consent recorded; this exact recipe is not authorized by the current pilot configuration."
          : "Consent recorded; reservation unavailable. Daily requests: "+result.readiness?.callsReservedToday+"/"+result.readiness?.maxCallsPerDay
            +"; daily reserved USD "+result.readiness?.reservedTodayUsd+"/"+result.readiness?.dailyBudgetUsd
            +"; total reserved USD "+result.readiness?.reservedTotalUsd+"/"+result.readiness?.totalBudgetUsd
            +". Daily allowance resets at 00:00 UTC.";
      }
    } catch(e) { imageGrant=null;budgetApproval.checked=false;el("image-readiness").textContent=e.message; }
    finally {busy=false;sync();}
  }
  function clearArtwork() {
    clearPlacement();placementPanel.hidden=true;
    if (artworkUrl) URL.revokeObjectURL(artworkUrl);
    artworkUrl=artworkRun=artworkCandidate=artworkRetry=revisionReviewId=null; artworkVersion=0;
    el("revision-panel").hidden=true;el("revision-status").textContent="";
    el("advertiser-revision-feedback").replaceChildren();el("advertiser-revision-feedback").hidden=true;
    artwork.hidden=artworkPanel.hidden=artworkReview.hidden=el("artwork-release").hidden=true; artwork.removeAttribute("src");
    el("reconciliation-id").value="";el("release-payment").checked=el("release-scope").checked=false;
    artworkFields.forEach(f=>{if(f.type==="checkbox") f.checked=false;else if(f.tagName==="SELECT") f.selectedIndex=0;else f.value="";});
    el("artwork-status").textContent="";
  }
  async function artworkAction(action) {
    if (busy || !artworkRun || !imageTarget || !manifest || !review.checked) return;
    const current=epoch;busy=true;sync();
    try {
      const csrf=await session();
      const payload={...manifest,...imageTarget,runId:artworkRun,action};
      if(action==="REVIEW"){
        if(!artworkCandidate || !artworkUrl) throw Error("Preview and attach the image first.");
        const gate=k=>el("artwork-"+k).checked?"PASS":"FAIL";
        const data={expectedVersion:artworkVersion,decision:el("artwork-decision").value,
          customization:gate("custom"),qualityParity:gate("quality"),originality:gate("original"),geographicAuthenticity:gate("geo"),
          criticalDefect:el("artwork-defect").checked,rationale:el("artwork-rationale").value.trim(),
          preserve:el("artwork-preserve").value.trim(),repair:el("artwork-repair").value.trim()};
        const fingerprint=JSON.stringify(data);
        if(!artworkRetry || artworkRetry.fingerprint!==fingerprint) artworkRetry={fingerprint,key:crypto.randomUUID()};
        Object.assign(payload,{candidateId:artworkCandidate,review:{...data,idempotencyKey:artworkRetry.key}});
      }
      if(action==="DELIVERY_STATUS"){
        if(!artworkCandidate || !uuid.test(el("reconciliation-id").value.trim()))throw Error("Select the exact reconciliation receipt.");
        Object.assign(payload,{candidateId:artworkCandidate,reconciliationId:el("reconciliation-id").value.trim()});
      }
      if(action==="RELEASE"){        if(!artworkCandidate || !artworkUrl || !uuid.test(el("reconciliation-id").value.trim()) || !el("release-payment").checked || !el("release-scope").checked)throw Error("Complete release checks.");
        Object.assign(payload,{candidateId:artworkCandidate,reconciliationId:el("reconciliation-id").value.trim(),currentPaymentVerified:true,scopeVerified:true});
      }
      const request=async body=>fetch("/api/internal/creative-image-review",{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify(body)});
      let response=await request(payload);
      let previewBlob=null;
      if(!response.ok) throw Error("Artwork review unavailable (HTTP "+response.status+").");
      if(action==="PREVIEW"){
        if(!response.headers.get("content-type")?.startsWith("image/png")) throw Error("Expected private PNG.");
        const blob=await response.blob();
        if(blob.size>16*1024*1024 || blob.size===0) throw Error("Invalid artwork size.");
        if(current!==epoch)return;
        if(artworkUrl) URL.revokeObjectURL(artworkUrl);
        clearPlacement();placementPanel.hidden=true;
        artworkUrl=null;artwork.hidden=true;artwork.removeAttribute("src");
        previewBlob=blob;
        response=await request({...payload,action:"STATUS"});
        if(!response.ok) throw Error("Candidate history unavailable.");
      }
      const result=await response.json();if(current!==epoch)return;
      if(result.runId!==artworkRun || !hex.test(result.imageSha256) || result.dispatchAuthorized!==false
        || (action==="RELEASE" ? result.release!=="ARTWORK_DOWNLOAD_AVAILABLE" || result.delivery!=="DOWNLOAD_NOT_CONFIRMED" || !uuid.test(result.releaseId)
          : action==="DELIVERY_STATUS" ? !((result.release==="HELD" && result.delivery==="NOT_SENT" && result.releaseId==null) || (result.release==="ARTWORK_DOWNLOAD_AVAILABLE" && result.delivery==="DOWNLOAD_NOT_CONFIRMED" && uuid.test(result.releaseId)))
          : result.release!=="HELD" || result.delivery!=="NOT_SENT") || !Number.isInteger(result.reviewVersion))
        throw Error("Unexpected artwork review receipt.");
      if(action==="DELIVERY_STATUS"){
        if(result.candidateId!==artworkCandidate||typeof result.downloadRequestRecorded!=="boolean"||typeof result.receiptAcknowledged!=="boolean"||
          (result.downloadRequestRecorded?result.release!=="ARTWORK_DOWNLOAD_AVAILABLE"||!Number.isFinite(Date.parse(result.downloadRequestedAt)):result.downloadRequestedAt!=null)||
          (result.receiptAcknowledged?!result.downloadRequestRecorded||!Number.isFinite(Date.parse(result.receiptAcknowledgedAt)):result.receiptAcknowledgedAt!=null))throw Error("Invalid delivery readback.");
        el("artwork-status").textContent=result.receiptAcknowledged?"Customer approval actor acknowledged receipt: "+new Date(result.receiptAcknowledgedAt).toLocaleString()+". Not independent proof of saved file."
          :result.downloadRequestRecorded?"Customer approval actor requested download; receipt not acknowledged."
          :result.release==="HELD"?"No matching artwork release recorded.":"Artwork released; no download request from the approving customer recorded.";
        return;
      }
      if(action==="PREVIEW"){
        const digest=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",await previewBlob.arrayBuffer())),x=>x.toString(16).padStart(2,"0")).join("");
        if(digest!==result.imageSha256)throw Error("Artwork integrity verification failed.");
        if(current!==epoch)return;
        const url=URL.createObjectURL(previewBlob);
        try{
          artwork.src=url;await artwork.decode();
          if(current!==epoch){URL.revokeObjectURL(url);return;}
          artworkUrl=url;artwork.hidden=false;placementOptions();
        }catch{URL.revokeObjectURL(url);artwork.removeAttribute("src");artwork.hidden=true;throw Error("Artwork could not be displayed; review remains unavailable.");}
      }
      artworkCandidate=uuid.test(result.candidateId)?result.candidateId:null;artworkVersion=result.reviewVersion;
      const feedbackPanel=el("advertiser-revision-feedback");
      feedbackPanel.replaceChildren();feedbackPanel.hidden=true;
      if(result.advertiserRevisionRequest!=null){
        const feedback=result.advertiserRevisionRequest;
        if(!artworkCandidate || result.decision!=="PASS" || !uuid.test(feedback.decisionId)
            || feedback.reviewId!==result.reviewId || !Number.isInteger(feedback.version) || feedback.version<1
            || typeof feedback.rationale!=="string" || !feedback.rationale.trim() || feedback.rationale.length>2000
            || typeof feedback.recordedAt!=="string" || !Number.isFinite(Date.parse(feedback.recordedAt)))throw Error("Invalid current advertiser revision request.");
        const heading=document.createElement("h3");heading.textContent="Advertiser requested changes";
        const reference=document.createElement("p");reference.textContent="Decision "+feedback.decisionId+" · version "+feedback.version+" · "+new Date(feedback.recordedAt).toLocaleString();
        const notes=document.createElement("p");notes.textContent=feedback.rationale;notes.style.whiteSpace="pre-wrap";
        const explanation=document.createElement("p");explanation.textContent="Review this feedback against the exact private artwork. Copying it creates no review, revised recipe, paid request or release.";
        const use=document.createElement("button");use.type="button";use.textContent="Use feedback in human repair instructions";
        const candidateAtRead=artworkCandidate,reviewAtRead=artworkVersion,urlAtRead=artworkUrl;
        use.disabled=!urlAtRead;
        use.addEventListener("click",()=>{
          if(busy||current!==epoch||!feedbackPanel.contains(use)||!review.checked||artworkCandidate!==candidateAtRead||artworkVersion!==reviewAtRead||!urlAtRead||artworkUrl!==urlAtRead)return;
          const repair=el("artwork-repair");
          if(repair.value.trim()&&repair.value!==feedback.rationale&&!window.confirm("Replace the unsaved repair instructions with the recorded advertiser feedback?"))return;
          repair.value=feedback.rationale;el("artwork-decision").value="REVISE";
          el("artwork-status").textContent="Feedback copied into draft repair instructions. Review the artwork and record your own rationale and quality checks before saving.";
          sync();
        });
        feedbackPanel.append(heading,reference,notes,explanation,use);feedbackPanel.hidden=false;
      }
      revisionReviewId=result.decision==="REVISE" && uuid.test(result.reviewId)?result.reviewId:null;
      el("revision-panel").hidden=!revisionReviewId;
      artworkReview.hidden=el("artwork-release").hidden=!artworkCandidate;
      if(action==="RELEASE"){el("artwork-status").textContent="Private artwork download authorized. Receipt "+result.releaseId+". Customer download not confirmed.";return;}
      if(action==="REVIEW")artworkRetry=null;
      el("artwork-status").textContent=action==="REVIEW"?"Saved review: "+result.decision+" — release remains HELD."
        : (action==="PREVIEW" && typeof result.actualUsd==="number" ? "Verified provider cost: USD "+result.actualUsd+". " : "")+(artworkCandidate?"Candidate attached. Complete human quality review.":"Artwork loaded. Attach to candidate history.");
    }catch(e){if(current===epoch)el("artwork-status").textContent=e.message;}
    finally{busy=false;sync();}
  }
  el("artwork-preview").addEventListener("click",()=>artworkAction("PREVIEW"));
  el("artwork-attach").addEventListener("click",()=>artworkAction("ATTACH"));
  el("artwork-record").addEventListener("click",()=>artworkAction("REVIEW"));
  artworkFields.forEach(f=>f.addEventListener("input",sync));
  ["reconciliation-id","release-payment","release-scope"].forEach(id=>el(id).addEventListener("input",sync));
  el("delivery-readback").addEventListener("click",()=>{if(!el("delivery-readback").disabled)artworkAction("DELIVERY_STATUS");});
  el("release-artwork").addEventListener("click",()=>{if(!el("release-artwork").disabled)artworkAction("RELEASE");});
  imageReview.addEventListener("click",()=>imageAction("REVIEW"));
  imageReserve.addEventListener("click",()=>imageAction("RESERVE"));
  budgetApproval.addEventListener("change",sync);
  ["offer-scope","offer-terms","offer-approved","offer-tax"].forEach(id=>el(id).addEventListener("input",sync));
  el("offer-save").addEventListener("click",async()=>{
    if(el("offer-save").disabled)return;
    busy=true;sync();const current=epoch;
    try{
      const csrf=await session();
      const response=await fetch("/api/internal/creative-preparation/starter-offer",{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify({...manifest,workspaceId:offerWorkspace,
          scope:el("offer-scope").value.trim(),terms:el("offer-terms").value.trim(),scopeAndTermsApproved:true,taxTreatmentApproved:true})});
      if(!response.ok)throw Error("Offer not accepted (HTTP "+response.status+"). Check terms and permissions.");
      const result=await response.json();if(current!==epoch)return;
      if(!uuid.test(result.offerId)||!hex.test(result.offerSha256)||result.campaignId!==manifest.campaignId||result.amountCents!==14900
          ||result.status!=="OFFERED"||result.paymentAuthorized!==false||result.release!=="HELD")throw Error("Invalid offer receipt.");
      el("offer-status").textContent="Private offer issued. Customer must accept. Reference: "+result.offerId;
      el("checkout-offer-id").value=result.offerId;
    }catch(e){if(current===epoch)el("offer-status").textContent=e.message;}finally{busy=false;sync();}
  });
  ["checkout-offer-id","checkout-merchant","checkout-clear"].forEach(id=>el(id).addEventListener("input",()=>{el("checkout-url").value="";sync();}));
  el("checkout-issue").addEventListener("click",async()=>{
    if(el("checkout-issue").disabled)return;
    const current=epoch,id=el("checkout-offer-id").value.trim(),campaign=manifest.campaignId;
    busy=true;sync();el("checkout-url").value="";
    try{
      const csrf=await session();
      const response=await fetch("/api/internal/creative-preparation/starter-checkout",{method:"POST",credentials:"same-origin",cache:"no-store",
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf},body:JSON.stringify({campaignId:campaign,offerId:id,
          merchantPriceAndTaxVerified:true,noPriorOrOpenPaymentVerified:true})});
      if(!response.ok)throw Error("Payment instructions unavailable (HTTP "+response.status+"). Check acceptance and original Stripe checkout before retrying.");
      const result=await response.json();if(current!==epoch)return;
      const url=new URL(result.paymentUrl);
      if(result.offerId!==id||result.campaignId!==campaign||result.amountCents!==14900||result.currency!=="usd"
          ||result.paymentInitiated!==false||result.fulfillmentAuthorized!==false||result.status!=="INSTRUCTIONS_ISSUED"
          ||url.protocol!=="https:"||url.hostname!=="buy.stripe.com"||url.pathname!=="/14A00j8fN6El79Q8Df0Jq00"
          ||url.searchParams.get("client_reference_id")!==id)throw Error("Invalid payment instructions receipt.");
      el("checkout-url").value=url.href;el("checkout-status").textContent="Instructions recorded. Send privately only to the accepted customer. No payment initiated.";
      el("checkout-merchant").checked=el("checkout-clear").checked=false;
    }catch(e){if(current===epoch)el("checkout-status").textContent=e.message;}finally{busy=false;sync();}
  });
  review.addEventListener("change",sync); fields.forEach(f=>f.addEventListener("input",sync));
  prepare.addEventListener("click",()=>submit("workspace")); save.addEventListener("click",()=>submit("recipe"));
  el("revision-save").addEventListener("click",()=>submit("revision"));
  brandInputs.forEach(f=>f.addEventListener("input",()=>{approval.checked=false;sync();}));
  approval.addEventListener("change",sync);
  draftButton.addEventListener("click",()=>submit("SAVE_DRAFT")); confirmButton.addEventListener("click",()=>submit("CONFIRM_DRAFT"));
  window.addEventListener("pagehide",()=>{epoch++; reset(); file.value=""; queuedCampaigns=[];queueSelect.replaceChildren();panel.hidden=true;});
  reset();
  session().then(()=>{panel.hidden=false;status.textContent="Authorized staff access confirmed.";return readQueue();}).catch(e=>{status.textContent=e.message;});
})();
