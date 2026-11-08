/* ALPHA Creative Academy: six native-fit slots, no server calls, no ads/campaign actions.
 * The prepared slot images are created by the approved source-art adapter.
 * Fail closed if the source file cannot fill its slot without a crop/stretch.
 */
(() => {
  "use strict";
  const WIDTH=1920, HEIGHT=1080;
  const SLOTS=Object.freeze([
    Object.freeze({id:"TOP_BILLBOARD",label:"Top · full-width ad",x:0,y:0,w:1920,h:190,pattern:/ALPHA_NATIVE_TOP_BILLBOARD/i}),
    Object.freeze({id:"LEFT_UPPER",label:"Left · upper ad",x:0,y:190,w:350,h:330,pattern:/ALPHA_NATIVE_LEFT_UPPER/i}),
    Object.freeze({id:"LEFT_LOWER",label:"Left · lower ad",x:0,y:520,w:350,h:330,pattern:/ALPHA_NATIVE_LEFT_LOWER/i}),
    Object.freeze({id:"RIGHT_UPPER",label:"Right · upper ad",x:1570,y:190,w:350,h:330,pattern:/ALPHA_NATIVE_RIGHT_UPPER/i}),
    Object.freeze({id:"RIGHT_LOWER",label:"Right · lower ad",x:1570,y:520,w:350,h:330,pattern:/ALPHA_NATIVE_RIGHT_LOWER/i}),
    Object.freeze({id:"BOTTOM_BILLBOARD",label:"Bottom · full-width ad",x:0,y:850,w:1920,h:230,pattern:/ALPHA_NATIVE_BOTTOM_BILLBOARD/i})
  ]);
  const CENTER=Object.freeze({id:"PODCAST_CENTER",label:"Protected podcast",x:350,y:190,w:1220,h:660,
    pattern:/(podcast.center|podcast_center|center.podcast)/i});
  const REGIONS=Object.freeze([...SLOTS,CENTER]);
  const MAX_BYTES=15*1024*1024;
  function verifyGeometry(){
    const pixels=new Uint8Array(WIDTH*HEIGHT);
    let ad=0,protectedPixels=0;
    for(const rect of REGIONS){
      if(![rect.x,rect.y,rect.w,rect.h].every(Number.isInteger)||
        rect.x<0||rect.y<0||rect.w<1||rect.h<1||
        rect.x+rect.w>WIDTH||rect.y+rect.h>HEIGHT)throw Error("Invalid slot boundary: "+rect.id);
      for(let y=rect.y;y<rect.y+rect.h;y++){
        const offset=y*WIDTH;
        for(let x=rect.x;x<rect.x+rect.w;x++){
          if(pixels[offset+x])throw Error("Slot overlap at "+x+","+y);
          pixels[offset+x]=rect===CENTER?2:1;
        }
      }
      if(rect===CENTER)protectedPixels+=rect.w*rect.h;else ad+=rect.w*rect.h;
    }
    const gaps=pixels.reduce((total,value)=>total+(value===0?1:0),0);
    if(gaps||ad+protectedPixels!==WIDTH*HEIGHT)throw Error("Unassigned pixels: "+gaps);
    return Object.freeze({width:WIDTH,height:HEIGHT,adPixels:ad,
      centerPixels:protectedPixels,blankPixels:gaps,overlaps:0,adCount:SLOTS.length});
  }
  function cover(ctx,img,x,y,w,h){
    const scale=Math.max(w/img.width,h/img.height);
    const sw=w/scale,sh=h/scale;
    const sx=(img.width-sw)/2,sy=(img.height-sh)/2;
    ctx.drawImage(img,sx,sy,sw,sh,x,y,w,h);
  }
  function detect(name){return REGIONS.find(s=>s.pattern.test(name))?.id||null;}
  function ready(state){return REGIONS.every(s=>state.has(s.id));}
  window.AlphaNativeSixSlot=Object.freeze({width:WIDTH,height:HEIGHT,slots:SLOTS,center:CENTER,
    verifyGeometry,detect});
  if(typeof document==="undefined")return;
  document.addEventListener("DOMContentLoaded",()=>{
    const fileInput=document.getElementById("assets");
    const compose=document.getElementById("compose");
    const exportBtn=document.getElementById("export");
    const status=document.getElementById("status");
    const inventory=document.getElementById("inventory");
    const canvas=document.getElementById("stage");
    const proof=document.getElementById("proof");
    const caption=document.getElementById("canvas-caption");
    if(!fileInput||!compose||!exportBtn||!canvas||!inventory||!status||!proof||!caption)return;
    const state=new Map();
    const setStatus=(message,isError=false)=>{
      status.textContent=message;
      status.className=isError?"error":"pass";
    };
    const revoke=()=>{
      for(const item of state.values())try{item.bitmap.close?.();}catch{}
      state.clear();
    };
    function updateInventory(){
      inventory.replaceChildren();
      for(const rect of REGIONS){
        const row=document.createElement("tr");
        const file=state.get(rect.id);
        for(const value of [rect.label,
          rect===CENTER?"Center fills 1220×660":rect.w+"×"+rect.h,
          file?file.name:"Missing"]) {
          const cell=document.createElement("td");
          cell.textContent=value;
          row.append(cell);
        }
        inventory.append(row);
      }
      compose.disabled=!ready(state);
    }
    updateInventory();
    // The approved six native-fit images are committed in this existing
    // project's static staging assets. They are never generated dynamically.
    // Local file selection can override all seven without uploading anything.
    async function loadBundledAssets(){
      if(typeof Image==="undefined")return; // Node offline test has no browser Image.
      const folder="/assets/creative-academy/native-six/";
      const loaded=await Promise.all(REGIONS.map(rect=>new Promise(resolve=>{
        const photo=new Image();
        photo.decoding="async";
        photo.onload=()=>resolve({id:rect.id,photo,rect,
          name:rect===CENTER?"podcast_center_ai_crop.png":"ALPHA_NATIVE_"+rect.id+".png"});
        photo.onerror=()=>resolve(null);
        photo.src=folder+(rect===CENTER?"podcast_center_ai_crop.png":"ALPHA_NATIVE_"+rect.id+".png");
      })));
      if(fileInput.files?.length)return; // Never override an explicit operator selection.
      for(const item of loaded){
        if(!item)continue;
        if(item.rect!==CENTER&&(item.photo.width!==item.rect.w||item.photo.height!==item.rect.h))continue;
        state.set(item.id,{bitmap:item.photo,name:item.name+" (approved staging asset)"});
      }
      updateInventory();
      if(ready(state)){
        setStatus("7/7 approved native-fit images loaded from this site's existing static assets.");
        compose.click();
      }else{
        setStatus("Some bundled staging assets are unavailable; choose the seven local files to proceed.",true);
      }
    }
    loadBundledAssets();
    fileInput.addEventListener("change",async()=>{
      revoke();
      compose.disabled=true;exportBtn.disabled=true;canvas.hidden=true;
      proof.textContent="";caption.textContent="Waiting for all seven approved files.";
      const unknown=[],bad=[];
      for(const file of Array.from(fileInput.files||[])){
        const id=detect(file.name);
        if(!id){unknown.push(file.name);continue;}
        if(state.has(id)){bad.push("Duplicate placement "+id);continue;}
        if(!/^image\/(png|jpeg|webp)$/.test(file.type)||file.size>MAX_BYTES){
          bad.push("Invalid file type or size: "+file.name);continue;
        }
        try{
          const bitmap=await createImageBitmap(file);
          const slot=REGIONS.find(rect=>rect.id===id);
          if(slot!==CENTER&&(bitmap.width!==slot.w||bitmap.height!==slot.h)){
            bad.push(slot.id+" must be "+slot.w+"×"+slot.h+
              " native-fit, but source is "+bitmap.width+"×"+bitmap.height);
            bitmap.close();continue;
          }
          state.set(id,{bitmap,name:file.name});
        }catch{bad.push("Unable to decode "+file.name);}
      }
      updateInventory();
      const missing=REGIONS.filter(rect=>!state.has(rect.id)).map(x=>x.id);
      if(missing.length||bad.length){
        setStatus("Not ready. "+(bad.length?bad.join("; ")+". ":"")+
          (missing.length?"Missing: "+missing.join(", "):"")+
          (unknown.length?". Ignored "+unknown.length+" unmatched file(s).":""),true);
      }else {
        setStatus("7/7 images verified; every ad exactly matches its native placement. Render is ready.");
      }
    });
    compose.addEventListener("click",()=>{
      if(!ready(state)){setStatus("All six exact-size ads and a podcast image are required.",true);return;}
      const geometry=verifyGeometry();
      const ctx=canvas.getContext("2d",{alpha:false});
      if(!ctx){setStatus("Canvas unavailable in this browser.",true);return;}
      ctx.imageSmoothingEnabled=true;
      ctx.imageSmoothingQuality="high";
      for(const rect of SLOTS){
        const img=state.get(rect.id).bitmap;
        if(img.width!==rect.w||img.height!==rect.h)throw Error("Ad size mismatch");
        ctx.drawImage(img,rect.x,rect.y);
      }
      const center=state.get(CENTER.id).bitmap;
      cover(ctx,center,CENTER.x,CENTER.y,CENTER.w,CENTER.h);
      canvas.hidden=false;exportBtn.disabled=false;
      caption.textContent="Complete six-slot composite · no overlays, guide strips or thumbnail artwork. Demonstration only.";
      proof.textContent="Geometry PASS · 6 full-bleed ads · "+geometry.adPixels.toLocaleString()+
        " advertising pixels · "+geometry.centerPixels.toLocaleString()+
        " protected podcast pixels · 0 blank pixels · 0 overlaps.";
      setStatus("Native-size ad placement preview ready. Final commercial creative approval is still required.");
    });
    exportBtn.addEventListener("click",()=>{
      if(canvas.hidden)return;
      canvas.toBlob(blob=>{
        if(!blob){setStatus("PNG export failed.",true);return;}
        const url=URL.createObjectURL(blob),a=document.createElement("a");
        a.href=url;a.download="ALPHA-SIX-SLOT-NATIVE-FIT-STAGING.png";
        document.body.append(a);a.click();a.remove();
        setTimeout(()=>URL.revokeObjectURL(url),5000);
      },"image/png");
    });
    window.addEventListener("pagehide",revoke,{once:true});
  });
})();