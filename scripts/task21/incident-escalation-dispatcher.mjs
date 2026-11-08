import { prepareInternalEscalation } from './incident-escalation-contract.mjs';

/*
 * ALPHA Task21 — source-only, injection-driven staging dispatcher.
 * NO network client, secrets, direct Gmail/Supabase calls or schedule.
 * The caller MUST supply a durable transactional lease ledger, authenticated
 * provider transport, approved monitored internal destination and separate
 * human-ACK workflow. A provider acceptance is NOT delivery or human ACK.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const RECEIPT = /^[A-Za-z0-9._:-]{8,128}$/;
const MAILBOX = /^[^@\s]+@[a-z0-9.-]+\.[a-z]{2,}$/i;
const freeze = x => Object.freeze(x);
const failClosed = status => freeze({status,providerAccepted:false,humanAcknowledged:false});

function approved(config) {
  return config && config.environment==='staging' &&
    config.enableSyntheticInternalAlerts===true &&
    config.channel==='internal-email' &&
    config.monitoredByHuman===true &&
    config.recipientApproved===true &&
    typeof config.recipient==='string' &&
    MAILBOX.test(config.recipient) && config.recipient.length<=254 &&
    config.recipient===config.approvedRecipient;
}

/** Fixed metadata only: no requester, email, description, token or case content. */
export function prepareStagingEmail(alertEnvelope,recipient) {
  if (!alertEnvelope || alertEnvelope.schema!=='agm.task21.internal-alert.v1' ||
      alertEnvelope.environment!=='staging' || alertEnvelope.synthetic!==true ||
      !UUID.test(alertEnvelope.alert_id) || !UUID.test(alertEnvelope.case_id) ||
      !['HIGH','CRITICAL'].includes(alertEnvelope.severity) ||
      !MAILBOX.test(recipient)) throw Error('TASK21_EMAIL_INVALID');
  return freeze({
    to:recipient,
    subject:'ALPHA Task21 STAGING '+alertEnvelope.severity+' internal incident — SYNTHETIC',
    text:[
      'SYNTHETIC INTERNAL TEST — NOT A REAL INCIDENT.',
      'Alert reference: '+alertEnvelope.alert_id,
      'Case reference: '+alertEnvelope.case_id,
      'Severity: '+alertEnvelope.severity,
      'Queue: '+alertEnvelope.assigned_role,
      'Review using approved private support dashboard.',
      'Never send customer data, take payment, refund, or respond to this fictional case.',
      'Provider acceptance does not prove mailbox delivery or human acknowledgement.'
    ].join('\n'),
    idempotencyKey:'agm-task21-staging-alert:'+alertEnvelope.alert_id
  });
}

/**
 * Required injected adapter contract:
 * ledger.claim({alertId,key}) atomically gives {acquired:true,leaseId} once;
 * ledger.accept(receipt) durably records genuine provider acceptance;
 * ledger.fail(...) writes failed attempt with no automatic retry.
 * transport.send(message) returns {accepted,messageId,acceptedAt}.
 * No implementations here. Do not activate without approved provider, a
 * durable ledger and independently verified human ACK path.
 */
export async function dispatchSyntheticInternalAlert({alert,config,ledger,transport}) {
  // Critical: do not reserve anything or call a provider without all gates.
  if (!approved(config)) return failClosed('DISABLED_OR_UNAPPROVED');
  const envelope=prepareInternalEscalation(alert);
  if (!ledger || typeof ledger.claim!=='function' ||
      typeof ledger.accept!=='function' || typeof ledger.fail!=='function' ||
      !transport || typeof transport.send!=='function')
    return failClosed('ADAPTER_NOT_CONFIGURED');
  const message=prepareStagingEmail(envelope,config.recipient);
  const lease=await ledger.claim({alertId:envelope.alert_id,key:message.idempotencyKey});
  if (lease?.acquired===false) return failClosed('DUPLICATE_OR_LEASED');
  if (lease?.acquired!==true || !UUID.test(lease?.leaseId))
    return failClosed('LEDGER_CLAIM_UNVERIFIED');
  let result;
  try {result=await transport.send(message);}
  catch {
    try {await ledger.fail({alertId:envelope.alert_id,leaseId:lease.leaseId,reason:'PROVIDER_REQUEST_FAILED'});}
    catch {return failClosed('FAILED_LEDGER_WRITE_UNCERTAIN');}
    return failClosed('PROVIDER_REQUEST_FAILED');
  }
  if (!result || result.accepted!==true ||
      typeof result.messageId!=='string' || !RECEIPT.test(result.messageId) ||
      typeof result.acceptedAt!=='string' ||
      !Number.isFinite(Date.parse(result.acceptedAt)) ||
      !/(?:Z|[+-]\d{2}:\d{2})$/.test(result.acceptedAt)) {
    try {await ledger.fail({alertId:envelope.alert_id,leaseId:lease.leaseId,reason:'PROVIDER_ACCEPTANCE_UNVERIFIED'});}
    catch {return failClosed('FAILED_LEDGER_WRITE_UNCERTAIN');}
    return failClosed('PROVIDER_ACCEPTANCE_UNVERIFIED');
  }
  const receipt=freeze({
    alertId:envelope.alert_id,leaseId:lease.leaseId,
    providerMessageId:result.messageId,
    providerAcceptedAt:new Date(result.acceptedAt).toISOString()
  });
  let saved=false;
  try {saved=await ledger.accept(receipt);} catch { /* no auto-retry */ }
  if (saved!==true) return freeze({
    status:'PROVIDER_ACCEPTED_RECEIPT_UNCERTAIN_DO_NOT_RETRY',
    providerAccepted:true,humanAcknowledged:false
  });
  return freeze({
    status:'PROVIDER_ACCEPTED_NOT_HUMAN_ACK',
    providerAccepted:true,humanAcknowledged:false,
    providerMessageId:result.messageId
  });
}
