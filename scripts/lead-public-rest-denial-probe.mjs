// Explicit, non-mutating verification of PUBLIC Data API denials after RLS containment.
// Never invoked by CI against Supabase. No service_role or session JWT accepted.
// Run only after ALPHA owner approves safe public GET probes.
// Expected: 4x PostgREST 42501 from revoked anon table/view SELECT grants.
import { fileURLToPath } from 'node:url';

export const EXPECTED_ORIGIN = 'https://yyesqcyjhqjryaskrqip.supabase.co';
export const TARGETS = Object.freeze([
  'advertiser_inquiries', 'contact_inquiries',
  'creator_applications', 'alpha_lead_summary'
]);
const KEY_PATTERN = /^sb_publishable_[A-Za-z0-9_-]{8,}$/;

export function validatePublicConfig(config) {
  if (config.origin !== EXPECTED_ORIGIN) {
    throw new Error('REFUSED: origin is not the confirmed Alpha Lead Generation project');
  }
  if (typeof config.publishableKey !== 'string' ||
      !KEY_PATTERN.test(config.publishableKey)) {
    throw new Error('REFUSED: only a modern sb_publishable_ key is permitted; never use a secret, service_role key or JWT');
  }
  return config;
}

export async function probePublicDenials(config, request = fetch) {
  validatePublicConfig(config);
  const results = [];
  for (const object of TARGETS) {
    const url = new URL('/rest/v1/' + object, config.origin);
    url.searchParams.set('select', '*');
    url.searchParams.set('limit', '0');
    const response = await request(url.toString(), {
      method: 'GET',
      headers: { apikey: config.publishableKey, accept: 'application/json' },
      redirect: 'manual',
      signal: AbortSignal.timeout(8000)
    });

    // Even HTTP 200 returning zero rows is NOT a permission-grant denial.
    // Never read/print a successful response: it could inadvertently contain data.
    if (response.status !== 401 && response.status !== 403) {
      throw new Error('FAIL: ' + object + ' did not return an HTTP permission denial (' + response.status + ')');
    }

    let body;
    try {
      body = await response.json();
    } catch {
      throw new Error('INCONCLUSIVE: ' + object + ' returned a non-JSON gateway/auth error');
    }
    // 401 alone may mean a wrong/expired key. Only SQLSTATE 42501 proves
    // the PostgREST role reached the object and was denied by grants.
    if (body?.code !== '42501') {
      throw new Error('INCONCLUSIVE: ' + object +
        ' did not return PostgreSQL 42501 (status ' + response.status + ')');
    }
    results.push({ object, status: response.status, sqlstate: '42501' });
  }
  return results;
}

async function main() {
  try {
    const config = {
      origin: process.env.ALPHA_LEADS_SUPABASE_ORIGIN,
      publishableKey: process.env.ALPHA_LEADS_PUBLISHABLE_KEY
    };
    const results = await probePublicDenials(config);
    for (const r of results) {
      console.log('PASS: ' + r.object + ' HTTP ' + r.status +
        ' SQLSTATE ' + r.sqlstate + ' (zero-row GET)');
    }
    console.log('4/4 public REST-denial probes passed. No data was requested beyond limit=0.');
    console.log('LIMITATION: authenticated-role denials, allowed server intake, GHL/n8n workflows and historical incident evidence NOT TESTED.');
  } catch (err) {
    // Never log a response body, key, token, full URL or fetch exception internals.
    console.error(err instanceof Error && /^(FAIL|INCONCLUSIVE|REFUSED):/.test(err.message)
      ? err.message : 'INCONCLUSIVE: public Data API probe failed; inspect network/config privately');
    process.exitCode = 1;
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await main();
}
