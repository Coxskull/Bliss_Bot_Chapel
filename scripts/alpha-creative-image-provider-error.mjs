// Safe, read-only parsing of a rejected OpenAI image API response.
// Only allowlisted machine fields are retained. Never record raw bodies,
// free-form provider messages, prompts, authorization headers or secrets.
const TOKEN = /^[a-z][a-z0-9_.-]{0,63}$/;
const REQUEST_ID = /^req_[A-Za-z0-9_-]{1,128}$/;
const safeToken = value => typeof value === 'string' && TOKEN.test(value) ? value : 'UNAVAILABLE';

export async function summarizeProviderRejection(response) {
  const summary = {
    httpStatus: Number.isInteger(response?.status) ? response.status : 0,
    errorCode: 'UNAVAILABLE',
    errorType: 'UNAVAILABLE',
    providerRequestId: 'UNAVAILABLE',
  };
  const id = response?.headers?.get?.('x-request-id');
  if (typeof id === 'string' && REQUEST_ID.test(id)) summary.providerRequestId = id;
  try {
    // This is called on a non-2xx response only. Read bounded error metadata
    // and avoid echoing the provider's untrusted free-form error message.
    const declaredLength = response?.headers?.get?.('content-length');
    if (declaredLength != null && (!/^\d+$/.test(declaredLength) || Number(declaredLength) > 8192)) return summary;
    const body = await response.text();
    if (body.length > 8192) return summary;
    const payload = JSON.parse(body);
    summary.errorCode = safeToken(payload?.error?.code);
    summary.errorType = safeToken(payload?.error?.type);
  } catch { /* An opaque error must not block the protected fail-closed path. */ }
  return summary;
}
