#!/usr/bin/env node
// Read-only offline review sheet for the existing Bliss paid-image pilot.
// Never scores generated art automatically. No API, payment or delivery calls.
import {existsSync,readFileSync} from 'node:fs';
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
      !Array.isArray(s.entries)) throw Error('PILOT_QA_REFUSED_INCONSISTENT_LEDGER');
  base.cumulativeUsd=(s.spentMicroUsd/1e6).toFixed(6);
  base.remainingAuthorizedUsd=((20_000_000-s.spentMicroUsd-s.heldMicroUsd)/1e6).toFixed(6);
  base.liveExecution=s.entries.length>0?'PROVIDER_ATTEMPT_RECORDED':'NOT_EXECUTED';
  base.unknownOrUnreconciledCharges=s.closed || s.heldMicroUsd!==0 ||
      s.entries.some(e=>e.status!=='COMPLETE');
  base.artifacts=s.entries.map(e=>{
    const imagePath=join(DIR,String(e.imageFile||''));
    const bytes=e.status==='COMPLETE' && existsSync(imagePath)?readFileSync(imagePath):null;
    const imageShaOk=!!bytes&&sha(bytes)===e.imageSha256;
    return {
      index:e.index,role:e.role,language:e.language,model:e.model,
      providerRequestId:e.providerRequestId||'UNKNOWN',
      imageSha256:e.imageSha256||'',imageShaVerified:imageShaOk,
      outputFile:imageShaOk?imagePath:'NOT_VERIFIED',
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
