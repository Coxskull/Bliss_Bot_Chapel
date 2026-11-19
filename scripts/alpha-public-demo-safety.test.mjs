import { readFile, stat } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const source=resolve(root,'frontend/public');
const output=resolve(root,'.vercel-portal');
const get=async(p)=>readFile(p,'utf8');
const assert=(value,label)=>{if(!value)throw new Error('PUBLIC_AD_DEMO_DENIED: '+label)};
const page=await get(resolve(output,'real-estate-showcase.html'));
const homepage=await get(resolve(output,'index.html'));
assert(page.includes('id="stage" width="1920" height="1080"'), '1920x1080 demo canvas missing');
assert(page.includes('NOT A SELLABLE PRODUCT')&&page.includes('CREATOR'), 'fictional limitation missing');
assert(homepage.includes('href="/real-estate-showcase.html"'),'homepage demo route missing');
assert(homepage.includes('Public account registration') || homepage.includes('Account registration not yet available') || homepage.includes('online payments are not available'),'existing commercial limitations changed');
const src=await get(resolve(output,'real-estate-showcase.js'));
assert(src.includes('["LEFT_PURPLE", 0, 180, 270, 720,') && src.includes('["RIGHT_GREEN", 1650, 180, 270, 720,'),'six-layout side bands not repaired');
assert(src.includes('Math.min(w/img.width,h/img.height)'), 'asset contain-not-stretch omitted');
assert(src.includes('campaignReady: false') && src.includes('delivery: "NOT_SENT"'), 'draft status not fail closed');
for (const file of ['real-estate-showcase.js','real-estate-showcase-page.js','ad-composer.js']){
 const text=await get(resolve(output,file));
 assert(!/\b(fetch|XMLHttpRequest|WebSocket|EventSource|sendBeacon)\s*\(/.test(text), file+' has network path');
 assert(!/https?:\/\//.test(text),file+' contains external link');
 assert((await stat(resolve(output,file))).size<20000, file+' unexpectedly large');
}
for(const file of ['advertiser-login.html','join-advertiser.html','support.html','alpha-privacy.html'])
 assert((await stat(resolve(output,file))).size>100,file+' existing public portal protection lost');
console.log('ALPHA_PUBLIC_FICTIONAL_DEMO_SOURCE_PASS: 2/4/6 geometry, NO_API, NO_PAYMENTS, NOT_SENT, existing website preserved');
