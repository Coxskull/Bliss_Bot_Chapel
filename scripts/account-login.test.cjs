const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname,'../tools/supabase-login/login.js'),'utf8')
  .replace(/^import .*;\n/, '');

async function fixture({ pathname='/login.html', search='', available=true, signInError=null, throwOnSignIn=false }={}) {
  const handlers={}, redirects=[], calls=[];
  const button={disabled:false};
  const login={ hidden:true, email:{value:'internal@example.invalid'}, password:{value:'synthetic-password'},
    querySelector:()=>button, addEventListener:(name,fn)=>handlers[name]=fn };
  const elements={login,status:{textContent:''},consent:{hidden:true,querySelectorAll:()=>[]},
    scope:{textContent:''},approve:{addEventListener(){}},deny:{addEventListener(){}}};
  const context={URL,URLSearchParams,sessionStorage:{},location:{pathname,search,origin:'https://app.example',assign:u=>redirects.push(u)},
    document:{getElementById:id=>elements[id]},
    fetch:async()=>({ok:available,json:async()=>({url:'https://provider.example',publishableKey:'sb_publishable_fixture',clientId:'client-fixture'})}),
    createClient:()=>{calls.push('client');return {auth:{signInWithPassword:async()=>{
      calls.push('password-login');if(throwOnSignIn) throw new Error('provider detail');return {error:signInError};
    },getUser:async()=>({data:{user:null},error:null})}};}};
  vm.runInNewContext(source,context);
  await new Promise(resolve=>setImmediate(resolve));
  return {elements,handlers,redirects,calls,button};
}
test('standalone page shows credential form only after backend configuration succeeds',async()=>{
  const f=await fixture();assert.equal(f.elements.login.hidden,false);
  assert.equal(f.elements.consent.hidden,true);assert.deepEqual(f.redirects,[]);
});
test('unavailable backend does not expose an active credential form',async()=>{
  const f=await fixture({available:false});assert.equal(f.elements.login.hidden,true);
  assert.deepEqual(f.calls,[]);assert.match(f.elements.status.textContent,/not available/);
});
test('password login continues through backend OIDC instead of granting workspace access',async()=>{
  const f=await fixture({search:'?returnUrl=%2Fproduction.html'});
  await f.handlers.submit({preventDefault(){}});
  assert.deepEqual(f.redirects,['/api/auth/login?returnUrl=%2Fproduction.html']);
  assert.equal(f.elements.login.password.value,'');assert.equal(f.button.disabled,false);
});
test('return destination cannot be external or an arbitrary local endpoint',async()=>{
  for(const target of ['https://outside.example','//outside.example','/api/admin','/production.html?next=outside']){
    const f=await fixture({search:'?returnUrl='+encodeURIComponent(target)});
    await f.handlers.submit({preventDefault(){}});
    assert.deepEqual(f.redirects,['/api/auth/login?returnUrl=%2F']);
  }
});
test('failed and throwing password attempts clear credentials and never redirect',async()=>{
  for(const options of [{signInError:{message:'account does not exist'}},{throwOnSignIn:true}]){
    const f=await fixture(options);await f.handlers.submit({preventDefault(){}});
    assert.deepEqual(f.redirects,[]);assert.equal(f.elements.login.password.value,'');
    assert.equal(f.button.disabled,false);assert.doesNotMatch(f.elements.status.textContent,/account does not exist|provider detail/);
  }
});
test('OAuth consent still requires an authorization request outside standalone entry',async()=>{
  const f=await fixture({pathname:'/oauth-consent.html'});
  assert.equal(f.elements.login.hidden,true);assert.deepEqual(f.calls,[]);
  const valid=await fixture({pathname:'/oauth-consent.html',search:'?authorization_id=fixture'});
  assert.equal(valid.elements.login.hidden,false);assert.deepEqual(valid.redirects,[]);
});
test('login HTML uses password-manager fields and no credential GET action',()=>{
  const html=fs.readFileSync(path.join(__dirname,'../frontend/public/login.html'),'utf8');
  assert.match(html,/type="password" autocomplete="current-password" required/);
  assert.match(html,/type="email" autocomplete="username"/);
  assert.match(html,/<form id="login" hidden>/);assert.doesNotMatch(html,/<form[^>]*action=/);
});
