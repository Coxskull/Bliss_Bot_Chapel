import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
const sha=b=>createHash('sha256').update(b).digest('hex');
const path=mkdtempSync(join(tmpdir(),'alpha-pilot-offline-'));
const original=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=','base64');
const run=()=>spawnSync(process.execPath,['scripts/alpha-creative-image-pilot-qa.mjs'],
  {cwd:process.cwd(),env:{...process.env,ALPHA_IMAGE_PILOT_DIR:path,OPENAI_API_KEY:''},
    encoding:'utf8',timeout:10000});
const ledger={
  schema:'alpha.creative.pilot.ledger.v1',
  directive:'ALPHA-CREATIVE-IMAGE-PILOT-20261120',model:'gpt-image-2.5-flare',noDelivery:true,
  capUsd:20,closed:false,spentMicroUsd:3050,heldMicroUsd:0,
  entries:[{index:0,role:'MASTER',language:'English',model:'gpt-image-2.5-flare',
    delivery:'NOT_SENT',campaignReady:false,quality:'low',size:'1024x1024',
    status:'COMPLETE',reservedMicroUsd:2_000_000,costMicroUsd:3050,
    providerRequestId:'fixture-not-real-provider-receipt',
    imageFile:'alpha-image-00.png',imageSha256:sha(original),
    inputTokens:10,outputTokens:100,latencyMs:50}]
};
const save=()=>writeFileSync(join(path,'pilot-ledger.json'),JSON.stringify(ledger));
try {
  const before=run();
  assert.equal(before.status,0,before.stderr);
  const initial=JSON.parse(before.stdout);
  assert.equal(initial.artifacts.length,0);
  assert.equal(initial.remainingAuthorizedUsd,'20.000000');
  assert.equal(initial.commercialActivation,'NOT_READY');
  writeFileSync(join(path,'alpha-image-00.png'),original);
  save();
  let result=run();
  assert.equal(result.status,0,result.stderr);
  let report=JSON.parse(result.stdout);
  assert.equal(report.artifacts.length,1);
  assert.equal(report.artifacts[0].imageShaVerified,true);
  assert.equal(report.artifacts[0].humanQualityReview.approval,'PENDING_HUMAN_EVIDENCE');
  assert.equal(report.remainingAuthorizedUsd,'19.996950');
  assert.equal(report.commercialActivation,'NOT_READY');
  assert.equal(report.delivery,'NOT_SENT');

  ledger.spentMicroUsd=0;save();result=run();
  assert.notEqual(result.status,0);
  assert.match(result.stderr,/PILOT_QA_REFUSED_COST_OR_PROVIDER_RECEIPT_MISMATCH/);
  ledger.spentMicroUsd=3050;

  ledger.entries[0].imageFile='../../secret.txt';save();result=run();
  assert.equal(result.status,0,result.stderr);
  report=JSON.parse(result.stdout);
  assert.equal(report.artifacts[0].imageShaVerified,false);
  assert.equal(report.artifacts[0].outputFile,'NOT_VERIFIED');
  assert.doesNotMatch(result.stdout,/\.\.\/\.\.\/secret.txt/);
  ledger.entries[0].imageFile='alpha-image-00.png';

  ledger.entries[0].delivery='SENT';save();result=run();
  assert.notEqual(result.status,0);
  assert.match(result.stderr,/PILOT_QA_REFUSED_INCONSISTENT_LEDGER/);
  ledger.entries[0].delivery='NOT_SENT';

  ledger.entries[0].status='UNKNOWN_AFTER_RESERVATION';
  delete ledger.entries[0].costMicroUsd;
  delete ledger.entries[0].providerRequestId;
  ledger.spentMicroUsd=0;
  ledger.heldMicroUsd=2_000_000;
  ledger.closed=true;
  save();result=run();
  assert.equal(result.status,0,result.stderr);
  report=JSON.parse(result.stdout);
  assert.equal(report.unknownOrUnreconciledCharges,true);
  assert.equal(report.artifacts[0].imageShaVerified,false);
  assert.equal(report.remainingAuthorizedUsd,'18.000000');
  assert.equal(report.commercialActivation,'NOT_READY');

  const ignored=spawnSync('git',['check-ignore','-q','.private/alpha-creative-image-pilot/pilot-ledger.json'],
    {cwd:process.cwd(),encoding:'utf8'});
  assert.equal(ignored.status,0,'Private billing ledger MUST be gitignored');
  console.log('ALPHA offline pilot QA: no-spend, receipt integrity, private path, uncertainty and $20 ledger PASS; $0 provider calls');
} finally {rmSync(path,{recursive:true,force:true});}
