// Isolated PostgreSQL/WASM fixture only. No production URL/key is accepted.
import { PGlite } from '@electric-sql/pglite';
import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const db = new PGlite();
const tables = ['creator_applications', 'advertiser_inquiries', 'contact_inquiries'];
const verification = await readFile(new URL('../docs/security/lead-generation-containment-verification.sql',import.meta.url),'utf8');
let checks = 0;
await db.exec(`CREATE ROLE anon; CREATE ROLE authenticated; CREATE ROLE service_role BYPASSRLS;
  GRANT USAGE ON SCHEMA public TO anon, authenticated, service_role;`);
for (const table of tables) {
  await db.exec(`CREATE TABLE public.${table}(id integer PRIMARY KEY, email text NOT NULL);
    INSERT INTO public.${table} VALUES(1,'synthetic@example.invalid');
    GRANT ALL ON public.${table} TO anon, authenticated, service_role;`);
}
await db.exec(`CREATE VIEW public.alpha_lead_summary WITH (security_invoker=true) AS
 SELECT id,email FROM creator_applications UNION ALL SELECT id,email FROM advertiser_inquiries
 UNION ALL SELECT id,email FROM contact_inquiries;
 GRANT SELECT ON public.alpha_lead_summary TO anon,authenticated,service_role;`);
const asRole = async (role, sql) => {
  await db.exec(`SET ROLE ${role}`);
  try { return await db.query(sql); } finally { await db.exec('RESET ROLE'); }
};
for (const role of ['anon','authenticated']) {
  for (const table of tables) {
    assert.equal((await asRole(role,`SELECT count(*)::int AS n FROM ${table}`)).rows[0].n,1);checks++;
  }
  assert.equal((await asRole(role,'SELECT count(*)::int AS n FROM alpha_lead_summary')).rows[0].n,3);checks++;
}
console.log('BEFORE: both public roles can read all synthetic fixture tables/view (8 checks).');
const beforeCatalog = (await db.query(verification)).rows;
assert.equal(beforeCatalog.length,4);checks++;
for (const row of beforeCatalog) { assert.equal(row.passed,false);checks++; }
await db.exec(await readFile(new URL('../docs/security/lead-generation-containment-proposal.sql',import.meta.url),'utf8'));
const afterCatalog = (await db.query(verification)).rows;
assert.equal(afterCatalog.length,4);checks++;
for (const row of afterCatalog) { assert.equal(row.passed,true);checks++; }
for (const role of ['anon','authenticated']) {
  for (const table of tables) {
    for (const sql of [`SELECT * FROM ${table}`,`INSERT INTO ${table} VALUES(2,'denied@example.invalid')`,
      `UPDATE ${table} SET email='denied@example.invalid' WHERE id=1`,`DELETE FROM ${table} WHERE id=1`]) {
      await assert.rejects(asRole(role,sql),e=>e.code==='42501');checks++;
    }
  }
  await assert.rejects(asRole(role,'SELECT * FROM alpha_lead_summary'),e=>e.code==='42501');checks++;
}
for (const table of tables) {
  // Defense in depth: simulated later GRANT cannot override restrictive RLS.
  await db.exec(`GRANT SELECT,INSERT,UPDATE,DELETE ON ${table} TO anon,authenticated`);
  for (const role of ['anon','authenticated']) {
    assert.equal((await asRole(role,`SELECT count(*)::int AS n FROM ${table}`)).rows[0].n,0);checks++;
    await assert.rejects(asRole(role,`INSERT INTO ${table} VALUES(2,'denied@example.invalid')`),e=>e.code==='42501');checks++;
    assert.equal((await asRole(role,`UPDATE ${table} SET email='denied@example.invalid' WHERE id=1 RETURNING id`)).rows.length,0);checks++;
    assert.equal((await asRole(role,`DELETE FROM ${table} WHERE id=1 RETURNING id`)).rows.length,0);checks++;
  }
  await db.exec(`REVOKE ALL ON ${table} FROM anon,authenticated`);
  assert.equal((await asRole('service_role',`SELECT count(*)::int AS n FROM ${table}`)).rows[0].n,1);checks++;
  await asRole('service_role',`INSERT INTO ${table} VALUES(3,'backend@example.invalid')`);checks++;
  assert.equal((await asRole('service_role',`UPDATE ${table} SET email='updated@example.invalid' WHERE id=3 RETURNING id`)).rows.length,1);checks++;
  assert.equal((await asRole('service_role',`DELETE FROM ${table} WHERE id=3 RETURNING id`)).rows.length,1);checks++;
}
console.log(`PASS: ${checks} isolated checks; public CRUD/view denied, RLS survives later GRANT, service-role synthetic CRUD preserved.`);
console.log('NOT TESTED: hosted application/GHL integrations, HTTP API, actual incident history. No production changes.');
await db.close();
