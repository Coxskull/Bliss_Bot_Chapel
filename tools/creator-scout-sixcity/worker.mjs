/**
 * ALPHA GLOBAL MEDIA: isolated, fail-closed six-city Creator Scout Queue consumer.
 * REVIEW CANDIDATE ONLY. This code does not modify the existing Worker or Queue consumer.
 * Do not attach a Queue trigger before deployment approval, fresh Captain signatures,
 * and verified queue-pause authority. No AI, outreach, campaigns or payments.
 */
export const QUEUE_NAME = 'alpha-creator-scout-staging-trigger';
export const EXPECTED_QUEUE_ID = 'cecb61fd9d154360a686febf8693db36';
export const EXPECTED_ACCOUNT_ID = '2f29b8ee363840bd737f867a54019513';
export const BOUNDARY = 'ALPHA-CREATOR-SCOUT-ONESHOT-v2-MANIFEST';
export const PINS = Object.freeze({
  CO: ['7cecf6661c9028e0d997a9daad05025e7dec3116c04e9115ad00798f0f24c2d1', 'segunquienpodcast.com'],
  DO: ['eb0d4c155222b3ed94614d14179429020436de0a0ebd3251e2a762ee19dee51a', 'www.basadospodcast.com'],
  ID: ['185f6c1fca729b3994fed3c3e892a597f49e1a9d50d92f6562ab8078a9de517b', 'www.maknagroup.com'],
  MY: ['82e271e31fd18e9ae0738a67a74164644ccaf18adb2f0a222d62036ab8a588c0', 'aididitmyway.com'],
  PA: ['800acf51d289dc04a98eeeec3011794adfda7d8da8c3c8437c323a1be8e41638', 'flowlamusica.com'],
  PH: ['7ae47e1888f7bd9ac2067e9a513d05f07310d11474b6d411a6a9c43623651fb0', 'www.chinoy.tv']
});
const encoder = new TextEncoder();
const hex = bytes => Array.from(new Uint8Array(bytes), x => x.toString(16).padStart(2, '0')).join('');
const taskCode = id => /^AGM-SCOUT-REQ-20261024-(CO|DO|ID|MY|PA|PH)$/.exec(id||'')?.[1]||null;
const keys = 'approved_at,approved_by,expires_at,manifest_sha256,nonce,signature,task_id';
const stamp = s => typeof s === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{3})?Z$/.test(s) &&
  Number.isFinite(Date.parse(s)) && [new Date(s).toISOString(),new Date(s).toISOString().replace('.000Z','Z')].includes(s);
