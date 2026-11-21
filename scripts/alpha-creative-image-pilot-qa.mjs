#!/usr/bin/env node
// Read-only offline review sheet for the existing Bliss paid-image pilot.
// Never scores generated art automatically. No API, payment or delivery calls.
import {existsSync,readFileSync,lstatSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
const DIR=resolve(process.env.ALPHA_IMAGE_PILOT_DIR || join(process.cwd(),'.private','alpha-creative-image-pilot'));
const FILE=join(DIR,'pilot-ledger.json');
const sha=(b)=>createHash('sha256').update(b).digest('hex');
const base={directive:'ALPHA-CREATIVE-IMAGE-PILOT-20261120',
  model:'gpt-image-2.5-flare',plannedCount:11,liveExecution:'NOT_VERIFIED',
  artifacts:[],cumulativeUsd:'0.000000',remainingAuthorizedUsd:'20.000000',
  commercialActivation:'NOT_READY',globalStop:'UNCHANGED',delivery:'NOT_SENT'};
if(existsSync(FILE)){
  const s=JSON.parse(readFileSync(FILE,'utf8'));
  if(s.schema!=='alpha.creative.pilot.ledger.v1'||s.directive!==base.directive||
      s.model!==base.model||s.noDelivery!==true||s.capUsd!==20||
      !Number.isSafeInteger(s.spentMicroUsd)||!Number.isSafeInteger(s.heldMicroUsd)||
      s.spentMicroUsd<0||s.heldMicroUsd<0||
      s.spentMicroUsd+s.heldMicroUsd>20_000_000||
      !Array.isArray(s.entries) || s.entries.length>11 ||
      s.entries.some(e=>!e || typeof e!=='object' || !Number.isInteger(e.index) ||
      e.index<0 || e.index>10 || !Number.isSafeInteger(e.reservedMicroUsd) ||
      e.reservedMicroUsd!==2_000_000 || e.delivery!=='NOT_SENT' ||
      e.campaignReady!==false || e.model!=='gpt-image-2.5-flare' ||
      !['MASTER','VARIATION'].includes(e.role))) throw Error('PILOT_QA_REFUSED_INCONSISTENT_LEDGER');
  const slots=s.entries.map(e=>e.index);
  if(new Set(slots).size!==slots.length || slots.some((v,i)=>v!==i) ||
      s.entries.some(e=>e.role!==(e.index===0?'MASTER':'VARIATION')))
    throw Error('PILOT_QA_REFUSED_INVALID_ARTWORK_SEQUENCE');
  const metered=s.entries.filter(e=>e.status==='COMPLETE');
  if(metered.some(e=>!Number.isSafeInteger(e.costMicroUsd) || e.costMicroUsd<=0 ||
      e.costMicroUsd>e.reservedMicroUsd || typeof e.providerRequestId!=='string' ||
      !e.providerRequestId.trim() ||
      !Number.isSafeInteger(e.latencyMs) || e.latencyMs<0) ||
     metered.reduce((sum,e)=>sum+e.costMicroUsd,0)!==s.spentMicroUsd ||
     s.entries.filter(e=>e.status!=='COMPLETE').length*2_000_000!==s.heldMicroUsd)
     throw Error('PILOT_QA_REFUSED_COST_OR_PROVIDER_RECEIPT_MISMATCH');
  base.cumulativeUsd=(s.spentMicroUsd/1e6).toFixed(6);
  base.remainingAuthorizedUsd=((20_000_000-s.spentMicroUsd-s.heldMicroUsd)/1e6).toFixed(6);
  base.liveExecution=s.entries.length>0?'PROVIDER_ATTEMPT_RECORDED':'NOT_EXECUTED';
  base.unknownOrUnreconciledCharges=s.closed || s.heldMicroUsd!==0 ||
      s.entries.some(e=>e.status!=='COMPLETE');
  base.artifacts=s.entries.map(e=>{
    const expectedName='alpha-image-'+String(e.index).padStart(2,'0')+'.png';
    const expectedPath=join(DIR,expectedName);
    const validName=e.imageFile===expectedName;
    const safeFile=validName && existsSync(expectedPath) &&
      lstatSync(expectedPath).isFile() && !lstatSync(expectedPath).isSymbolicLink() &&
      lstatSync(expectedPath).size<=25_000_000;
    const bytes=e.status==='COMPLETE' && safeFile?readFileSync(expectedPath):null;
    const imageShaOk=!!bytes &&
      bytes.subarray(0,8).toString('hex')==='89504e470d0a1a0a' &&
      sha(bytes)===e.imageSha256;
    return {
      index:e.index,role:e.role,language:e.language,model:e.model,
      providerRequestId:e.providerRequestId||'UNKNOWN',
      imageSha256:e.imageSha256||'',imageShaVerified:imageShaOk,
      outputFile:imageShaOk?expectedPath:'NOT_VERIFIED',
      usageCostUsd:Number.isSafeInteger(e.costMicroUsd)?(e.costMicroUsd/1e6).toFixed(6):'UNKNOWN',
      inputTokens:e.inputTokens??null,textInputTokens:e.textInputTokens??null,
      imageInputTokens:e.imageInputTokens??null,outputTokens:e.outputTokens??null,
      generationLatencyMs:e.latencyMs??null,
      humanQualityReview:{
        visualQuality_1to5:null,
        productAccuracy_350mL_can_1to5:null,
        brandingAndGoldLighthouse_1to5:null,
        translatedHeadlineAccuracy_1to5:null,
        realismAndComposition_1to5:null,
        defects:[],
        reviewer:null,
        approval:'PENDING_HUMAN_EVIDENCE'
      },
      externalDelivery:'NOT_SENT',campaignReady:false
    };
  });
}
console.log(JSON.stringify(base,null,2));
