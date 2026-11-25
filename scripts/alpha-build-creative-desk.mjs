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
  let previews=[];
  function clear(){
    ++generation;
    byId("results").replaceChildren();
    for(const url of previews)URL.revokeObjectURL(url);
    previews=[];
    byId("status").textContent="Generate drafts after editing.";
  }
  byId("brief").addEventListener("input",clear);
  byId("brief").addEventListener("submit",event=>{
    event.preventDefault();clear();
    const token=generation;
    try{
      const form=new FormData(event.currentTarget);
      const brief=Object.fromEntries(form);
      brief.rightsConfirmed=form.get("rightsConfirmed")==="on";
      const pack=renderPack(brief);
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
    }catch(error){clear();byId("status").textContent=error.message;}
  });
}
const html= `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src blob:; connect-src 'none'; form-action 'none'; base-uri 'none'"><title>ALPHA Creative Desk</title><style>
*{box-sizing:border-box}body{font:16px system-ui,sans-serif;background:#f4f3f8;color:#1d1932;margin:0}main{max-width:1180px;margin:auto;padding:28px}h1{font-size:30px;margin:0}p{line-height:1.5}form,article{background:white;border:1px solid #dad7e5;border-radius:12px;padding:20px}form{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:16px}label{display:grid;gap:6px;font-weight:600}input,select,textarea,button{font:inherit;border:1px solid #aaa5ba;border-radius:7px;padding:10px;max-width:100%}textarea{resize:vertical}button{background:#292553;color:white;cursor:pointer}button:disabled{opacity:.5}button:focus-visible,input:focus-visible{outline:3px solid #008e87}#results{display:grid;grid-template-columns:repeat(auto-fit,minmax(245px,1fr));gap:14px}article{min-width:0}article h2{font-size:16px}article img{width:100%;height:280px;object-fit:contain;display:block;margin-bottom:12px}article button{margin:4px}small{font-weight:400}.wide{grid-column:1/-1}.check{display:flex;align-items:start;gap:8px}#status{min-height:28px}
</style></head><body><main><p>ALPHA GLOBAL MEDIA · PRIVATE OPERATOR TOOL</p><h1>Creative Desk</h1><p>Generate editable, review-marked advertising drafts for three channels. No paid AI calls, uploads, customer delivery or publishing. No text is saved by this tool. Use approved brand copy only.</p>
<form id="brief"><label>Brand<input name="brand" required maxlength="28" value="ALPHA GLOBAL MEDIA"></label><label>Headline<input name="headline" required maxlength="76" value="Your story. More impact."></label><label>Call to action<input name="cta" required maxlength="24" value="Request a proposal"></label><label>Palette<select name="palette"><option value="midnight">Midnight</option><option value="ocean">Ocean</option><option value="ember">Ember</option></select></label><label class="wide">Ad description<textarea name="description" required maxlength="104" rows="2">Plan creative campaigns around your audience.</textarea><small>English printable text; shorter copy fits all formats best.</small></label><label class="wide check"><input type="checkbox" name="rightsConfirmed" required> I confirm permission to use this brand and copy.</label><button class="wide" type="submit">Generate three drafts</button></form>
<p id="status" role="status" aria-live="polite">Complete the brief to generate drafts.</p><section id="results" aria-label="Generated draft creatives"></section></main>
<script>${renderPack.toString()}\n(${desk.toString()})();</script></body></html>`;
if(process.argv.length!==3)throw Error("Usage: node scripts/alpha-build-creative-desk.mjs NEW_HTML_FILE");
await writeFile(process.argv[2],html,{flag:"wx"});
console.log("Offline creative desk exported.");