const decode64 = x => {
  if(typeof x!=='string' || !/^[A-Za-z0-9+/]+={0,2}$/.test(x) || x.length%4 !== 0)throw Error('INVALID_BASE64');
  return Uint8Array.from(atob(x), c => c.charCodeAt(0));
};
export const canonical = e => [BOUNDARY, e.task_id,e.manifest_sha256,e.approved_by,e.approved_at,e.expires_at,e.nonce].join('\n');
export const sha256 = async str => hex(await crypto.subtle.digest('SHA-256', typeof str==='string'?encoder.encode(str):str));
export async function verifyApproval(e, publicKeyBase64, now=Date.now()) {
  if(!e || Array.isArray(e) || typeof e!=='object' || Object.keys(e).sort().join(',')!==keys)
    return {ok:false,reason:'INVALID_FIELDS'};
  const code = taskCode(e.task_id);
  if(!code || e.manifest_sha256!==PINS[code][0])return {ok:false,reason:'TASK_OR_MANIFEST_NOT_PINNED'};
  if(typeof e.approved_by!=='string'||!/^[A-Za-z][A-Za-z0-9 _-]{2,70}$/.test(e.approved_by))return {ok:false,reason:'INVALID_APPROVER'};
  if(!stamp(e.approved_at)||!stamp(e.expires_at))return {ok:false,reason:'INVALID_TIMESTAMPS'};
  const issued=Date.parse(e.approved_at),expiry=Date.parse(e.expires_at);
  if(issued>now || expiry<=now || expiry-issued!==600000 || now-issued>=600000)return {ok:false,reason:'EXPIRED_OR_INVALID_WINDOW'};
  if(typeof e.nonce!=='string'|| !/^[a-f0-9]{32}$/.test(e.nonce))return {ok:false,reason:'INVALID_NONCE'};
  if(!publicKeyBase64)return {ok:false,reason:'PUBLIC_KEY_UNAVAILABLE'};
  try {
    const sig=decode64(e.signature),spki=decode64(publicKeyBase64);
    if(sig.length!==64 || spki.length!==44)return {ok:false,reason:'KEY_OR_SIGNATURE_LENGTH'};
    const key=await crypto.subtle.importKey('spki',spki,{name:'Ed25519'},false,['verify']);
    const valid=await crypto.subtle.verify('Ed25519',key,sig,encoder.encode(canonical(e)));
    return {ok:valid,reason:valid?'VALID_CAPTAIN_SIGNATURE':'BAD_SIGNATURE',code};
  } catch {return {ok:false,reason:'SIGNATURE_CHECK_ERROR'};}
}
export function inspectSeed(seedJson, taskId, digest) {
  const code=taskCode(taskId);
  if(!code || digest!==PINS[code][0])throw Error('TASK_NOT_PINNED');
  const seed=JSON.parse(seedJson);
  if(seed.task_id!==taskId || seed.schema!=='AGM_SIX_CITY_CREATOR_SOURCE_CHECK_V1' ||
     seed.scope!=='STAGING_CREATOR_PUBLIC_SOURCE_ONLY' || seed.market?.country_code!==code ||
     seed.execution_policy?.ai_inference_calls!==0 || seed.execution_policy?.max_spend_cents!==0 ||
     seed.execution_policy?.no_outreach!==true || seed.execution_policy?.no_production_writes!==true ||
     seed.execution_policy?.requires_captain_signed_envelope!==true ||
     !Array.isArray(seed.seeds) || seed.seeds.length!==1)throw Error('SEED_POLICY_INVALID');
  const item=seed.seeds[0];
  const url=new URL(item.official_url);
  if(url.protocol!=='https:'||url.username||url.password||url.port||url.hash||url.hostname!==PINS[code][1] ||
     !Array.isArray(item.source_urls) || item.source_urls.length!==1 || item.source_urls[0]!==item.official_url ||
     typeof item.verify_marker!=='string' || item.verify_marker.length<3 || item.verify_marker.length>100)
    throw Error('SOURCE_NOT_ALLOWLISTED');
  return item;
}
async function flagsAreEnabled(db) {
  const r=await db.prepare("SELECT key,value FROM scout_control WHERE key IN ('captain_stop','scout_enabled')").all();
  const o=Object.fromEntries((r.results||[]).map(x=>[x.key,x.value]));
  return o.captain_stop==='0' && o.scout_enabled==='1';
}
export function envConfigured(env) {
  return !!(env.SCOUT_DB && env.SCOUT_RUN_ENABLED==='YES' && env.SCOUT_COST_CAP_READY==='YES' &&
    env.SCOUT_QUEUE_AUTOPAUSE_READY==='YES' && env.SCOUT_QUEUE_CONTROL_TOKEN &&
    env.CAPTAIN_PUBLIC_KEY_SPKI_B64 && env.CF_ACCOUNT_ID===EXPECTED_ACCOUNT_ID &&
    env.SCOUT_QUEUE_ID===EXPECTED_QUEUE_ID);
}
export async function sourceCheck(url, marker, fetcher=fetch) {
  // Direct HTTPS fetch only. Redirects explicitly rejected (no cross-host hop/SSRF).
  const response=await fetcher(url,{method:'GET',redirect:'manual',headers:{'Accept':'text/html,text/plain'},signal:AbortSignal.timeout(9000)});
  if(response.status>=300 && response.status<400)return 'REDIRECT_REVIEW';
  if(!response.ok)return 'HTTP_REVIEW';
  const mime=(response.headers.get('content-type')||'').toLowerCase();
  if(mime && !mime.includes('text/html') && !mime.includes('text/plain'))return 'TYPE_REVIEW';
  if(Number(response.headers.get('content-length')||0)>524288 || !response.body?.getReader)return 'SIZE_REVIEW';
  const reader=response.body.getReader();let amount=0,body='';const decoder=new TextDecoder();
  try {
    while(true){const next=await reader.read();if(next.done)break;
      amount+=next.value.byteLength;
      if(amount>524288)return 'SIZE_REVIEW';
      body+=decoder.decode(next.value,{stream:true});
    }
  }finally {try{await reader.cancel();}catch{}reader.releaseLock();}
  // A marker match is indicative only, never a commercial or contact qualification.
  return body.toLocaleLowerCase('en').includes(marker.toLocaleLowerCase('en')) ? 'PUBLIC_MARKER_FOUND_NEEDS_HUMAN_REVIEW' : 'MARKER_NOT_FOUND_REVIEW';
}
export async function consumeOne(envelope, env, {now=Date.now(),fetcher=fetch}={}) {
  if(!envConfigured(env))return {status:'BLOCKED',reason:'CONFIGURATION_GATES'};
  const check=await verifyApproval(envelope,env.CAPTAIN_PUBLIC_KEY_SPKI_B64,now);
  if(!check.ok)return {status:'BLOCKED',reason:check.reason};
  const db=env.SCOUT_DB;
  if(!await flagsAreEnabled(db))return {status:'BLOCKED',reason:'CAPTAIN_STOP'};
  const task=await db.prepare("SELECT seed_json,status,approved_by,approved_at,attempts,max_attempts FROM scout_tasks WHERE task_id=?").bind(envelope.task_id).first();
  if(!task || task.status!=='approved' || task.approved_by!==envelope.approved_by ||
     task.approved_at!==envelope.approved_at || task.attempts>=task.max_attempts)
    return {status:'BLOCKED',reason:'D1_APPROVAL_NOT_MATCHED'};
  if(await sha256(task.seed_json)!==envelope.manifest_sha256)return {status:'BLOCKED',reason:'D1_MANIFEST_MISMATCH'};
  const seed=inspectSeed(task.seed_json,envelope.task_id,envelope.manifest_sha256);
  // Atomic nonce guard: the unique (task_id,nonce) and (task_id,signature_hash) constraints prevent replay.
  const signatureHash=await sha256(decode64(envelope.signature));
  const receipt=await db.prepare("INSERT OR IGNORE INTO scout_approval_nonce_receipts(task_id,nonce,approved_at,manifest_sha256,signature_sha256) SELECT ?,?,?,?,? WHERE EXISTS (SELECT 1 FROM scout_tasks WHERE task_id=? AND seed_json=? AND status='approved' AND approved_by=? AND approved_at=?)")
    .bind(envelope.task_id,envelope.nonce,envelope.approved_at,envelope.manifest_sha256,signatureHash,
      envelope.task_id,task.seed_json,envelope.approved_by,envelope.approved_at).run();
  if(receipt.meta?.changes!==1)return {status:'BLOCKED',reason:'NONCE_REPLAY'};
  const read=await db.prepare('SELECT approved_at,manifest_sha256,signature_sha256 FROM scout_approval_nonce_receipts WHERE task_id=? AND nonce=?').bind(envelope.task_id,envelope.nonce).first();
  if(read?.approved_at!==envelope.approved_at || read?.manifest_sha256!==envelope.manifest_sha256 || read?.signature_sha256!==signatureHash)
    return {status:'BLOCKED',reason:'NONCE_NOT_DURABLE'};
  if(!await flagsAreEnabled(db))return {status:'BLOCKED',reason:'CAPTAIN_STOP_AFTER_NONCE'};
  const change=await db.prepare("UPDATE scout_tasks SET status='running', attempts=attempts+1,started_at=CURRENT_TIMESTAMP WHERE task_id=? AND seed_json=? AND status='approved' AND approved_by=? AND approved_at=? AND attempts<max_attempts AND EXISTS (SELECT 1 FROM scout_control WHERE key='captain_stop' AND value='0') AND EXISTS (SELECT 1 FROM scout_control WHERE key='scout_enabled' AND value='1')")
    .bind(envelope.task_id,task.seed_json,envelope.approved_by,envelope.approved_at).run();
  if(change.meta?.changes!==1)return {status:'BLOCKED',reason:'ATOMIC_TASK_LOCK_FAILED'};
  let status='FETCH_FAILED';
  try {
    if(!await flagsAreEnabled(db))throw Error('CAPTAIN_STOP');
    status=await sourceCheck(seed.official_url,seed.verify_marker,fetcher);
    if(!await flagsAreEnabled(db))throw Error('CAPTAIN_STOP');
    const saved=await db.prepare("INSERT OR IGNORE INTO scout_six_city_worker_results(task_id,country_code,source_url,verification_status,manifest_sha256) VALUES(?,?,?,?,?)")
      .bind(envelope.task_id,check.code,seed.official_url,status,envelope.manifest_sha256).run();
    if(saved.meta?.changes!==1)throw Error('RESULT_STORAGE_UNCONFIRMED');
    const result=await db.prepare('SELECT verification_status FROM scout_six_city_worker_results WHERE task_id=?').bind(envelope.task_id).first();
    if(result?.verification_status!==status)throw Error('RESULT_READBACK_UNCONFIRMED');
    await db.prepare("UPDATE scout_tasks SET status='completed_with_review',completed_at=CURRENT_TIMESTAMP WHERE task_id=? AND status='running'").bind(envelope.task_id).run();
    return {status:'COMPLETED_WITH_REVIEW',reason:status};
  } catch {
    await db.prepare("UPDATE scout_tasks SET status='failed',last_error='SOURCE_CHECK_FAILED_OR_CAPTAIN_STOP',completed_at=CURRENT_TIMESTAMP WHERE task_id=? AND status='running'").bind(envelope.task_id).run();
    return {status:'FAILED',reason:'SOURCE_CHECK_FAILED_OR_CAPTAIN_STOP'};
  }
}
export async function emergencyStop(env,fetcher=fetch) {
  // Independently attempt D1 STOP and Cloudflare Queue PAUSE; neither may depend
  // on the other's success. Confirm both via readback, not merely API acknowledgement.
  const stopD1=async()=>{
    if(!env.SCOUT_DB)return false;
    const result=await env.SCOUT_DB.prepare("UPDATE scout_control SET value=CASE key WHEN 'captain_stop' THEN '1' WHEN 'scout_enabled' THEN '0' END, updated_at=CURRENT_TIMESTAMP WHERE key IN ('captain_stop','scout_enabled')").run();
    const read=await env.SCOUT_DB.prepare("SELECT key,value FROM scout_control WHERE key IN ('captain_stop','scout_enabled')").all();
    const flags=Object.fromEntries((read.results||[]).map(x=>[x.key,x.value]));
    return result.meta?.changes===2 && flags.captain_stop==='1' && flags.scout_enabled==='0';
  };
  const pauseQueue=async()=>{
    if(!env.SCOUT_QUEUE_CONTROL_TOKEN || env.SCOUT_QUEUE_AUTOPAUSE_READY!=='YES' ||
        env.CF_ACCOUNT_ID!==EXPECTED_ACCOUNT_ID || env.SCOUT_QUEUE_ID!==EXPECTED_QUEUE_ID)return false;
    const url=`https://api.cloudflare.com/client/v4/accounts/${EXPECTED_ACCOUNT_ID}/queues/${EXPECTED_QUEUE_ID}`;
    const headers={Authorization:`Bearer ${env.SCOUT_QUEUE_CONTROL_TOKEN}`,'Content-Type':'application/json'};
    const patch=await fetcher(url,{method:'PATCH',headers,body:JSON.stringify({settings:{delivery_paused:true}}),signal:AbortSignal.timeout(7000)});
    if(!patch.ok)return false;
    const changed=await patch.json();
    if(changed.success!==true || changed.result?.settings?.delivery_paused!==true)return false;
    const read=await fetcher(url,{method:'GET',headers,signal:AbortSignal.timeout(7000)});
    if(!read.ok)return false;
    const confirmed=await read.json();
    return confirmed.success===true && confirmed.result?.settings?.delivery_paused===true;
  };
  const [d1,queue]=await Promise.all([
    stopD1().catch(()=>false),pauseQueue().catch(()=>false)
  ]);
  return {d1,queue};
}

export default {
  async scheduled(){return {status:'CRON_DISABLED'};},
  async queue(batch,env){
    const msgs=Array.isArray(batch?.messages)?batch.messages:[];
    let result={status:'BLOCKED',reason:'INVALID_QUEUE_OR_BATCH'};
    try {
      if(batch?.queue===QUEUE_NAME && msgs.length===1)result=await consumeOne(msgs[0].body,env);
    }catch {result={status:'FAILED',reason:'CONSUMER_EXCEPTION'};}
    finally {
      const shutdown=await emergencyStop(env);
      const stopped=shutdown.d1&&shutdown.queue;
      for(const msg of msgs){try{msg.ack();}catch{}}
      console.log(JSON.stringify({event:'alpha_six_city_scout',status:result.status,reason:result.reason,stop_confirmed:stopped,ack_count:msgs.length}));
      if(!stopped)console.error('SIX_CITY_SCOUT_EMERGENCY_SHUTDOWN_UNCONFIRMED');
    }
  }
};
