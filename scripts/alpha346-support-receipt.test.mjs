import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const sop = await readFile(new URL('../docs/commercial/ALPHA-346-SUPPORT-MAILBOX-RECEIPT.md',import.meta.url),'utf8');

test('internal Gmail self-test cannot be promoted to external support acceptance',()=>{
  assert.match(sop,/SELF_TEST_PASSED_PAID_CUSTOMER_SUPPORT_NOT_VERIFIED/);
  assert.match(sop,/NOT TESTED:/);
  assert.match(sop,/separately authorized non-self address/);
  assert.match(sop,/NOT ASSIGNED \/ NOT VERIFIED/);
});
test('support mailbox self-test proves only selected mailbox and internal label',()=>{
  assert.match(sop,/dejesuserwin1@gmail.com/);
  assert.match(sop,/ALPHA \/ Commercial Inquiries/);
  assert.match(sop,/SENT/);assert.match(sop,/INBOX/);
  assert.match(sop,/No customer recipient/); 
});
test('internal support triage text never authorizes outreach, payment or public policies',()=>{
  assert.match(sop,/Global STOP:\*\* ENGAGED/);
  assert.match(sop,/No paid-order support SLA/);
  assert.match(sop,/Commercial payment release: NOT AUTHORIZED/);
  assert.match(sop,/Replies are human-reviewed/);
});

test('external two-way non-customer test is distinguished from paid support authorization',()=>{
  assert.match(sop,/EXTERNAL_TEST_SEND_AND_REPLY_PASS/);
  assert.match(sop,/1a1b4792a48c5a9b/);
  assert.match(sop,/2026-11-07 13:26:29 PHT/);
  assert.match(sop,/paid-customer support monitoring \*\*NOT VERIFIED\*\*/);
});
