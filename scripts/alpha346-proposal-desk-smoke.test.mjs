import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {runInNewContext} from 'node:vm';
const load=async path=>readFile(new URL(path,import.meta.url),'utf8');
const desk=await load('../tools/commercial/alpha346-proposal-desk.html');
const contact=await load('../frontend/public/contact.html');
const publicBuilder=await load('./build-vercel-portal.mjs');

test('ALPHA346 internal proposal desk blocks network and third-party code',()=>{
  assert.match(desk,/connect-src 'none'/);
  assert.match(desk,/default-src 'none'/);
  for(const source of ['fetch(', 'XMLHttpRequest', 'WebSocket', 'sendBeacon(', 'localStorage','sessionStorage','src="https:', 'action="https:'])
    assert.ok(!desk.includes(source),'forbidden token '+source);
  assert.match(desk,/form-action 'none'/);
  assert.match(desk,/No entries are transmitted or saved to a server/);
});

test('proposal editor enforces internal review watermark, fixed five planning deliverables and zero payment',()=>{
  const ids=['campaign discovery brief','creator and channel suitability plan','creative direction outline','milestone and coordination schedule','final planning summary'];
  for(const phrase of ids) assert.ok(desk.toLowerCase().includes(phrase));
  assert.equal((desk.match(/data-approval="/g)||[]).length,8);
  for(const phrase of ['INTERNAL REVIEW DRAFT','NOT A CUSTOMER OFFER','GLOBAL STOP ENGAGED','PAYMENT DISABLED','DELIVERY NOT SENT'])
    assert.ok(desk.includes(phrase));
  assert.ok(desk.includes('.textContent=value'));
  assert.ok(!desk.includes('.innerHTML='));
});

test('owner-selected Gmail inbox is shown as inquiry-only, not approved post-purchase support',()=>{
  assert.match(contact,/mailto:dejesuserwin1@gmail.com/);
  assert.match(contact,/paid-customer support service and response time are not yet verified/);
  assert.match(desk,/dejesuserwin1@gmail.com/);
  assert.match(desk,/VERIFY MONITORING BEFORE PAID ORDERS/);
});

test('internal proposal desk is NEVER copied into public Vercel allowlist',()=>{
  assert.ok(!publicBuilder.includes('alpha346-proposal-desk.html'));
  assert.ok(!publicBuilder.includes('tools/commercial/'));
  assert.match(contact,/Online paid orders are <strong>not currently available/);
});

test('planning desk includes five bounded authorable print sections without modifying the public portal',()=>{
  const ids=['brief','channels','creative','milestones','summary'];
  assert.equal((desk.match(/data-packet-field="/g)||[]).length,5);
  assert.equal((desk.match(/data-deliverable="/g)||[]).length,5);
  for(const id of ids){
    assert.ok(desk.includes('data-packet-field="'+id+'" maxlength="1500"'),'editable field '+id);
    assert.ok(desk.includes('data-deliverable="'+id+'"'),'print section '+id);
    assert.ok(desk.includes('id="v-'+id+'"'),'live safe preview '+id);
  }
  assert.match(desk,/page-break-before:always/);
  assert.match(desk,/break-before:page/);
  assert.match(desk,/FICTIONAL DEMO ONLY/);
  assert.match(desk,/NOT A CUSTOMER OFFER/);
  assert.match(desk,/PAYMENT DISABLED/);
  assert.match(desk,/DELIVERY NOT SENT/);
  assert.match(desk,/demoMode=false/);
  assert.match(desk,/for\(const id of deliverableIds\)if\(!value\[id\]\)missing.push/);
  assert.ok(!publicBuilder.includes('alpha346-proposal-desk.html'));
});

test('offline planning packet preview, fictional demo, print and clear behave safely with adversarial text',()=>{
  const code=desk.match(/<script>\s*([\s\S]*?)<\/script>/i)?.[1];
  assert.ok(code,'one inline offline desk script');
  const ids=['company','contact','market','ref','quote','goal','scope','schedule','price','currency','revisions','support','brief','channels','creative','milestones','summary'];
  const elements=new Map();
  const node=id=>{
    if(!elements.has(id)){
      const listeners=new Map();
      const el={value:'',textContent:'',className:'',listeners,
        addEventListener:(event,callback)=>listeners.set(event,callback)};
      Object.defineProperty(el,'innerHTML',{set:()=>{throw Error('Unsafe HTML insertion')}});
      elements.set(id,el);
    }
    return elements.get(id);
  };
  const approvals=Array.from({length:8},(_,i)=>({checked:false,getAttribute:key=>key==='data-approval'?'gate-'+i:null}));
  let printCount=0;
  const document={
    getElementById:id=>node(id),
    querySelectorAll:q=>{assert.equal(q,'[data-approval]');return approvals}
  };
  runInNewContext(code,{document,window:{print:()=>{printCount++}}},{timeout:1000});
  assert.match(node('v-brief').textContent,/NOT YET PREPARED/);
  assert.match(node('status').textContent,/INTERNAL DRAFT/);
  assert.ok(node('status').textContent.includes('pending'));
  node('demoButton').listeners.get('click')();
  assert.match(node('v-company').textContent,/FICTIONAL/);
  assert.match(node('status').textContent,/FICTIONAL DEMO ONLY/);
  for(const id of ['brief','channels','creative','milestones','summary'])
    assert.ok(node('v-'+id).textContent.length>80,id+' demo content');
  assert.match(node('v-price').textContent,/NOT SET \/ NOT APPROVED/);
  assert.ok(approvals.every(x=>!x.checked),'fictional sample cannot fabricate approvals');
  const hostile='<img src=x onerror=alert(1)> & <script>never execute</script>';
  node('brief').value=hostile;
  node('brief').listeners.get('input')();
  node('previewButton').listeners.get('click')();
  assert.equal(node('v-brief').textContent,hostile,'text must remain literal');
  assert.doesNotMatch(node('status').textContent,/FICTIONAL DEMO ONLY/,'manual edits clear demo mode');
  node('printButton').listeners.get('click')();
  assert.equal(printCount,1,'printing is local; no network');
  node('clearButton').listeners.get('click')();
  for(const id of ids)assert.equal(node(id).value,'',id+' was not cleared');
  assert.match(node('v-brief').textContent,/NOT YET PREPARED/);
  assert.match(node('v-price').textContent,/NOT SET \/ NOT APPROVED/);
  assert.ok(approvals.every(x=>!x.checked));
});
