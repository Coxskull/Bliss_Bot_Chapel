"use strict";
document.addEventListener("DOMContentLoaded", async () => {
  const $=id=>document.getElementById(id), status=$("status"), canvas=$("stage");
  let controller=null, paused=false;
  const download=(blob,name)=>{const url=URL.createObjectURL(blob);const a=document.createElement("a");a.href=url;a.download=name;document.body.appendChild(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),5000);};
  try {
    if(!window.AlphaAdvertisingRealEstate.validate())throw Error("Invalid geometry");
    const images=await window.AlphaAdvertisingRealEstate.prepare();
    controller=window.AlphaAdvertisingRealEstate.start(canvas,images,$("layout").value);
    $("layout").onchange=()=>controller.choose($("layout").value);
    $("play").onclick=()=>{if(paused){controller=window.AlphaAdvertisingRealEstate.start(canvas,images,$("layout").value);paused=false;}else{controller.stop();paused=true;}$("play").textContent=paused?"Play":"Pause";};
    $("png").onclick=async()=>download(await controller.capture(),`ALPHA-ARE-${canvas.dataset.configuration}-FICTIONAL.png`);
    $("webm").onclick=async()=>{status.textContent="Recording three fictional configurations on this device only…";$("webm").disabled=true;try{download(await controller.record(9400),"ALPHA-ARE-2-4-6-FICTIONAL.webm");status.textContent="Offline WebM produced. NOT_SENT.";}catch(e){status.textContent=e.message;}finally{$("webm").disabled=false;}};
    ["play","png","webm"].forEach(id=>$(id).disabled=false);
    $("inventory").textContent=JSON.stringify(window.AlphaAdvertisingRealEstate.inventory,null,2);
    status.textContent="Existing ALPHA composer loaded: 2, 4 and 6 placements validated. No external API, no price, no consent, no campaign.";
  }catch(e){status.textContent="Demo blocked: "+e.message;}
});
