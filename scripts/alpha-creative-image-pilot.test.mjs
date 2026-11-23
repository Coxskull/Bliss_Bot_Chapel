import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
const script = readFileSync('scripts/alpha-creative-image-pilot.mjs','utf8');
const brief = JSON.parse(readFileSync('scripts/alpha-creative-image-pilot-brief.json','utf8'));
assert.equal(brief.directive,'ALPHA-CREATIVE-IMAGE-PILOT-20261120');
assert.equal(brief.model,'gpt-image-2.5-flare');
assert.equal(brief.artworks.length,11);
assert.equal(brief.artworks[0].role,'MASTER');
assert(brief.artworks.slice(1).every(x=>x.role==='VARIATION'));
assert(brief.artworks.every((x,i)=>x.id===i && x.prompt.includes('NOT FOR PUBLICATION')));
assert.match(script,/const CAP = 20/);
assert.match(script,/const RESERVATION = 2.00/);
assert.match(script,/const QUALITY = 'low'/);
assert.match(script,/const MAX_IMAGES = 11/);
assert.match(script,/openSync\(lockPath,'wx'/);
assert.match(script,/atomicWrite\(statePath, JSON.stringify\(ledger/);
assert.match(script,/UNKNOWN_AFTER_RESERVATION/);
assert.match(script,/NO_RETRY/);
assert.match(script,/ALPHA_GLOBAL_STOP !== 'ENGAGED'/);
assert.match(script,/ALPHA_IMAGE_PILOT_AUTO_RECHARGE !== 'OFF'/);
assert.match(script,/ALPHA_IMAGE_PILOT_PROVIDER_LIMIT_USD, 'PROJECT_LIMIT'/);
assert.match(script,/ALPHA_IMAGE_PILOT_PROVIDER_SPENT_USD, 'PROJECT_SPEND'/);
assert.match(script,/ALPHA_IMAGE_PILOT_PREPAID_AVAILABLE_USD, 'PREPAID'/);
assert.match(script,/ALPHA_IMAGE_PILOT_BILLING_ADMIN_VERIFIED !== 'YES'/);
assert.match(script,/count !== 1/);
assert.match(script,/NEVER_RUN_PAID_CALL_IN_CI/);
assert.match(script,/quality:QUALITY,size:SIZE/);
assert.match(script,/n:1,output_format:'png'/);
assert.match(script,/delivery:'NOT_SENT', campaignReady:false/);
assert(!/retry\s*\(/.test(script),'no charge-risking automatic retry allowed');
const dry=spawnSync(process.execPath,['scripts/alpha-creative-image-pilot.mjs','--dry-run'],{encoding:'utf8',timeout:10000});
assert.equal(dry.status,0,dry.stderr);
const x=JSON.parse(dry.stdout);
assert.equal(x.mode,'DRY_RUN_NO_API');
assert.equal(x.planned.length,11);
assert.equal(x.stop,'ENGAGED_NOT_MODIFIED');
assert.equal(x.delivery,'NOT_SENT');
const refused=spawnSync(process.execPath,['scripts/alpha-creative-image-pilot.mjs','--execute'],{
  encoding:'utf8',timeout:10000,env:{...process.env,OPENAI_API_KEY:'',ALPHA_IMAGE_PILOT_OWNER_APPROVAL:''}
});
assert.equal(refused.status,2);
assert.match(refused.stderr,/OWNER_BILLING_OR_ISOLATION_ATTESTATION_MISSING/);
const fakeEnv={
  ...process.env,
  CI:'',GITHUB_ACTIONS:'',
  OPENAI_API_KEY:'sk-fixture-not-real',
  ALPHA_IMAGE_PILOT_OWNER_APPROVAL:'ALPHA-CREATIVE-IMAGE-PILOT-20261120',
  ALPHA_IMAGE_PILOT_AUTO_RECHARGE:'OFF',
  ALPHA_IMAGE_PILOT_SCOPE:'ISOLATED_NO_DELIVERY',
  ALPHA_GLOBAL_STOP:'ENGAGED',
  ALPHA_IMAGE_PILOT_BILLING_ADMIN_VERIFIED:'YES',
  ALPHA_IMAGE_PILOT_PROVIDER_LIMIT_USD:'9.00',
  ALPHA_IMAGE_PILOT_PROVIDER_SPENT_USD:'0.00',
  ALPHA_IMAGE_PILOT_PREPAID_AVAILABLE_USD:''
};
const unverified=spawnSync(process.execPath,['scripts/alpha-creative-image-pilot.mjs','--execute'],
  {encoding:'utf8',timeout:10000,env:fakeEnv});
assert.equal(unverified.status,2);
assert.match(unverified.stderr,/INVALID_BILLING_ATTESTATION_PREPAID/);
const exhausted=spawnSync(process.execPath,['scripts/alpha-creative-image-pilot.mjs','--execute'],{
  encoding:'utf8',timeout:10000,env:{...fakeEnv,
    ALPHA_IMAGE_PILOT_PROVIDER_SPENT_USD:'9.00',ALPHA_IMAGE_PILOT_PREPAID_AVAILABLE_USD:'20.00'}
});
assert.equal(exhausted.status,2);
assert.match(exhausted.stderr,/PROVIDER_PROJECT_OR_PREPAID_HEADROOM_INSUFFICIENT/);
const multi=spawnSync(process.execPath,['scripts/alpha-creative-image-pilot.mjs','--execute','--max-new=10'],{
  encoding:'utf8',timeout:10000,env:{...fakeEnv,
    ALPHA_IMAGE_PILOT_PROVIDER_SPENT_USD:'0.00',ALPHA_IMAGE_PILOT_PREPAID_AVAILABLE_USD:'9.00'}
});
assert.equal(multi.status,2);
assert.match(multi.stderr,/ONE_BILLABLE_IMAGE_PER_INVOCATION_REVERIFY_BILLING/);

console.log('ALPHA creative image pilot: 11 design specs and offline budget/STOP refusal PASS; $0 spent');
