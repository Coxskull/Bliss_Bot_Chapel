// End-to-end offline 429 regression. No network or API key is used.
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const dir=mkdtempSync(join(tmpdir(),'alpha-429-fixture-'));
const preload=join(dir,'fake-provider.mjs');
const calls=join(dir,'calls.log');
const ledger=join(dir,'pilot-ledger.json');
const fake=`
import {appendFileSync} from 'node:fs';
globalThis.fetch = async (_url,_options) => {
  appendFileSync(process.env.ALPHA_IMAGE_TEST_CALLS,'one-call\\n');
  return {
    ok:false,status:429,
    headers:{get(name){
      if(name==='x-request-id')return 'req_synthetic429';
      if(name==='content-length')return '128';
      return null;
    }},
    async text(){return JSON.stringify({error:{
      code:'credit_balance_exhausted',type:'insufficient_quota',
      message:'sk-proj-SHOULD-NOT-LEAK'}});}
  };
};
`;
try {
  writeFileSync(preload,fake);
  const env={
    ...process.env,CI:'',GITHUB_ACTIONS:'',
    ALPHA_IMAGE_PILOT_DIR:dir,ALPHA_IMAGE_TEST_CALLS:calls,
    ALPHA_IMAGE_PILOT_OWNER_APPROVAL:'ALPHA-CREATIVE-IMAGE-PILOT-20261120',
    ALPHA_IMAGE_PILOT_AUTO_RECHARGE:'OFF',
    ALPHA_IMAGE_PILOT_SCOPE:'ISOLATED_NO_DELIVERY',
    ALPHA_GLOBAL_STOP:'ENGAGED',
    ALPHA_IMAGE_PILOT_BILLING_ADMIN_VERIFIED:'YES',
    ALPHA_IMAGE_PILOT_PROVIDER_LIMIT_USD:'20.00',
    ALPHA_IMAGE_PILOT_PROVIDER_SPENT_USD:'0.00',
    ALPHA_IMAGE_PILOT_PREPAID_AVAILABLE_USD:'10.00',
    OPENAI_API_KEY:'sk-fixture-not-real'
  };
  const runner=resolve('scripts/alpha-creative-image-pilot.mjs');
  const run=spawnSync(process.execPath,['--import',preload,runner,'--execute'],
    {cwd:process.cwd(),env,encoding:'utf8',timeout:10000});
  assert.equal(run.status,2,run.stderr);
  assert.match(run.stderr,/PROVIDER_REJECTED_HTTP_429_CODE_credit_balance_exhausted/);
  assert.doesNotMatch(run.stderr,/SHOULD-NOT-LEAK/);
  assert.equal(readFileSync(calls,'utf8').trim(),'one-call','exactly one attempted request; no retry');
  const state=JSON.parse(readFileSync(ledger,'utf8'));
  assert.equal(state.closed,true);
  assert.equal(state.noDelivery,true);
  assert.equal(state.heldMicroUsd,2_000_000);
  assert.equal(state.spentMicroUsd,0);
  assert.equal(state.entries.length,1);
  assert.equal(state.entries[0].status,'UNKNOWN_AFTER_RESERVATION');
  assert.equal(state.entries[0].providerHttpStatus,429);
  assert.equal(state.entries[0].providerErrorCode,'credit_balance_exhausted');
  assert.equal(state.entries[0].providerErrorType,'insufficient_quota');
  assert.equal(state.entries[0].providerRequestId,'req_synthetic429');
  assert.doesNotMatch(JSON.stringify(state),/SHOULD-NOT-LEAK/);
  const rerun=spawnSync(process.execPath,['--import',preload,runner,'--execute'],
    {cwd:process.cwd(),env,encoding:'utf8',timeout:10000});
  assert.equal(rerun.status,2);
  assert.match(rerun.stderr,/LEDGER_CLOSED_OR_INVALID/);
  assert.equal(readFileSync(calls,'utf8').trim(),'one-call','no second paid attempt after hold');
  console.log('ALPHA 429 fixture: error code captured, key redacted, $2 hold retained, no retry PASS');
} finally {
  rmSync(dir,{recursive:true,force:true});
}
