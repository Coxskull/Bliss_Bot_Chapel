import assert from 'node:assert/strict';
import {dispatchSyntheticInternalAlert,prepareStagingEmail} from './incident-escalation-dispatcher.mjs';
import {prepareInternalEscalation} from './incident-escalation-contract.mjs';

const alert={
  alert_id:'11111111-1111-4111-8111-111111111111',
  case_id:'22222222-2222-4222-8222-222222222222',
  category:'security_incident',severity:'CRITICAL',
  assigned_role:'SECURITY_REVIEW',delivery_state:'UNROUTED',
  created_at:'2026-11-08T03:00:00Z',
  full_name:'DO NOT LEAK',email:'customer@private.invalid',
  description:'highly sensitive description'
};
const cfg={
  environment:'staging',enableSyntheticInternalAlerts:true,
  channel:'internal-email',monitoredByHuman:true,recipientApproved:true,
  recipient:'internal-alerts@example.org',approvedRecipient:'internal-alerts@example.org'
};
const leaseId='33333333-3333-4333-8333-333333333333';
function adapters(overrides={}){
  const calls={claim:0,accept:0,fail:0,send:0};
  const ledger={
    async claim(){calls.claim++;return {acquired:true,leaseId};},
    async accept(){calls.accept++;return true;},
    async fail(){calls.fail++;return true;}
  };
  const transport={async send(){calls.send++;return {
    accepted:true,messageId:'provider-safe-00001',acceptedAt:'2026-11-08T03:01:00Z'
  };}};
  Object.assign(ledger,overrides.ledger||{});
  Object.assign(transport,overrides.transport||{});
  return {calls,ledger,transport};
}
const envelope=prepareInternalEscalation(alert);
const mail=prepareStagingEmail(envelope,cfg.recipient);
assert.equal(mail.to,cfg.recipient);
assert.ok(mail.text.includes(alert.alert_id));
for(const privateText of [alert.full_name,alert.email,alert.description])
  assert.ok(!JSON.stringify(mail).includes(privateText));

// The exported email builder must defend itself, even when an alternate
// adapter accidentally bypasses the DB-backed envelope validator.
for(const forged of [
  {assigned_role:'SECURITY_REVIEW\\nCustomer: private@example.invalid'},
  {assigned_role:'\u003cb\u003eADMIN\u003c/b\u003e'},
  {assigned_role:'PRIVACY_REVIEW'}, // wrong severity for this CRITICAL alert
  {assigned_role:undefined},
  {severity:'NORMAL'},
  {severity:'HIGH',assigned_role:'SECURITY_REVIEW'}
]){
  assert.throws(()=>prepareStagingEmail({...envelope,...forged},cfg.recipient),
    /TASK21_EMAIL_INVALID/);
}
assert.throws(()=>prepareStagingEmail(envelope,'a'.repeat(250)+'@example.org'),
  /TASK21_EMAIL_INVALID/);
const privacyEnvelope=prepareInternalEscalation({
  ...alert,category:'privacy',severity:'HIGH',assigned_role:'PRIVACY_REVIEW'
});
assert.match(prepareStagingEmail(privacyEnvelope,cfg.recipient).text,
  /Queue: PRIVACY_REVIEW/);

for(const changed of [
 {enableSyntheticInternalAlerts:false},{recipientApproved:false},
 {monitoredByHuman:false},{environment:'production'},
 {channel:'customer-email'},{recipient:'other@example.org'},
 {approvedRecipient:'other@example.org'},{recipient:'not an address'}
]){
  const a=adapters();
  const res=await dispatchSyntheticInternalAlert({alert,config:{...cfg,...changed},...a});
  assert.equal(res.status,'DISABLED_OR_UNAPPROVED');
  assert.equal(a.calls.claim,0);assert.equal(a.calls.send,0);
}
const happy=adapters();
const ok=await dispatchSyntheticInternalAlert({alert,config:cfg,...happy});
assert.equal(ok.status,'PROVIDER_ACCEPTED_NOT_HUMAN_ACK');
assert.equal(ok.humanAcknowledged,false);
assert.equal(ok.providerAccepted,true);
assert.deepEqual(happy.calls,{claim:1,accept:1,fail:0,send:1});

const duplicate=adapters({ledger:{async claim(){return {acquired:false};}}});
assert.equal((await dispatchSyntheticInternalAlert({alert,config:cfg,...duplicate})).status,'DUPLICATE_OR_LEASED');
assert.equal(duplicate.calls.send,0);

const badLease=adapters({ledger:{async claim(){return {acquired:true,leaseId:'fake'};}}});
assert.equal((await dispatchSyntheticInternalAlert({alert,config:cfg,...badLease})).status,'LEDGER_CLAIM_UNVERIFIED');
assert.equal(badLease.calls.send,0);

assert.equal((await dispatchSyntheticInternalAlert({alert,config:cfg,ledger:adapters().ledger})).status,'ADAPTER_NOT_CONFIGURED');

const providerDown=adapters({transport:{async send(){throw Error('SECRET-MUST-NOT-LEAK');}}});
const down=await dispatchSyntheticInternalAlert({alert,config:cfg,...providerDown});
assert.equal(down.status,'PROVIDER_REQUEST_FAILED');
assert.equal(down.humanAcknowledged,false);
assert.equal(providerDown.calls.fail,1);
assert.ok(!JSON.stringify(down).includes('SECRET-MUST-NOT-LEAK'));

const badReceipt=adapters({transport:{async send(){return {accepted:true,messageId:'x',acceptedAt:'no'};}}});
assert.equal((await dispatchSyntheticInternalAlert({alert,config:cfg,...badReceipt})).status,'PROVIDER_ACCEPTANCE_UNVERIFIED');
assert.equal(badReceipt.calls.fail,1);

const uncertain=adapters({ledger:{async accept(){return false;}}});
const maybeSent=await dispatchSyntheticInternalAlert({alert,config:cfg,...uncertain});
assert.equal(maybeSent.status,'PROVIDER_ACCEPTED_RECEIPT_UNCERTAIN_DO_NOT_RETRY');
assert.equal(maybeSent.humanAcknowledged,false);

const forged=adapters();
await assert.rejects(()=>dispatchSyntheticInternalAlert({
  alert:{...alert,delivery_state:'DELIVERED'},config:cfg,...forged
}),/TASK21_ALERT_INVALID/);
assert.equal(forged.calls.send,0);

const high=adapters();
const highAlert={...alert,category:'refund',severity:'HIGH',assigned_role:'BILLING_REVIEW'};
const highResult=await dispatchSyntheticInternalAlert({alert:highAlert,config:cfg,...high});
assert.equal(highResult.status,'PROVIDER_ACCEPTED_NOT_HUMAN_ACK');
assert.equal(high.calls.send,1);

const normal=adapters();
await assert.rejects(()=>dispatchSyntheticInternalAlert({
  alert:{...alert,category:'general',severity:'NORMAL',assigned_role:'GENERAL_SUPPORT'},
  config:cfg,...normal
}),/TASK21_ALERT_INVALID/);
assert.equal(normal.calls.send,0);

console.log('TASK21_STAGING_DISPATCHER_INJECTED_NO_EGRESS_TESTS_PASS');
