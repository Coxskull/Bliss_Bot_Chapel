#!/usr/bin/env node
// ALPHA-CREATIVE-IMAGE-PILOT-20261120
// Read-only project model-visibility check. Absolutely NO image/model calls,
// spending, API key printing, key creation, Factory dispatch or STOP changes.
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const MODEL = 'gpt-image-2.5-flare';
const ENDPOINT = 'https://api.openai.com/v1/models/gpt-image-2.5-flare';

export async function probeModel(fetchImpl, { key, scope, stop } = {}) {
  if (scope !== 'ISOLATED_NO_DELIVERY' || stop !== 'ENGAGED')
    throw new Error('MODEL_PROBE_HELD: ISOLATION_OR_STOP_UNATTESTED');
  if (typeof key !== 'string' || !key.startsWith('sk-') || key.length > 512)
    throw new Error('MODEL_PROBE_HELD: PRIVATE_PROJECT_KEY_MISSING');
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  try {
    const response = await fetchImpl(ENDPOINT, {
      method: 'GET',
      headers: { Authorization: 'Bearer ' + key, Accept: 'application/json' },
      redirect: 'error', signal: controller.signal,
      cache: 'no-store'
    });
    // Refuse redirects and report status only. Do not echo provider error bodies
    // or any user/token/account identifying information.
    if (!response.ok)
      return { model: MODEL, result: 'ACCESS_NOT_VERIFIED',
        httpStatus: response.status, paidCalls: 0, imageCount: 0,
        generationEntitlement: 'NOT_TESTED', delivery: 'NOT_SENT' };
    const raw = await response.text();
    if (raw.length > 131072) throw new Error('MODEL_PROBE_HELD: OVERSIZED_METADATA');
    const data = JSON.parse(raw);
    if (data?.object !== 'model' || data?.id !== MODEL)
      throw new Error('MODEL_PROBE_HELD: WRONG_MODEL_METADATA');
    return { model: MODEL, result: 'PROJECT_MODEL_VISIBLE', httpStatus: 200,
      paidCalls: 0, imageCount: 0,
      generationEntitlement: 'NOT_TESTED', delivery: 'NOT_SENT' };
  } finally { clearTimeout(timeout); }
}

async function main() {
  try {
    const result = await probeModel(fetch, {
      key: process.env.OPENAI_API_KEY,
      scope: process.env.ALPHA_IMAGE_PILOT_SCOPE,
      stop: process.env.ALPHA_GLOBAL_STOP
    });
    process.stdout.write(JSON.stringify(result) + '\n');
    if (result.result !== 'PROJECT_MODEL_VISIBLE') process.exitCode = 2;
  } catch (error) {
    // Strip sensitive error objects, provider bodies and request/response data.
    const code = error?.message?.startsWith('MODEL_PROBE_HELD:')
      ? error.message : 'MODEL_PROBE_HELD: NETWORK_OR_PROVIDER_RESPONSE_FAILURE';
    process.stderr.write(code + '\n');
    process.exitCode = 2;
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url))
  await main();
