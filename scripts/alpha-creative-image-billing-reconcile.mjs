#!/usr/bin/env node
// ALPHA-CREATIVE-IMAGE-PILOT-20261120 — local reconciliation evidence ONLY.
// NEVER writes a ledger, opens a network connection, loads a key, charges, or
// clears a hold. Only the actual provider's billing authority can reconcile.
import {existsSync, lstatSync, readFileSync} from 'node:fs';
import {join, resolve} from 'node:path';
import {createHash} from 'node:crypto';

const DIRECTIVE='ALPHA-CREATIVE-IMAGE-PILOT-20261120';
const MODEL='gpt-image-2.5-flare';
const dir=resolve(process.env.ALPHA_IMAGE_PILOT_DIR ||
                  join(process.cwd(),'.private','alpha-creative-image-pilot'));
const path=join(dir,'pilot-ledger.json');
const sha256=bytes=>createHash('sha256').update(bytes).digest('hex');
const isMicro=x=>Number.isSafeInteger(x) && x>=0 && x<=20_000_000;
// Only documented/reviewed machine categories are safe to expose from
// a caller-influenced ledger. A syntax-only regex also accepts lowercase
// token-shaped secrets and tenant identifiers. Keep unknown metadata opaque.
const KNOWN_ERROR_CODES=new Set([
  'credit_balance_exhausted','insufficient_quota','rate_limit_exceeded'
]);
const KNOWN_ERROR_TYPES=new Set([
  'insufficient_quota','rate_limit_error','rate_limit_exceeded'
]);
const safeCode=x=>typeof x==='string' && KNOWN_ERROR_CODES.has(x)
  ? x : 'NOT_CAPTURED';
const safeType=x=>typeof x==='string' && KNOWN_ERROR_TYPES.has(x)
  ? x : 'NOT_CAPTURED';
const safeRequest=x=>typeof x==='string' && /^req_[a-zA-Z0-9_-]{1,128}$/.test(x)
  ? x : 'NOT_CAPTURED';
const safeDate=x=>typeof x==='string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(x)
  && !Number.isNaN(Date.parse(x)) ? x : 'NOT_CAPTURED';
const money=x=>(x/1_000_000).toFixed(6);

