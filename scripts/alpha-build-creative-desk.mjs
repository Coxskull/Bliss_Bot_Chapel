// Build a private, offline operator workspace using the existing creative renderer.
// Node 20+: node scripts/alpha-build-creative-desk.mjs alpha-creative-desk.html
import { writeFile } from "node:fs/promises";
import { renderPack } from "./alpha-manual-creative-export.mjs";

function desk() {
  const byId=id=>document.getElementById(id);
  let generation=0;
  function save(blob,name){
    const url=URL.createObjectURL(blob);
    const a=document.createElement("a");a.href=url;a.download=name;a.click();
    setTimeout(()=>URL.revokeObjectURL(url),60000);
  }
  async function png(asset){
    const url=URL.createObjectURL(new Blob([asset.svg],{type:"image/svg+xml;charset=utf-8"}));
    try{
      const img=new Image();img.src=url;await img.decode();
      const canvas=document.createElement("canvas");canvas.width=asset.width;canvas.height=asset.height;
      canvas.getContext("2d").drawImage(img,0,0);
      return await new Promise((ok,no)=>canvas.toBlob(b=>b?ok(b):no(Error("PNG encoding failed")),"image/png"));
    }finally{URL.revokeObjectURL(url);}
  }
  async function localRaster(file) {
    if(!file||!file.size)return "";
    if(file.size>10*1024*1024||!["image/png","image/jpeg","image/webp"].includes(file.type))
      throw Error("Choose a PNG, JPEG or WebP file up to 10 MB.");
    const bytes=new Uint8Array(await file.slice(0,12).arrayBuffer());
    const png=bytes.length>=8&&[137,80,78,71,13,10,26,10].every((v,i)=>bytes[i]===v);
    const jpeg=bytes[0]===255&&bytes[1]===216&&bytes[2]===255;
    const webp=String.fromCharCode(...bytes.slice(0,4))==="RIFF"&&String.fromCharCode(...bytes.slice(8,12))==="WEBP";
    if(!((file.type==="image/png"&&png)||(file.type==="image/jpeg"&&jpeg)||(file.type==="image/webp"&&webp)))throw Error("Image content does not match its file type.");
    const image=await createImageBitmap(file);
    try{
      if(image.width>6000||image.height>6000||image.width*image.height>20000000)throw Error("Image too large; resize it before importing.");
      const scale=Math.min(1,1400/Math.max(image.width,image.height));
      const canvas=document.createElement("canvas");canvas.width=Math.max(1,Math.round(image.width*scale));canvas.height=Math.max(1,Math.round(image.height*scale));
      canvas.getContext("2d").drawImage(image,0,0,canvas.width,canvas.height);
      return canvas.toDataURL("image/png");
    }finally{image.close();}
  }
  function withAssets(asset,photo,logo){
    if(!photo&&!logo)return asset;
    const w=asset.width,h=asset.height,wide=w>h;
    const bg=asset.svg.match(/<rect width="\d+" height="\d+" fill="(#[A-Fa-f0-9]{6})"/)[1];
    const mainW=wide?Math.round(w*.62):w,mainH=wide?h:Math.round(h*.60);
    const px=wide?mainW:0,py=wide?0:mainH,pw=w-px,ph=h-py,pad=Math.round(Math.min(pw,ph)*.055);
    const logoH=logo&&photo?Math.round(ph*.22):ph-2*pad;
    const img=(data,x,y,width,height)=>data?'<image href="'+data+'" x="'+x+'" y="'+y+'" width="'+width+'" height="'+height+'" preserveAspectRatio="xMidYMid meet"/>':"";
    const photoY=py+pad+(logo?logoH+pad:0);
    const nested=asset.svg.replace('<svg xmlns=', '<svg x="0" y="0" xmlns=').replace('width="'+w+'" height="'+h+'"','width="'+mainW+'" height="'+mainH+'"');
    const svg='<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'" height="'+h+'" viewBox="0 0 '+w+' '+h+'"><rect width="'+w+'" height="'+h+'" fill="'+bg+'"/>'+nested+
      '<rect x="'+(px+pad)+'" y="'+(py+pad)+'" width="'+(pw-2*pad)+'" height="'+(ph-2*pad)+'" rx="18" fill="#FFFFFF" fill-opacity=".10"/>'+
      img(logo,px+pad,py+pad,pw-2*pad,logoH)+img(photo,px+pad,photoY,pw-2*pad,py+ph-pad-photoY)+'</svg>';
    return {...asset,svg};
  }
  let previews=[];
  function clear(){
    ++generation;
    byId("results").replaceChildren();
    for(const url of previews)URL.revokeObjectURL(url);
    previews=[];
    byId("status").textContent="Generate drafts after editing.";
  }
  byId("brief").addEventListener("input",clear);
  byId("brief").addEventListener("submit",async event=>{
    event.preventDefault();clear();
    const token=generation;
    try{
      const form=new FormData(event.currentTarget);
      const brief=Object.fromEntries(form);
      brief.rightsConfirmed=form.get("rightsConfirmed")==="on";
      const base=renderPack(brief);
      byId("status").textContent="Preparing local assets…";
      const [photo,logo]=await Promise.all([localRaster(form.get("productPhoto")),localRaster(form.get("brandLogo"))]);
      if(token!==generation)return;
      const pack=base.map(asset=>withAssets(asset,photo,logo));
      for(const asset of pack){
        const card=document.createElement("article");
        const heading=document.createElement("h2");heading.textContent=asset.name.replace(".svg","")+" · "+asset.width+" × "+asset.height;
        const image=document.createElement("img");image.alt="Draft "+asset.name;
        const blob=new Blob([asset.svg],{type:"image/svg+xml;charset=utf-8"});
        image.src=URL.createObjectURL(blob);previews.push(image.src);
        const svgButton=document.createElement("button");svgButton.type="button";svgButton.textContent="Download SVG";
        svgButton.addEventListener("click",()=>save(blob,asset.name));
        const pngButton=document.createElement("button");pngButton.type="button";pngButton.textContent="Download PNG";
        pngButton.addEventListener("click",async()=>{
          pngButton.disabled=true;
          try{const data=await png(asset);if(token===generation)save(data,asset.name.replace(".svg",".png"));}
          catch(error){if(token===generation)byId("status").textContent=error.message;}
          finally{pngButton.disabled=false;}
        });
        card.append(heading,image,svgButton,pngButton);byId("results").append(card);
      }
      byId("status").textContent="3 drafts ready. Review the wording and layout before use.";
    }catch(error){if(token===generation){clear();byId("status").textContent=error.message;}}
  });
}
const html= `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src blob: data:; connect-src 'none'; form-action 'none'; base-uri 'none'"><title>ALPHA Creative Desk</title><style>
*{box-sizing:border-box}body{font:16px system-ui,sans-serif;background:#f4f3f8;color:#1d1932;margin:0}main{max-width:1180px;margin:auto;padding:28px}h1{font-size:30px;margin:0}p{line-height:1.5}form,article{background:white;border:1px solid #dad7e5;border-radius:12px;padding:20px}form{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:16px}label{display:grid;gap:6px;font-weight:600}input,select,textarea,button{font:inherit;border:1px solid #aaa5ba;border-radius:7px;padding:10px;max-width:100%}textarea{resize:vertical}button{background:#292553;color:white;cursor:pointer}button:disabled{opacity:.5}button:focus-visible,input:focus-visible{outline:3px solid #008e87}#results{display:grid;grid-template-columns:repeat(auto-fit,minmax(245px,1fr));gap:14px}article{min-width:0}article h2{font-size:16px}article img{width:100%;height:280px;object-fit:contain;display:block;margin-bottom:12px}article button{margin:4px}small{font-weight:400}.wide{grid-column:1/-1}.check{display:flex;align-items:start;gap:8px}#status{min-height:28px}
</style></head><body><main><p>ALPHA GLOBAL MEDIA · PRIVATE OPERATOR TOOL</p><h1>Creative Desk</h1><p>Generate editable, review-marked advertising drafts for three channels. No paid AI calls, uploads, customer delivery or publishing. No text is saved by this tool. Use approved brand copy and images only. Imported images stay in this browser and are embedded in downloaded files.</p>
<form id="brief"><label>Brand<input name="brand" required maxlength="28" value="ALPHA GLOBAL MEDIA"></label><label>Headline<input name="headline" required maxlength="76" value="Your story. More impact."></label><label>Call to action<input name="cta" required maxlength="24" value="Request a proposal"></label><label>Palette<select name="palette"><option value="midnight">Midnight</option><option value="ocean">Ocean</option><option value="ember">Ember</option></select></label><label class="wide">Ad description<textarea name="description" required maxlength="104" rows="2">Plan creative campaigns around your audience.</textarea><small>English printable text; shorter copy fits all formats best.</small></label><label>Product photo (optional)<input type="file" name="productPhoto" accept="image/png,image/jpeg,image/webp"><small>PNG, JPEG, WebP; up to 10 MB.</small></label><label>Brand logo (optional)<input type="file" name="brandLogo" accept="image/png,image/jpeg,image/webp"><small>Transparent PNG recommended.</small></label><label class="wide check"><input type="checkbox" name="rightsConfirmed" required> I confirm permission to use this brand, copy and imported images.</label><button class="wide" type="submit">Generate three drafts</button></form>
<p id="status" role="status" aria-live="polite">Complete the brief to generate drafts.</p><section id="results" aria-label="Generated draft creatives"></section></main>
<script>${renderPack.toString()}\n(${desk.toString()})();</script></body></html>`;
if(process.argv.length!==3)throw Error("Usage: node scripts/alpha-build-creative-desk.mjs NEW_HTML_FILE");
await writeFile(process.argv[2],html,{flag:"wx"});
console.log("Offline creative desk exported.");
