// Offline subprocess checks: fake psql, no database or network connection.
import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, chmodSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';

const script = resolve('tests/lead-containment/verify-real-event-trigger.sh');
const fixture = { PGHOST: '127.0.0.1', PGPORT: '5432', PGDATABASE: 'postgres',
  PGUSER: 'postgres', PGPASSWORD: 'isolated-ci-fixture-only' };
function run(overrides = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'lead-psql-offline-'));
  try {
    const fake = join(directory, 'psql');
    writeFileSync(fake, `#!/bin/sh
# Fail if libpq redirection or server-side options leak into the child.
for name in PGHOSTADDR PGSERVICE PGSERVICEFILE PGOPTIONS PGPASSFILE PGDATABASE PGHOST PGPORT PGUSER; do
  if printenv "$name" >/dev/null; then exit 81; fi
done
test "$PGPASSWORD" = isolated-ci-fixture-only || exit 82
test "$PGSSLMODE" = disable || exit 83
test "$PGCONNECT_TIMEOUT" = 5 || exit 84
case "$*" in
  '--host=127.0.0.1 --port=5432 --dbname=postgres --username=postgres --no-password -X -A -t -v ON_ERROR_STOP=1 -f tests/lead-containment/real-event-trigger-driver.sql') ;;
  *) exit 85 ;;
esac
printf '%s\\n' 'rls_auto_enable|f' 'set_updated_at|f' 'rls_auto_enable|t' 'set_updated_at|t' 'rls_auto_enable|t' 'set_updated_at|t' 'PASS: PostgreSQL 17 event trigger auto-RLS offline fixture'
`);
    chmodSync(fake, 0o700);
    return spawnSync('/bin/bash', [script], { encoding: 'utf8', timeout: 10000,
      env: { ...process.env, ...fixture, PATH: `${directory}:/usr/bin:/bin`, ...overrides } });
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

test('exact fixture uses only explicit loopback psql connection options', () => {
  const result = run(); assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /before 0\/2, after 2\/2/);
});
test('inherited libpq redirects and options cannot reach psql', () => {
  const result = run({ PGHOSTADDR: '203.0.113.5', PGSERVICE: 'production',
    PGSERVICEFILE: '/nonexistent', PGOPTIONS: '-c search_path=untrusted', PGPASSFILE: '/nonexistent' });
  assert.equal(result.status, 0, result.stderr);
});
for (const [name, value] of Object.entries({ PGHOST: '203.0.113.5', PGPORT: '6543',
  PGDATABASE: 'production', PGUSER: 'application', PGPASSWORD: 'not-the-fixture' })) {
  test(`reject mismatched ${name} before psql`, () => {
    const result = run({ [name]: value }); assert.equal(result.status, 1);
    assert.match(result.stderr, /REFUSED/);
    assert.doesNotMatch(result.stdout, /PASS/);
  });
}
