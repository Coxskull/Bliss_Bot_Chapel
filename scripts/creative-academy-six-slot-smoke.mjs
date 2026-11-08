// ALPHA Creative Academy six-slot native-fit CI: no files uploaded, no provider calls.
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {runInNewContext} from "node:vm";
const root = new URL("../",import.meta.url);
const script=readFileSync(new URL("frontend/public/creative-academy-six-slot.js",root),"utf8");
const page=readFileSync(new URL("frontend/public/creative-academy-six-slot.html",root),"utf8");
const build=readFileSync(new URL("scripts/build-vercel-portal.mjs",root),"utf8");
const win={listeners:new Map(),addEventListener(name,cb){this.listeners.set(name,cb)}};
let readyCallback;
const elements=new Map();
function element(id,initial={}){
  const obj={disabled:false,hidden:false,textContent:"",className:"",listeners:{},
    addEventListener(name,cb){this.listeners[name]=cb},...initial};
  elements.set(id,obj);return obj;
}
const fileInput=element("assets",{files:[]});
const compose=element("compose",{disabled:true});
const exported=element("export",{disabled:true});
const status=element("status");
const proof=element("proof");
const caption=element("canvas-caption");
const inventory=element("inventory",{children:[],replaceChildren(){this.children=[]},append(row){this.children.push(row)}});
const drawings=[];
const canvas=element("stage",{hidden:true,
  getContext(){return {drawImage(...args){drawings.push(args)},imageSmoothingEnabled:false,imageSmoothingQuality:"low"}}
});
const document={
  addEventListener(name,cb){if(name==="DOMContentLoaded")readyCallback=cb},
  getElementById(id){return elements.get(id)},
  createElement(tag){return {tagName:tag,textContent:"",children:[],append(node){this.children.push(node)}}}
};
const sandbox={window:win,document,Uint8Array,Array,Number,Error,Object,RegExp,
 createImageBitmap:async file=>({width:file.width,height:file.height,close(){}})};
runInNewContext(script,sandbox,{timeout:5000});
const api=win.AlphaNativeSixSlot;
assert.ok(api,"API must expose geometry and id matching");
assert.equal(api.slots.length,6);
assert.equal(api.width,1920);assert.equal(api.height,1080);
assert.equal(api.center.x,350);assert.equal(api.center.y,190);
assert.equal(api.center.w,1220);assert.equal(api.center.h,660);
const q=api.verifyGeometry();
assert.equal(q.adPixels,1268400);
assert.equal(q.centerPixels,805200);
assert.equal(q.blankPixels,0);
assert.equal(q.overlaps,0);
assert.equal(api.detect("ALPHA_NATIVE_TOP_BILLBOARD.png"),"TOP_BILLBOARD");
assert.equal(api.detect("podcast_center_ai_crop.png"),"PODCAST_CENTER");
assert.equal(api.detect("unknown_poster.png"),null);
assert.ok(page.includes('name="robots" content="noindex,nofollow,noarchive"'));
assert.ok(page.includes("connect-src 'none'"),"Browser preview must never upload assets");
assert.ok(page.includes("/creative-academy-six-slot.js"));
assert.ok(build.includes("'creative-academy-six-slot.html'"));
assert.ok(build.includes("'creative-academy-six-slot.js'"));
assert.equal(typeof readyCallback,"function");
readyCallback();
assert.equal(compose.disabled,true,"No output before every asset is validated");
assert.equal(inventory.children.length,7);
const fixtures=[...api.slots,api.center].map(rect=>({
 name:rect.id==="PODCAST_CENTER"?"podcast_center_ai_crop.png":"ALPHA_NATIVE_"+rect.id+".png",
 width:rect.id==="PODCAST_CENTER"?973:rect.w,
 height:rect.id==="PODCAST_CENTER"?530:rect.h,
 type:"image/png",size:3000
}));
fileInput.files=fixtures;
await fileInput.listeners.change();
assert.equal(compose.disabled,false);
compose.listeners.click();
assert.equal(canvas.hidden,false);
assert.equal(exported.disabled,false);
assert.equal(drawings.length,7,"Six ad images and one center must render");
for(let i=0;i<6;i++){
 const rect=api.slots[i], args=drawings[i];
 assert.equal(args.length,3,"Native-fit ads must be pasted at intrinsic size without scaling");
 assert.equal(args[1],rect.x);assert.equal(args[2],rect.y);
}
assert.equal(drawings[6].length,9,"Podcast center is fitted separately");
assert.match(proof.textContent,/0 blank pixels/);
assert.match(proof.textContent,/0 overlaps/);
assert.ok(!script.includes("fetch("),"No hidden asset upload");
fileInput.files=fixtures.map(x=>x.name==="ALPHA_NATIVE_LEFT_UPPER.png"?{...x,width:x.width+50}:x);
await fileInput.listeners.change();
assert.equal(compose.disabled,true,"Reject ad images whose dimension does not match placement");
assert.equal(canvas.hidden,true,"Never display an invalid or stale preview");
assert.match(status.textContent,/must be 350×330/);
console.log("ALPHA_SIX_SLOT_NATIVE_FIT_GEOMETRY_IMPORT_RENDER_AND_FAILCLOSE_PASS");