import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  EXPECTED_ORIGIN, TARGETS, validatePublicConfig, probePublicDenials
} from './lead-public-rest-denial-probe.mjs';

const config = {
  origin: EXPECTED_ORIGIN,
  publishableKey: 'sb_publishable_offlinefixture1234'
};
const denied = { status: 403, json: async () => ({ code: '42501', message: 'permission denied' }) };

test('refuses unknown domains, JWTs, service keys, missing and malformed public keys', () => {
  const invalid = [
    { ...config, origin: 'https://other.supabase.co' },
    { ...config, origin: 'http://yyesqcyjhqjryaskrqip.supabase.co' },
    { ...config, publishableKey: 'sb_secret_exampleinvalid' },
    { ...config, publishableKey: 'eyJhbGciOiJIUzI1NiJ9.payload.signature' },
    { ...config, publishableKey: '' },
    { ...config, publishableKey: undefined }
  ];
  for (const item of invalid) assert.throws(() => validatePublicConfig(item), /REFUSED:/);
  assert.equal(validatePublicConfig(config).origin, EXPECTED_ORIGIN);
});

test('GETs exactly four allowlisted routes with limit=0, publishable key only and no redirects', async () => {
  const calls = [];
  const request = async (url, opts) => {
    calls.push({ url, opts });
    return denied;
  };
  const result = await probePublicDenials(config, request);
  assert.deepEqual(result.map(x => x.object), [...TARGETS]);
  assert.equal(result.length, 4);
  for (let i = 0; i < calls.length; i++) {
    const { url, opts } = calls[i];
    const u = new URL(url);
    assert.equal(u.origin, EXPECTED_ORIGIN);
    assert.equal(u.pathname, '/rest/v1/' + TARGETS[i]);
    assert.equal(u.searchParams.get('select'), '*');
    assert.equal(u.searchParams.get('limit'), '0');
    assert.equal(opts.method, 'GET');
    assert.equal(opts.redirect, 'manual');
    assert.equal(opts.headers.apikey, config.publishableKey);
    assert.equal(opts.headers.authorization, undefined);
    assert.ok(opts.signal);
  }
});

test('SQLSTATE 42501 on HTTP 401 is accepted as PostgreSQL grant denial', async () => {
  const result = await probePublicDenials(config,
    async () => ({ status: 401, json: async () => ({ code: '42501' }) }));
  assert.equal(result.length, 4);
});

test('HTTP 200, even with empty rows, must FAIL and its body must not be read', async () => {
  let touched = 0;
  await assert.rejects(
    probePublicDenials(config, async () => ({
      status: 200,
      json: async () => { touched++; return []; }
    })),
    /FAIL: advertiser_inquiries/
  );
  assert.equal(touched, 0);
});

test('rejects wrong/missing key authentication errors and unclear access denials', async () => {
  for (const response of [
    { status: 401, json: async () => ({ code: 'PGRST301' }) },
    { status: 403, json: async () => ({ code: 'JWT_INVALID' }) },
    { status: 401, json: async () => ({ message: 'API key required' }) },
    { status: 302, json: async () => ({ code: '42501' }) },
    { status: 500, json: async () => ({ code: '42501' }) }
  ]) {
    await assert.rejects(probePublicDenials(config, async () => response),
      /^(INCONCLUSIVE|FAIL):/);
  }
});

test('rejects HTML gateway response without exposing its contents', async () => {
  await assert.rejects(probePublicDenials(config, async () =>
    ({ status: 403, json: async () => { throw new SyntaxError('private body'); } })),
    /INCONCLUSIVE: .*non-JSON/);
});

test('fails closed if any later table leaks access even after previous 42501 checks', async () => {
  let n = 0;
  await assert.rejects(probePublicDenials(config, async () => (++n === 4
    ? { status: 200, json: async () => [] }
    : denied)), /FAIL: alpha_lead_summary/);
  assert.equal(n, 4);
});
