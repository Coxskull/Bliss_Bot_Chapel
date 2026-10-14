const storageKey='bliss-financial-pending-v1';
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,hash=/^[0-9A-F]{64}$/;
export async function digest(text){return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(text))),b=>b.toString(16).padStart(2,'0')).join('').toUpperCase();}
export function recoveryStore(storage){
 function read(){let raw;try{raw=storage.getItem(storageKey);}catch{throw Error('Recovery storage unavailable; no new note can be submitted.');}if(raw===null)return null;let p;try{p=JSON.parse(raw);}catch{throw Error('Pending recovery record is invalid; actions stopped.');}if(!p||p.version!==1||Object.keys(p).sort().join(',')!=='actorKey,evidenceFingerprint,idempotencyKey,noteHash,operationId,version'||!hash.test(p.actorKey)||!hash.test(p.evidenceFingerprint)||!hash.test(p.noteHash)||!uuid.test(p.operationId)||!uuid.test(p.idempotencyKey))throw Error('Pending recovery record is invalid; actions stopped.');return p;}
 return {
  load(actorKey){const p=read();if(p&&p.actorKey!==actorKey)throw Error('Pending note belongs to another reviewer. Sign in as that reviewer to resolve it.');return p;},
  async save(actorKey,p){if(read())throw Error('Resolve the pending note before submitting another.');const record={version:1,actorKey,operationId:p.operationId,evidenceFingerprint:p.evidenceFingerprint.toUpperCase(),idempotencyKey:p.idempotencyKey,noteHash:await digest(p.note)};if(read())throw Error('Resolve the pending note before submitting another.');const raw=JSON.stringify(record);try{storage.setItem(storageKey,raw);if(storage.getItem(storageKey)!==raw)throw Error();}catch{throw Error('Recovery storage unavailable; no new note was submitted.');}return read();},
  clear(expected){const p=read();if(p&&JSON.stringify(p)!==JSON.stringify(expected))throw Error('Pending recovery changed; actions stopped.');try{storage.removeItem(storageKey);if(storage.getItem(storageKey)!==null)throw Error();}catch{throw Error('Recovery record could not be cleared; inspect history before continuing.');}}
 };
}
export async function matches(record,note){return note.operationId===record.operationId&&note.evidenceFingerprint.toUpperCase()===record.evidenceFingerprint&&await digest(note.note)===record.noteHash&&await digest('financial-review-recovery-v1\0'+note.actorSubject)===record.actorKey;}
