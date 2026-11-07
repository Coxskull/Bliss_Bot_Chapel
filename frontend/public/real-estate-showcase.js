"use strict";
/* ALPHA Advertising Real Estate — offline adapter for the EXISTING AlphaTemplateComposer.
   Fictional briefs only. No quotes, creator consent, reservations or network calls. */
(function () {
  const W = 1920, H = 1080;
  const fixtures = {
    2: {
      protected: [0, 0, 1920, 810],
      slots: [
        ["BOTTOM_BLUE", 0, 810, 960, 270, "#0863D5", "LumaWater", "Refresh every moment", "PURE WATER"],
        ["BOTTOM_ORANGE", 960, 810, 960, 270, "#EB7821", "RoastVale", "Made for your mornings", "COFFEE"]
      ]
    },
    4: {
      protected: [0, 180, 1920, 720],
      slots: [
        ["TOP_BLUE", 0, 0, 960, 180, "#0863D5", "NovaFizz", "Make a little sparkle", "SOFT DRINK"],
        ["TOP_ORANGE", 960, 0, 960, 180, "#EB7821", "CitrusBloom", "A brighter sip", "FRUIT DRINK"],
        ["BOTTOM_GREEN", 0, 900, 960, 180, "#15904B", "TropiLeaf", "Bright fruit moments", "JUICE"],
        ["BOTTOM_PURPLE", 960, 900, 960, 180, "#8027B8", "VelvetScoop", "A scoop of delight", "DESSERT"]
      ]
    },
    6: {
      protected: [270, 180, 1380, 720],
      slots: [
        ["TOP_BLUE", 0, 0, 960, 180, "#0863D5", "SparkHome", "New ways to live", "APPLIANCES"],
        ["TOP_ORANGE", 960, 0, 960, 180, "#EB7821", "CraftBolt", "Make what matters", "TOOLS"],
        ["LEFT_PURPLE", 0, 270, 270, 540, "#8027B8", "BloomNails", "Color your day", "NAILS"],
        ["RIGHT_GREEN", 1650, 270, 270, 540, "#15904B", "SonicGear", "Hear every detail", "AUDIO"],
        ["BOTTOM_BLUE", 0, 900, 960, 180, "#0863D5", "MediVale", "Care for today", "PHARMACY"],
        ["BOTTOM_PURPLE", 960, 900, 960, 180, "#8027B8", "JellyJoy", "Share a little joy", "SNACKS"]
      ]
    }
  };
  function overlaps(a, b) {
    return a[1] < b[1] + b[3] && b[1] < a[1] + a[3]
      && a[2] < b[2] + b[4] && b[2] < a[2] + a[4];
  }
  function check(layout) {
    const p = ["PROTECTED", ...layout.protected];
    if (p[1] < 0 || p[2] < 0 || p[3] < 1 || p[4] < 1 || p[1] + p[3] > W || p[2] + p[4] > H) throw Error("Invalid protected area");
    layout.slots.forEach((slot, i) => {
      if (slot[1] < 0 || slot[2] < 0 || slot[3] < 1 || slot[4] < 1 || slot[1] + slot[3] > W || slot[2] + slot[4] > H) throw Error("Invalid slot geometry");
      if (overlaps(slot, p) || layout.slots.some((next, j) => j !== i && overlaps(slot, next))) throw Error("Slots overlap protected content or each other");
    });
    return true;
  }
  Object.values(fixtures).forEach(check);
  const inventory = Object.freeze(Object.fromEntries(Object.entries(fixtures).map(([id, layout]) => [id, Object.freeze({
    configurationId: `ALPHA-ARE-DEMO-${id}-V1`, geometryVersion: "ALPHA-ARE-1920x1080-DEMO-V1", canvasWidth: W, canvasHeight: H,
    advertiserCapacity: layout.slots.length, protectedCreatorArea: { x: layout.protected[0], y: layout.protected[1], width: layout.protected[2], height: layout.protected[3] },
    durationSeconds: 15, occurrenceCount: null, exclusivity: "SHARED_DEMO_ONLY", creatorAuthorization: "NOT_RECORDED",
    availability: "UNVERIFIED", deviceCompatibility: "UNVERIFIED", economicsQuoteReference: null, campaignReady: false,
    delivery: "NOT_SENT", source: "Existing AlphaTemplateComposer.render; fictional offline fixtures",
    slots: layout.slots.map(s => ({ id: s[0], x: s[1], y: s[2], width: s[3], height: s[4], advertiser: s[6], color: s[5] }))
  })])));
  async function prepare() {
    if (typeof window.AlphaTemplateComposer?.render !== "function") throw Error("Existing ALPHA ad composer is not loaded");
    const images = {};
    for (const layout of Object.values(fixtures)) {
      for (const slot of layout.slots) {
        const name = slot[6];
        if (images[name]) continue;
        const blob = await window.AlphaTemplateComposer.render({ format: "landscape", brandName: name, headline: slot[7], colors: `${slot[5]} #F7D79D`, cta: "FICTIONAL DEMO", market: "DEMONSTRATION" });
        images[name] = await createImageBitmap(blob);
      }
    }
    return images;
  }
  function draw(ctx, id, images, seconds) {
    const layout = fixtures[id];
    if (!layout) throw Error("Unknown advertising configuration");
    check(layout);
    const [px, py, pw, ph] = layout.protected;
    const bg = ctx.createLinearGradient(0, 0, 0, H); bg.addColorStop(0, "#101D35"); bg.addColorStop(1, "#050B19");
    ctx.fillStyle = bg; ctx.fillRect(0, 0, W, H);
    const glow = ctx.createRadialGradient(W / 2, H / 2, 90, W / 2, H / 2, 1000);
    glow.addColorStop(0, "#254A6D"); glow.addColorStop(1, "#071323");
    ctx.fillStyle = glow; ctx.fillRect(px, py, pw, ph);
    // Animated, original, fictional podcast program visualization. No stock footage.
    ctx.save(); ctx.beginPath(); ctx.rect(px, py, pw, ph); ctx.clip();
    const cx = px + pw / 2, cy = py + ph / 2;
    for (let i = 0; i < 5; i++) {
      ctx.strokeStyle = `rgba(91,195,241,${0.25-i*.033})`;
      ctx.lineWidth = 4; ctx.beginPath(); ctx.arc(cx, cy, 125 + i * 56 + Math.sin(seconds + i) * 6, 0, 2 * Math.PI); ctx.stroke();
    }
    ctx.fillStyle = "#0A1828";ctx.beginPath();ctx.ellipse(cx, cy+65, 85, 135, 0, 0, 2*Math.PI);ctx.fill();
    ctx.strokeStyle = "#CBEFFF";ctx.lineWidth = 10;
    ctx.beginPath();ctx.roundRect(cx-43,cy-105,86,150,42);ctx.stroke();
    ctx.beginPath();ctx.arc(cx,cy-20,95,0,Math.PI);ctx.stroke();
    ctx.beginPath();ctx.moveTo(cx,cy+75);ctx.lineTo(cx,cy+145);ctx.stroke();
    ctx.beginPath();ctx.moveTo(cx-65,cy+145);ctx.lineTo(cx+65,cy+145);ctx.stroke();
    for (let i=0;i<17;i++) {
      const height=16+Math.abs(Math.sin(seconds*3+i*.77))*72;
      ctx.fillStyle=i%2?"#37CCF2":"#F6B951";
      ctx.fillRect(cx-240+i*29,cy+165-height/2,12,height);
    }
    ctx.textAlign="center";ctx.fillStyle="#E8F4FE";ctx.font="700 38px system-ui,sans-serif";
    ctx.fillText("ALPHA PODCAST • FICTIONAL PROGRAM", cx, py+Math.min(90,ph*.16), pw-40);
    ctx.restore();
    layout.slots.forEach(slot => {
      const [slotId,x,y,w,h,color,brand,headline,category]=slot;
      ctx.save();ctx.beginPath();ctx.rect(x,y,w,h);ctx.clip();
      ctx.fillStyle=color;ctx.fillRect(x,y,w,h);
      const img=images[brand];
      if (img) {ctx.globalAlpha=.35;ctx.drawImage(img,0,0,img.width,img.height,x,y,w,h);ctx.globalAlpha=1;}
      const panel=ctx.createLinearGradient(x,y,x+w,y);panel.addColorStop(0,"rgba(0,0,0,.55)");panel.addColorStop(1,"rgba(0,0,0,.04)");ctx.fillStyle=panel;ctx.fillRect(x,y,w,h);
      const side=w<350;
      ctx.textAlign="left";ctx.textBaseline="middle";ctx.fillStyle="#FFFFFF";ctx.shadowColor="#06142A";ctx.shadowBlur=9;
      ctx.font=`800 ${side?34:55}px system-ui,sans-serif`;ctx.fillText(brand,x+24,y+(side?95:h*.37),w-48);
      ctx.font=`600 ${side?19:30}px system-ui,sans-serif`;ctx.fillText(category,x+24,y+(side?142:h*.63),w-48);
      ctx.shadowBlur=0;ctx.strokeStyle="rgba(255,255,255,.86)";ctx.lineWidth=4;ctx.strokeRect(x+2,y+2,w-4,h-4);
      ctx.restore();
    });
    // The label stays inside the protected presentation area, never on a paid slot.
    ctx.fillStyle="rgba(0,9,18,.85)";ctx.fillRect(px,py+ph-42,pw,42);
    ctx.fillStyle="#FFE4A7";ctx.font="700 19px system-ui,sans-serif";ctx.textAlign="center";
    ctx.fillText(`${id} RECTANGLES • FICTIONAL DEMO • CREATOR PERMISSION UNVERIFIED • NOT SENT`, px+pw/2,py+ph-15,pw-20);
  }
  function start(canvas, images, selected) {
    if (!canvas || canvas.tagName !== "CANVAS") throw Error("Canvas required");
    canvas.width=W;canvas.height=H;
    const ctx=canvas.getContext("2d",{alpha:false});if(!ctx)throw Error("Canvas unavailable");
    let enabled=true, fixed=selected==="cycle"?null:String(selected), startTime=performance.now(), timer=0;
    const keys=["2","4","6"];
    function frame() {
      if(!enabled)return;
      const sec=(performance.now()-startTime)/1000;
      const id=fixed??keys[Math.floor(sec/3)%3];
      draw(ctx,id,images,sec);
      canvas.dataset.configuration=id;
      timer=requestAnimationFrame(frame);
    }
    frame();
    return {
      choose(id){if(!fixtures[id] && id!=="cycle")throw Error("Unknown configuration");fixed=id==="cycle"?null:String(id);startTime=performance.now();/* Preserve the single scheduled animation loop; an extra frame() here multiplies requestAnimationFrame chains on every layout selection. */},
      stop(){enabled=false;cancelAnimationFrame(timer);},
      capture(){return new Promise((resolve,reject)=>canvas.toBlob(b=>b?resolve(b):reject(Error("Capture failed")),"image/png"));},
      async record(milliseconds=9400){
        if(typeof MediaRecorder!=="function"||typeof canvas.captureStream!=="function")throw Error("WebM capture unsupported by this browser");
        const prior=fixed;fixed=null;startTime=performance.now();
        const stream=canvas.captureStream(15);
        const type=MediaRecorder.isTypeSupported("video/webm;codecs=vp9")?"video/webm;codecs=vp9":"video/webm";
        const recorder=new MediaRecorder(stream,{mimeType:type});const parts=[];
        return new Promise((resolve,reject)=>{
          recorder.ondataavailable=e=>{if(e.data.size)parts.push(e.data);};
          recorder.onerror=e=>reject(e.error||Error("Recording failed"));
          recorder.onstop=()=>{stream.getTracks().forEach(t=>t.stop());fixed=prior;resolve(new Blob(parts,{type}));};
          recorder.start();setTimeout(()=>{if(recorder.state!=="inactive")recorder.stop();},Math.min(Math.max(milliseconds,500),15000));
        });
      }
    };
  }
  window.AlphaAdvertisingRealEstate=Object.freeze({inventory,prepare,start,validate:()=>Object.values(fixtures).every(check)});
})();