function analyze(bytes) {
  let ledger;
  try { ledger=JSON.parse(bytes.toString('utf8')); }
  catch {throw new Error('RECONCILIATION_REFUSED_INVALID_LEDGER_JSON');}
  if (!ledger || typeof ledger!=='object' || Array.isArray(ledger) ||
      ledger.schema!=='alpha.creative.pilot.ledger.v1' ||
      ledger.directive!==DIRECTIVE || ledger.model!==MODEL ||
      ledger.noDelivery!==true || ledger.capUsd!==20 ||
      typeof ledger.closed!=='boolean' ||
      !isMicro(ledger.spentMicroUsd) || !isMicro(ledger.heldMicroUsd) ||
      ledger.spentMicroUsd+ledger.heldMicroUsd>20_000_000 ||
      !Array.isArray(ledger.entries) || ledger.entries.length>11)
    throw new Error('RECONCILIATION_REFUSED_UNTRUSTED_LEDGER_FIELDS');
  let charged=0, held=0;
  const receipts=[];
  for (const [index,e] of ledger.entries.entries()) {
    if (!e || typeof e!=='object' || Array.isArray(e) ||
        e.index!==index || e.role!==(index===0?'MASTER':'VARIATION') ||
        e.model!==MODEL || e.delivery!=='NOT_SENT' ||
        e.campaignReady!==false || e.reservedMicroUsd!==2_000_000)
      throw new Error('RECONCILIATION_REFUSED_RECEIPT_INTEGRITY');
    if (e.status==='COMPLETE') {
      if (!Number.isSafeInteger(e.costMicroUsd) ||
          e.costMicroUsd<=0 || e.costMicroUsd>e.reservedMicroUsd ||
          typeof e.providerRequestId!=='string' || !e.providerRequestId.trim())
        throw new Error('RECONCILIATION_REFUSED_COST_RECEIPT');
      charged+=e.costMicroUsd;
    } else if (e.status==='UNKNOWN_AFTER_RESERVATION') {
      held+=e.reservedMicroUsd;
    } else throw new Error('RECONCILIATION_REFUSED_UNKNOWN_STATUS');
    receipts.push({
      index:e.index,
      role:e.role,
      localStatus:e.status,
      reservedUsd:money(e.reservedMicroUsd),
      localReportedCostUsd:e.status==='COMPLETE'?money(e.costMicroUsd):'UNKNOWN',
      providerHttpStatus:Number.isInteger(e.providerHttpStatus) && e.providerHttpStatus>=100
        && e.providerHttpStatus<=599 ? e.providerHttpStatus:null,
      providerErrorCode:safeCode(e.providerErrorCode),
      providerErrorType:safeType(e.providerErrorType),
      providerRequestId:safeRequest(e.providerRequestId),
      startedAtUtc:safeDate(e.startedAt),
    });
  }
  if (charged!==ledger.spentMicroUsd || held!==ledger.heldMicroUsd ||
      (held>0 && !ledger.closed))
    throw new Error('RECONCILIATION_REFUSED_LEDGER_TOTALS');
  return {
    directive:DIRECTIVE,
    assessment:'LOCAL_EVIDENCE_ONLY_NOT_PROVIDER_BILLING',
    status:ledger.closed || held>0 ? 'HELD_MANUAL_BILLING_RECONCILIATION_REQUIRED'
      : 'LOCAL_LEDGER_NOT_HELD_PROVIDER_BILLING_STILL_UNVERIFIED',
    localLedgerSha256:sha256(bytes),
    projectCapUsd:'20.000000',
    localReportedSpendUsd:money(charged),
    reservedUnreconciledUsd:money(held),
    actualProviderChargeUsd:'UNKNOWN_NOT_ACCOUNT_VERIFIED',
    ledgerClosed:ledger.closed,
    receipts,
    manualEvidenceNeeded:[
      'Confirm same organization and project as project API key (do not export key)',
      'Inspect authenticated API usage and billing detail for the UTC attempt timestamp',
      'Match model and any provider request ID, comparing charges and time range',
      'Confirm actual available prepaid balance and auto-recharge OFF',
      'Have billing authority determine disposition of any unknown reservation',
    ],
    localLedgerModified:false, providerCalls:0, paidRequests:0,
    requestPermission:'NOT_GRANTED', unlockAuthorized:false,
    globalStop:'NOT_READ_NOT_CHANGED', externalDelivery:'NOT_SENT',
  };
}

function main() {
  if (process.argv.length!==2)
    throw new Error('RECONCILIATION_REFUSED_NO_ARGUMENTS_ACCEPTED');
  if (!existsSync(path)) return {
    directive:DIRECTIVE,status:'LOCAL_LEDGER_NOT_FOUND',
    actualProviderChargeUsd:'UNKNOWN_NOT_ACCOUNT_VERIFIED',
    manualEvidenceNeeded:['Identify the original private pilot ledger on the owner workstation'],
    localLedgerModified:false,providerCalls:0,paidRequests:0,
    requestPermission:'NOT_GRANTED',unlockAuthorized:false,
    globalStop:'NOT_READ_NOT_CHANGED',externalDelivery:'NOT_SENT'
  };
  const stat=lstatSync(path);
  if (!stat.isFile() || stat.isSymbolicLink() || stat.size>128*1024)
    throw new Error('RECONCILIATION_REFUSED_UNSAFE_LEDGER_FILE');
  return analyze(readFileSync(path));
}

try {process.stdout.write(JSON.stringify(main(),null,2)+'\n');}
catch(e) {
  // Never copy raw filesystem contents, provider errors or credentials into logs.
  const allowed = /^RECONCILIATION_REFUSED_[A-Z_]+$/;
  process.stderr.write((allowed.test(e?.message||'')?e.message:
    'RECONCILIATION_REFUSED_LOCAL_READ_FAILED')+'\n');
  process.exitCode=2;
}
