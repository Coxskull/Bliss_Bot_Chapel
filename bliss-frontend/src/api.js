export class ReviewError extends Error {
  constructor(message, status = 0) { super(message); this.status = status; }
}
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const fingerprint = /^[0-9a-f]{64}$/i;
const modules = ['BlissBot', 'CreatorAcademy', 'WeddingPlanner'];
const boundString = (v, max) => typeof v === 'string' && v.length <= max;
const noteValid = n => n && uuid.test(n.id) && uuid.test(n.operationId) && boundString(n.actorSubject,200) && fingerprint.test(n.evidenceFingerprint) && boundString(n.note,2000) && typeof n.recordedAt === 'string';
export function validateResult(kind, value) {
  if (kind === 'export') return value && boundString(value.artifactJson,2000000) && fingerprint.test(value.sha256);
  if (kind === 'recovery') return value && fingerprint.test(value.actorKey);
  if (kind === 'queue') return value && Array.isArray(value.items) && value.items.length <= 100 && (value.nextAfter === null || uuid.test(value.nextAfter)) && value.items.every(i => uuid.test(i.operationId) && i.scope && uuid.test(i.scope.merchantId) && uuid.test(i.scope.campaignId) && (modules.includes(i.scope.module) || [0,1,2].includes(i.scope.module)) && i.currency === 'USD' && /^\d+$/.test(i.amountMinor) && typeof i.amountMinor === 'string' && boundString(i.reason,200));
  if (kind === 'case') return value && uuid.test(value.operationId) && typeof value.requiresReview === 'boolean' && fingerprint.test(value.evidenceFingerprint) && boundString(value.evidenceJson,1000000) && boundString(value.reason,200);
  if (kind === 'notes') return value && Array.isArray(value.items) && value.items.length <= 100 && value.items.every(noteValid) && (value.nextAfter === null || uuid.test(value.nextAfter));
  if (kind === 'retry') return value === null || noteValid(value);
  if (kind === 'note') return noteValid(value);
  if (kind === 'history') return value && noteValid(value.note) && boundString(value.evidenceJson,1000000);
  return false;
}
export function createReviewApi(fetcher = fetch) {
  async function request(path, kind, options = {}) {
    let response;
    try { response = await fetcher(path, {credentials:'same-origin',cache:'no-store',redirect:'error',...options}); }
    catch { throw new ReviewError('Connection interrupted. Check note history before retrying a write.'); }
    if (!response.ok) {
      const messages = {401:'Session expired. Sign in again.',403:'Access denied for this review scope.',409:'Evidence changed or is busy. Refresh the case before a new annotation.',429:'Write limit reached. Wait before retrying.'};
      throw new ReviewError(messages[response.status] || 'Request refused. Refresh your session and case.', response.status);
    }
    let body; try { body = await response.json(); } catch { throw new ReviewError('Invalid server response.'); }
    if (body.mode !== 'MOCK' || body.stripeAccountStatus !== 'UNVERIFIED / OWNER DECISION REQUIRED' || body.paymentCompleted !== false || body.approvalGranted !== false || body.holdReleased !== false || !validateResult(kind,body.result)) throw new ReviewError('Unexpected financial response; actions stopped.');
    if (kind === 'queue') body.result.items = body.result.items.map(i => ({...i,scope:{...i.scope,module:typeof i.scope.module === 'number' ? modules[i.scope.module] : i.scope.module}}));
    return body.result;
  }
  return {
    async session() {
      let response;try { response = await fetcher('/api/auth/session',{credentials:'same-origin',cache:'no-store',redirect:'error'}); } catch { throw new ReviewError('Session unavailable.'); }
      if (!response.ok) throw new ReviewError('Session unavailable.',response.status);
      const session = await response.json();
      if (!session || typeof session.authenticationEnabled !== 'boolean' || typeof session.isAuthenticated !== 'boolean' || !Array.isArray(session.roles)) throw new ReviewError('Invalid session response.');
      return session;
    },
    recoveryContext: () => request('/api/financials/mock-review/recovery-context','recovery'),
    queue: (module, after) => request('/api/financials/mock-review/queue?limit=25'+(module?'&module='+encodeURIComponent(module):'')+(after?'&after='+encodeURIComponent(after):''),'queue'),
    case: id => request('/api/financials/mock-review/cases/'+encodeURIComponent(id),'case'),
    notes: (id,after) => request('/api/financials/mock-review/cases/'+encodeURIComponent(id)+'/notes?limit=25'+(after?'&after='+encodeURIComponent(after):''),'notes'),
    retry: (id,key) => request('/api/financials/mock-review/cases/'+encodeURIComponent(id)+'/note-retries/'+encodeURIComponent(key),'retry'),
    exportEvidence: (id,version) => request('/api/financials/mock-review/cases/'+encodeURIComponent(id)+'/evidence-export?evidenceFingerprint='+encodeURIComponent(version),'export'),
    history: id => request('/api/financials/mock-review/notes/'+encodeURIComponent(id),'history'),
    add: (operationId,evidenceFingerprint,note,idempotencyKey,csrfToken) => {
      if (!csrfToken || !uuid.test(operationId) || !fingerprint.test(evidenceFingerprint) || !note.trim() || note.length>2000 || !uuid.test(idempotencyKey)) throw new ReviewError('Fresh session, case and bounded note required.');
      return request('/api/financials/mock-review/notes','note',{method:'POST',headers:{'Content-Type':'application/json','X-CSRF-TOKEN':csrfToken},body:JSON.stringify({operationId,evidenceFingerprint,note,idempotencyKey})});
    }
  };
}
