// Synthetic PostgreSQL/WASM contract for the DRAFT function-hardening SQL.
// Does not emulate event-trigger wiring: that is verified by the separate
// read-only metadata query against the real confirmed project.
import { PGlite } from '@electric-sql/pglite';
import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const db = new PGlite();
const proposal = await readFile(new URL('../docs/security/lead-generation-function-security-hardening-proposal.sql',import.meta.url),'utf8');
let checks = 0;
try {
  await db.exec(`
    CREATE ROLE anon;
    CREATE ROLE authenticated;
    CREATE ROLE service_role;
    GRANT USAGE ON SCHEMA public TO anon, authenticated, service_role;
    CREATE FUNCTION public.rls_auto_enable() RETURNS void
      LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog
      AS $$BEGIN NULL; END;$$;
    CREATE FUNCTION public.set_updated_at() RETURNS trigger
      LANGUAGE plpgsql AS $$
      BEGIN NEW.updated_at = now(); RETURN NEW; END;$$;
    CREATE TABLE public.function_security_fixture(
      id integer PRIMARY KEY, updated_at timestamptz);
    CREATE TRIGGER update_timestamp BEFORE UPDATE ON public.function_security_fixture
      FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();
    INSERT INTO public.function_security_fixture VALUES (1,'2020-01-01T00:00:00Z');
    GRANT EXECUTE ON FUNCTION public.rls_auto_enable() TO anon,authenticated,service_role;
  `);
  const metadata = async name => (await db.query(
    `SELECT p.oid,p.prosrc,p.prosecdef,p.proconfig::text as config,
       has_function_privilege('anon',p.oid,'EXECUTE') AS anon_exec,
       has_function_privilege('authenticated',p.oid,'EXECUTE') AS auth_exec,
       has_function_privilege('service_role',p.oid,'EXECUTE') AS service_exec
       FROM pg_proc p JOIN pg_namespace n ON p.pronamespace=n.oid
       WHERE n.nspname='public' AND p.proname='${name}'`
  )).rows[0];
  const beforeEvent=await metadata('rls_auto_enable');
  const beforeTimestamp=await metadata('set_updated_at');
  assert.equal(beforeEvent.anon_exec,true);checks++;
  assert.equal(beforeEvent.auth_exec,true);checks++;
  assert.equal(beforeEvent.prosecdef,true);checks++;
  assert.equal(beforeTimestamp.config,null);checks++;
  // Execute EXACT review SQL; the fixture intentionally substitutes an
  // ordinary no-op SECURITY DEFINER for event_trigger, which PGlite does not
  // certify. Owner acceptance checks real event-trigger identity separately.
  await db.exec(proposal);
  for(let iteration=0;iteration<2;iteration++){
    const event=await metadata('rls_auto_enable');
    const timestamp=await metadata('set_updated_at');
    assert.equal(event.anon_exec,false);checks++;
    assert.equal(event.auth_exec,false);checks++;
    assert.equal(event.service_exec,true);checks++;
    assert.equal(event.prosecdef,true);checks++;
    assert.equal(event.prosrc,beforeEvent.prosrc);checks++;
    assert.equal(timestamp.prosrc,beforeTimestamp.prosrc);checks++;
    assert.equal(timestamp.prosecdef,false);checks++;
    assert.ok(timestamp.config.includes('search_path=pg_catalog'));checks++;
    // Existing timestamp trigger behavior still works; no customer rows.
    await db.exec(`UPDATE function_security_fixture SET id=1 WHERE id=1;`);
    const t=(await db.query('SELECT updated_at IS NOT NULL AS updated FROM function_security_fixture')).rows[0];
    assert.equal(t.updated,true);checks++;
    if(iteration===0)await db.exec(proposal); // Idempotency test.
  }
  // A mistaken future grant must be detectable; do not silently count it as a pass.
  await db.exec('GRANT EXECUTE ON FUNCTION public.rls_auto_enable() TO anon');
  assert.equal((await metadata('rls_auto_enable')).anon_exec,true);checks++;
  await db.exec(proposal);
  assert.equal((await metadata('rls_auto_enable')).anon_exec,false);checks++;
  console.log(`PASS: ${checks} isolated function-security checks; grants removed, source bodies retained, timestamp trigger works, proposal idempotent.`);
  console.log('NOT TESTED: actual event-trigger identity/firing in PGlite, hosted REST/CRM integrations or production changes.');
} finally {
  await db.close();
}
