// ALPHA manual commercial creative export; no network, API key, customer delivery or Factory dispatch.
// Node 20+. Usage: node scripts/alpha-manual-creative-export.mjs brief.json NEW_OUTPUT_DIRECTORY
// Self-test: node scripts/alpha-manual-creative-export.mjs --self-test
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { createHash } from "node:crypto";
import assert from "node:assert/strict";
import { pathToFileURL } from "node:url";

export function renderPack(brief) {
  if (!brief || typeof brief !== "object" || Array.isArray(brief)) throw Error("Expected a brief object");
  const fields={brand:28,headline:76,description:104,cta:24}, b={};
  for(const [key,max] of Object.entries(fields)){
    const v=brief[key];
    if(typeof v!=="string"||!/^[\x20-\x7E]+$/.test(v)||!v.trim()||v.trim().length>max)throw Error("Invalid "+key+" (English printable text, max "+max+")");
    b[key]=v.trim().replace(/ +/g," ");
  }
  const palettes={midnight:["#151331","#292553","#A4F2DB","#FFFFFF"],ocean:["#092D38","#124953","#F7D68A","#FFFFFF"],ember:["#38201F","#62312D","#FFE0AA","#FFFFFF"]};
  if(!Object.hasOwn(palettes,brief.palette))throw Error("Choose midnight, ocean or ember");
  if(brief.rightsConfirmed!==true)throw Error("Operator must confirm copy/brand rights");
  const [bg,panel,accent,white]=palettes[brief.palette];
  const esc=s=>s.replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&apos;"}[c]));
  function wrap(s,n){const out=[];let line="";for(const word of s.split(" ")){if(word.length>n)throw Error("Word too long; shorten copy");if(line&&line.length+1+word.length>n){out.push(line);line=word;}else line=line?line+" "+word:word;}if(line)out.push(line);return out;}
  return [["square",1080,1080],["story",1080,1920],["landscape",1920,1080]].map(([name,w,h])=>{
    const x=Math.round(w*.085), contentW=Math.round(w*.83);
    const titleSize=Math.round(Math.min(w*.055,h*.073)),bodySize=Math.round(Math.min(w*.029,h*.037)),brandSize=Math.round(Math.min(w*.027,h*.035));
    const title=wrap(b.headline,Math.floor(contentW/titleSize)),body=wrap(b.description,Math.floor(contentW/bodySize));
    const titleY=Math.round(h*.28), titleStep=Math.round(titleSize*1.18),bodyStep=Math.round(bodySize*1.4);
    const bodyY=titleY+(title.length-1)*titleStep+Math.round(titleSize*1.3);
    const ctaY=bodyY+(body.length-1)*bodyStep+Math.round(bodySize*1.5);
    const buttonH=Math.round(bodySize*2.4),buttonW=Math.max(Math.round(w*.29),Math.ceil(b.cta.length*bodySize+bodySize*2));
    if(ctaY+buttonH>h*.88||buttonW>contentW||b.brand.length*brandSize>contentW)throw Error("Copy does not fit all formats; shorten it");
    const lines=(arr,y,step,size,weight,color)=>arr.map((line,i)=>`<text x="${x}" y="${y+i*step}" font-size="${size}" font-weight="${weight}" fill="${color}">${esc(line)}</text>`).join("\n");
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(b.brand+" — "+b.headline)}">
<title>${esc(b.brand+" — "+b.headline)}</title>
<rect width="${w}" height="${h}" fill="${bg}"/>
<circle cx="${Math.round(w*.94)}" cy="${Math.round(h*.13)}" r="${Math.round(w*.28)}" fill="${panel}"/>
<circle cx="${Math.round(w*.96)}" cy="${Math.round(h*.13)}" r="${Math.round(w*.19)}" fill="none" stroke="${accent}" stroke-opacity=".35" stroke-width="4"/>
<rect x="${x}" y="${Math.round(h*.19)}" width="${Math.round(w*.095)}" height="8" rx="4" fill="${accent}"/>
<g font-family="Arial, Helvetica, sans-serif">
<text x="${x}" y="${Math.round(h*.14)}" font-size="${brandSize}" font-weight="700" fill="${accent}">${esc(b.brand)}</text>
${lines(title,titleY,titleStep,titleSize,700,white)}
${lines(body,bodyY,bodyStep,bodySize,400,white)}
<rect x="${x}" y="${ctaY}" width="${buttonW}" height="${buttonH}" rx="${Math.round(buttonH*.22)}" fill="${accent}"/>
<text x="${x+Math.round(buttonW/2)}" y="${ctaY+Math.round(buttonH*.65)}" font-size="${bodySize}" font-weight="700" text-anchor="middle" fill="${bg}">${esc(b.cta)}</text>
<path d="M${x} ${Math.round(h*.92)} H${w-x}" stroke="${accent}" stroke-opacity=".45" stroke-width="2"/>
<text x="${x}" y="${Math.round(h*.96)}" font-size="${Math.round(Math.min(w*.019,h*.026))}" fill="${white}">DRAFT / REVIEW BEFORE PUBLICATION</text>
</g></svg>\n`;
    return {name:name+".svg",width:w,height:h,svg};
  });
}

const example = {
  "brand": "ALPHA GLOBAL MEDIA",
  "headline": "Your story. More impact.",
  "description": "Plan creative campaigns around your audience.",
  "cta": "Request a proposal",
  "palette": "midnight",
  "rightsConfirmed": true
};
function selfTest() {
  const pack=renderPack(example);
  assert.deepEqual(pack.map(({width,height})=>[width,height]),[[1080,1080],[1080,1920],[1920,1080]]);
  assert.ok(pack.every(x=>x.svg.includes("DRAFT / REVIEW BEFORE PUBLICATION")));
  assert.ok(renderPack({...example,headline:"A & B < C"}).every(x=>x.svg.includes("A &amp; B &lt; C")));
  for(const change of [{rightsConfirmed:false},{palette:"constructor"},{headline:"x".repeat(100)},{brand:"bad\ninput"}])assert.throws(()=>renderPack({...example,...change}));
  console.log("PASS: three draft formats, XML escaping and invalid brief rejection");
}
async function main() {
  const args=process.argv.slice(2);
  if(args.length===1&&args[0]==="--self-test"){selfTest();return;}
  if(args.length!==2)throw Error("Usage: node scripts/alpha-manual-creative-export.mjs brief.json NEW_OUTPUT_DIRECTORY");
  const [input,output]=args;
  const pack=renderPack(JSON.parse(await readFile(input,"utf8")));
  await mkdir(output);
  const files=[];
  for(const {name,width,height,svg} of pack){
    await writeFile(join(output,name),svg,{flag:"wx"});
    files.push({name,width,height,sha256:createHash("sha256").update(svg).digest("hex")});
  }
  const manifest={schema:"alpha-manual-creative-export/v1",status:"DRAFT_PENDING_HUMAN_REVIEW",delivery:"NOT_SENT",providerCalls:0,providerCostMinor:0,otherCosts:"NOT_MEASURED",files};
  await writeFile(join(output,"manifest.json"),JSON.stringify(manifest,null,2)+"\n",{flag:"wx"});
  console.log("Exported "+files.length+" draft creatives and manifest.");
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href)main().catch(e=>{console.error(e.message);process.exitCode=1;});
