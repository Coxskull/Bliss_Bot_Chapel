import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {reconcilePlanningSandboxEvidence as verify} from './alpha346-stripe-sandbox-reconcile.mjs';
const template=JSON.parse(await readFile(new URL('./fixtures/alpha346-stripe-sandbox-readback-20261109.json',import.meta.url),'utf8'));
const fresh=()=>structuredClone(template);

test('real noncustomer Stripe API sandbox snapshot reconciles to test-only status',()=>{
  const r=verify(fresh());
  assert.deepEqual(r,{
    status:'SANDBOX_READBACK_VERIFIED',paidTestAmountMinor:14900,currency:'USD',
    successfulSandboxCheckout:true,declinedSandboxPayment:true,checkoutTaxCalculated:true,
    webhookDeliveryVerified:false,productionPaymentAuthorized:false,
    realRevenueRecognized:false,customerOutreachPerformed:false,
    fulfillmentAuthorized:false,requiresManualReview:true
  });
});
test('rejects live mode and test-to-production confusion at every payment boundary',()=>{
  for(const path of ['link','session','paymentIntent','failedPaymentIntent']){
    const x=fresh();x[path].livemode=true;
    assert.throws(()=>verify(x),/SANDBOX_RECONCILIATION_REJECTED/,path);
  }
  const x=fresh();x.environment='STRIPE_LIVE';assert.throws(()=>verify(x),/invalid_environment/);
});
test('denies mismatched price, service, link, amount, and captured money',()=>{
  const cases=[
    x=>{x.product.id='prod_other';}, x=>{x.price.product='prod_other';},
    x=>{x.price.unit_amount=100;}, x=>{x.price.currency='php';},
    x=>{x.price.type='recurring';}, x=>{x.session.payment_link='plink_other';},
    x=>{x.session.amount_total=15000;}, x=>{x.session.payment_intent='pi_other';},
    x=>{x.paymentIntent.amount_received=0;}, x=>{x.lineItems[0].price='price_other';},
    x=>{x.lineItems.push(structuredClone(x.lineItems[0]));},
    x=>{x.link.active=false;}
  ];
  for(const [index,mutate] of cases.entries()){
    const x=fresh();mutate(x);assert.throws(()=>verify(x),/SANDBOX_RECONCILIATION_REJECTED/,'case '+index);
  }
});
test('unpaid, async, declined, or incomplete checkout never passes as paid',()=>{
  const cases=[
    x=>{x.session.status='open';},x=>{x.session.payment_status='unpaid';},
    x=>{x.session.payment_status='no_payment_required';},
    x=>{x.paymentIntent.status='processing';},
    x=>{x.paymentIntent.status='requires_payment_method';},
    x=>{x.session.automatic_tax.status='requires_location_inputs';},
    x=>{x.session.total_details.amount_tax=-1;}
  ];
  for(const [index,mutate] of cases.entries()){
    const x=fresh();mutate(x);assert.throws(()=>verify(x),/SANDBOX_RECONCILIATION_REJECTED/,'case '+index);
  }
});
test('missing verified success/failure events, altered IDs, or no decline evidence reject',()=>{
  const cases=[
    x=>{x.events=[];},
    x=>{x.events=x.events.filter(e=>e.type!=='payment_intent.succeeded');},
    x=>{x.events=x.events.filter(e=>e.type!=='checkout.session.completed');},
    x=>{x.events=x.events.filter(e=>e.type!=='payment_intent.payment_failed');},
    x=>{x.events[0].livemode=true;},
    x=>{x.failedPaymentIntent.last_payment_error.decline_code='';},
    x=>{x.failedPaymentIntent.amount_received=14900;}
  ];
  for(const [index,mutate] of cases.entries()){
    const x=fresh();mutate(x);assert.throws(()=>verify(x),/SANDBOX_RECONCILIATION_REJECTED/,'case '+index);
  }
});
test('sandbox-only policy is explicit; no real charge, outreach, or fulfillment authority',()=>{
  for(const path of ['environment','payment','policy_status','real_customer_use']){
    const x=fresh();x.link.metadata[path]='LIVE';
    assert.throws(()=>verify(x),/SANDBOX_RECONCILIATION_REJECTED/,path);
  }
  const x=fresh();x.session.metadata.real_customer_use='ALLOWED';
  assert.throws(()=>verify(x),/checkout_policy_boundary/);
});

test('inclusive tax cannot exceed the total paid by the sandbox buyer',()=>{
  for(const amount of [14901, Number.MAX_SAFE_INTEGER, -1, 0.5, '0', null]){
    const x=fresh();x.session.total_details.amount_tax=amount;
    assert.throws(()=>verify(x),/tax_readback_incomplete/,'tax '+amount);
  }
  for(const amount of [0, 987, 14900]){
    const x=fresh();x.session.total_details.amount_tax=amount;
    assert.equal(verify(x).fulfillmentAuthorized,false);
  }
});

test('one provider event cannot stand in for three distinct payment events',()=>{
  const x=fresh();x.events.forEach(e=>{e.id=x.events[0].id;});
  assert.throws(()=>verify(x),/event_identity_conflict/);
});
