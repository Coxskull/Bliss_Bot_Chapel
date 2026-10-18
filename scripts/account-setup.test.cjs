const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const source=fs.readFileSync('tools/supabase-login/account-setup.js','utf8').replace(/^import .*;\n/,'');
const validHash='#type=invite&access_token=synthetic-access&refresh_token=synthetic-refresh';
async function fixture({hash=validHash,available=true,sessionError=false,userError=false,changedUser=false,updateError=false}={}) {
  const handlers={},calls=[],replaced=[];
  const button={disabled:false};
  const form={hidden:true,password:{value:'synthetic-password'},confirmation:{value:'synthetic-password'},querySelector:()=>button,addEventListener:(n,h)=>handlers[n]=h};
  const elements={'password-setup':form,status:{textContent:''},continue:{hidden:true}};
  let userReads=0;
  const auth={setSession:async()=>{calls.push('set-session');return {error:sessionError};},getUser:async()=>({error:userError,data:{user:{email:'internal@example.invalid',id:changedUser&&++userReads>1?'other-user':'invited-user'}}}),updateUser:async()=>{calls.push('update-password');return {error:updateError};}};
  vm.runInNewContext(source,{URLSearchParams,location:{hash},history:{replaceState:(...a)=>replaced.push(a[2])},document:{getElementById:id=>elements[id]},sessionStorage:{},fetch:async()=>({ok:available,json:async()=>({url:'https://provider.example',publishableKey:'sb_publishable_fixture'})}),createClient:()=>({auth})});
  await new Promise(resolve=>setImmediate(resolve));
  return {handlers,calls,replaced,form,elements,button};
}
test('invitation URL secrets are removed before any setup interaction',async()=>{
  const f=await fixture();assert.deepEqual(f.replaced,['/account-setup.html']);assert.equal(f.form.hidden,false);
});
test('missing, malformed, and unrelated sessions cannot expose password setup',async()=>{
  for(const hash of ['', '#type=signup&access_token=x&refresh_token=y','#type=invite&access_token=x','#error_code=otp_expired']) {
    const f=await fixture({hash});assert.equal(f.form.hidden,true);assert.deepEqual(f.calls,[]);
  }
});
test('unavailable backend never consumes invitation or opens the form',async()=>{
  const f=await fixture({available:false});assert.equal(f.form.hidden,true);assert.deepEqual(f.calls,[]);
});
test('rejected token or unverified identity keeps setup hidden',async()=>{
  for(const options of [{sessionError:true},{userError:true}]) {const f=await fixture(options);assert.equal(f.form.hidden,true);}
});
test('successful password setup shows normal login without issuing application access',async()=>{
  const f=await fixture();await f.handlers.submit({preventDefault(){}});
  assert.deepEqual(f.calls,['set-session','update-password']);assert.equal(f.form.hidden,true);assert.equal(f.elements.continue.hidden,false);
  assert.equal(f.form.password.value,'');assert.equal(f.form.confirmation.value,'');
});
test('short or mismatched passwords are refused and cleared',async()=>{
  for(const value of ['short','a-different-long-password']) {const f=await fixture();f.form.confirmation.value=value;await f.handlers.submit({preventDefault(){}});assert.deepEqual(f.calls,['set-session']);assert.equal(f.form.password.value,'');}
});
test('identity change or provider error never reports password success',async()=>{
  for(const options of [{changedUser:true},{updateError:true}]) {const f=await fixture(options);await f.handlers.submit({preventDefault(){}});assert.equal(f.elements.continue.hidden,true);assert.equal(f.form.password.value,'');assert.equal(f.button.disabled,false);}
});
test('existing invitation links move only to same-origin setup with fragment intact',()=>{
  const source=fs.readFileSync('frontend/public/invitation-entry.js','utf8');
  for(const hash of [validHash,'#type=recovery&access_token=x&refresh_token=y','#error_code=otp_expired']) {const redirects=[];vm.runInNewContext(source,{URLSearchParams,location:{hash,replace:u=>redirects.push(u)}});assert.deepEqual(redirects,['/account-setup.html'+hash]);}
  const redirects=[];vm.runInNewContext(source,{URLSearchParams,location:{hash:'#home',replace:u=>redirects.push(u)}});assert.deepEqual(redirects,[]);
});
