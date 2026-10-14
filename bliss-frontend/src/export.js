import {digest} from './recovery.js';
export async function verifyExport(result,expected){
 if(!result||typeof result.artifactJson!=='string'||result.artifactJson.length>2000000||!/^\w{64}$/.test(result.sha256)||await digest(result.artifactJson)!==result.sha256)throw Error('Evidence export checksum mismatch; no file downloaded.');
 let artifact;try{artifact=JSON.parse(result.artifactJson);}catch{throw Error('Malformed evidence export; no file downloaded.');}
 if(artifact.schema!=='BLISS-MOCK-CASE-EVIDENCE/1'||artifact.mode!=='MOCK'||artifact.stripeAccountStatus!=='UNVERIFIED / OWNER DECISION REQUIRED'||artifact.paymentCompleted!==false||artifact.approvalGranted!==false||artifact.holdReleased!==false||artifact.includesNotes!==false||artifact.operationId!==expected.operationId||artifact.evidenceFingerprint!==expected.evidenceFingerprint||typeof artifact.evidenceJson!=='string'||await digest(artifact.evidenceJson)!==expected.evidenceFingerprint)throw Error('Evidence export case/version mismatch; no file downloaded.');return artifact;
}
export function downloadExport(result,id){const url=URL.createObjectURL(new Blob([JSON.stringify(result,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='bliss-mock-case-'+id+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
