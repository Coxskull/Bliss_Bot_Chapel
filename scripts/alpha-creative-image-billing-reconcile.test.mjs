// Zero-network, zero-write invoice-evidence extractor acceptance.
import assert from 'node:assert/strict';
import test from 'node:test';
import {spawnSync} from 'node:child_process';
import {mkdtempSync, writeFileSync, readFileSync, rmSync, readdirSync, symlinkSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join, resolve} from 'node:path';
const runner=resolve('scripts/alpha-creative-image-billing-reconcile.mjs');
const dir=mkdtempSync(join(tmpdir(),'alpha-billing-readonly-'));
const ledgerPath=join(dir,'pilot-ledger.json');
const fixture=()=>({
  schema:'alpha.creative.pilot.ledger.v1',
  directive:'ALPHA-CREATIVE-IMAGE-PILOT-20261120',
  model:'gpt-image-2.5-flare',capUsd:20,noDelivery:true,
  spentMicroUsd:0,heldMicroUsd:2_000_000,closed:true,
  entries:[{
    index:0,role:'MASTER',language:'English',
    model:'gpt-image-2.5-flare',status:'UNKNOWN_AFTER_RESERVATION',
    reservedMicroUsd:2_000_000,
    startedAt:'2026-11-21T12:00:00.000Z',
    providerHttpStatus:429,
    providerErrorCode:'insufficient_quota',
    providerErrorType:'rate_limit_error',
    providerRequestId:'req_fixture123',
    prompt:'sk-proj-THIS-IS-A-FICTIONAL-SECRET',
    campaignReady:false,delivery:'NOT_SENT'
  }]
});
const execute=(args=[])=>spawnSync(process.execPath,[runner,...args],{
  encoding:'utf8',timeout:10000,cwd:process.cwd(),
  env:{...process.env,ALPHA_IMAGE_PILOT_DIR:dir,
    OPENAI_API_KEY:'sk-proj-THIS-IS-A-FICTIONAL-SECRET'}
});
const put=x=>writeFileSync(ledgerPath,JSON.stringify(x),'utf8');
try {
  test('missing ledger yields no-charge-authority and creates no ledger',()=>{
    const r=execute();
    assert.equal(r.status,0,r.stderr);
    const out=JSON.parse(r.stdout);
    assert.equal(out.status,'LOCAL_LEDGER_NOT_FOUND');
    assert.equal(out.unlockAuthorized,false);
    assert.deepEqual(readdirSync(dir),[]);
  });
  test('original HTTP 429 remains a hold, not a $2 provider charge',()=>{
    const before=fixture();put(before);
    const raw=readFileSync(ledgerPath);
    const r=execute();
    assert.equal(r.status,0,r.stderr);
    const x=JSON.parse(r.stdout);
    assert.equal(x.status,'HELD_MANUAL_BILLING_RECONCILIATION_REQUIRED');
    assert.equal(x.reservedUnreconciledUsd,'2.000000');
    assert.equal(x.localReportedSpendUsd,'0.000000');
    assert.equal(x.actualProviderChargeUsd,'UNKNOWN_NOT_ACCOUNT_VERIFIED');
    assert.equal(x.receipts[0].providerHttpStatus,429);
    assert.equal(x.receipts[0].providerRequestId,'req_fixture123');
    assert.equal(x.receipts[0].startedAtUtc,'2026-11-21T12:00:00.000Z');
    assert.equal(x.requestPermission,'NOT_GRANTED');
    assert.equal(x.globalStop,'NOT_READ_NOT_CHANGED');
    assert.equal(x.externalDelivery,'NOT_SENT');
    assert.equal(x.providerCalls,0);
    assert.equal(x.unlockAuthorized,false);
    assert.doesNotMatch(r.stdout,/FICTIONAL-SECRET|sk-proj-/);
    assert.deepEqual(readFileSync(ledgerPath),raw);
    assert.deepEqual(readdirSync(dir),['pilot-ledger.json']);
  });
  test('untrusted provider fields are redacted not repeated',()=>{
    const x=fixture();
    x.entries[0].providerErrorCode='sk-proj-SENSITIVE';
    x.entries[0].providerRequestId='Bearer sk-proj-SENSITIVE';
    x.entries[0].startedAt='private-key-ref';
    put(x);
    const r=execute();
    assert.equal(r.status,0,r.stderr);
    const out=JSON.parse(r.stdout);
    assert.equal(out.receipts[0].providerErrorCode,'NOT_CAPTURED');
    assert.equal(out.receipts[0].providerRequestId,'NOT_CAPTURED');
    assert.equal(out.receipts[0].startedAtUtc,'NOT_CAPTURED');
    assert.doesNotMatch(r.stdout,/SENSITIVE|Bearer/);
  });
  test('lowercase secret-like and arbitrary unknown provider codes cannot leak via evidence packet',()=>{
    const fixtureValues=[
      ['sk-proj-lowercase-secret','sk-proj-lowercase-type'],
      ['tenant_12345','unrecognized_provider_type'],
      ['custom.error-code','custom.error-type']
    ];
    for(const [code,type] of fixtureValues){
      const x=fixture();
      x.entries[0].providerErrorCode=code;
      x.entries[0].providerErrorType=type;
      put(x);
      const raw=readFileSync(ledgerPath);
      const r=execute();
      assert.equal(r.status,0,r.stderr);
      const out=JSON.parse(r.stdout);
      assert.equal(out.receipts[0].providerErrorCode,'NOT_CAPTURED');
      assert.equal(out.receipts[0].providerErrorType,'NOT_CAPTURED');
      assert.doesNotMatch(r.stdout,/sk-proj-|tenant_12345|custom.error/);
      assert.equal(out.actualProviderChargeUsd,'UNKNOWN_NOT_ACCOUNT_VERIFIED');
      assert.equal(out.reservedUnreconciledUsd,'2.000000');
      assert.equal(out.unlockAuthorized,false);
      assert.deepEqual(readFileSync(ledgerPath),raw);
    }
  });
  test('recognized machine reasons remain usable for provider billing reconciliation',()=>{
    for(const [code,type] of [
      ['credit_balance_exhausted','insufficient_quota'],
      ['insufficient_quota','rate_limit_error'],
      ['rate_limit_exceeded','rate_limit_exceeded']
    ]){
      const x=fixture();
      x.entries[0].providerErrorCode=code;
      x.entries[0].providerErrorType=type;
      put(x);
      const r=execute();
      assert.equal(r.status,0,r.stderr);
      const out=JSON.parse(r.stdout);
      assert.equal(out.receipts[0].providerErrorCode,code);
      assert.equal(out.receipts[0].providerErrorType,type);
      assert.equal(out.receipts[0].providerRequestId,'req_fixture123');
      assert.equal(out.status,'HELD_MANUAL_BILLING_RECONCILIATION_REQUIRED');
      assert.equal(out.providerCalls,0);
    }
  });
  test('tampered totals, unapproved delivery and impossible sequence fail closed',()=>{
    const cases=[
      x=>{x.spentMicroUsd=100;},
      x=>{x.closed=false;},
      x=>{x.entries[0].delivery='SENT';},
      x=>{x.entries[0].index=3;},
      x=>{x.entries[0].reservedMicroUsd=1;},
      x=>{x.entries[0].status='COMPLETE';},
    ];
    for(const mutate of cases){
      const x=fixture();mutate(x);put(x);
      const r=execute();
      assert.equal(r.status,2,r.stderr);
      assert.match(r.stderr,/RECONCILIATION_REFUSED_/);
      assert.equal(r.stdout,'');
    }
  });
  test('complete local usage is not billed-account proof or spend authorization',()=>{
    const x=fixture();
    x.entries[0].status='COMPLETE';
    x.entries[0].costMicroUsd=3500;
    x.heldMicroUsd=0;x.spentMicroUsd=3500;x.closed=false;
    put(x);
    const r=execute();
    assert.equal(r.status,0,r.stderr);
    const out=JSON.parse(r.stdout);
    assert.equal(out.status,'LOCAL_LEDGER_NOT_HELD_PROVIDER_BILLING_STILL_UNVERIFIED');
    assert.equal(out.localReportedSpendUsd,'0.003500');
    assert.equal(out.actualProviderChargeUsd,'UNKNOWN_NOT_ACCOUNT_VERIFIED');
    assert.equal(out.requestPermission,'NOT_GRANTED');
  });
  test('args cannot activate payment or clear a ledger',()=>{
    const before=readFileSync(ledgerPath);
    const r=execute(['--execute']);
    assert.equal(r.status,2);
    assert.match(r.stderr,/RECONCILIATION_REFUSED_NO_ARGUMENTS_ACCEPTED/);
    assert.deepEqual(before,readFileSync(ledgerPath));
  });
  test('symlinked ledger is denied',()=>{
    rmSync(ledgerPath);
    const other=join(dir,'real-ledger.json');
    writeFileSync(other,JSON.stringify(fixture()));
    try {
      symlinkSync(other,ledgerPath);
      const r=execute();
      assert.equal(r.status,2);
      assert.match(r.stderr,/RECONCILIATION_REFUSED_UNSAFE_LEDGER_FILE/);
    } finally {
      rmSync(ledgerPath,{force:true});
      rmSync(other,{force:true});
    }
  });
} finally {
  process.on('exit',()=>rmSync(dir,{recursive:true,force:true}));
}
