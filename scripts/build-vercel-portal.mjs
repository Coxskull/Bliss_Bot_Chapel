import { mkdir, readFile, copyFile, writeFile } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const input = process.env.BLISS_APPLICATION_ORIGIN;
if (!input) throw new Error('Set BLISS_APPLICATION_ORIGIN to the deployed Railway application HTTPS origin.');
const origin = new URL(input);
if (origin.protocol !== 'https:' || origin.username || origin.password || origin.pathname !== '/' || origin.search || origin.hash) {
  throw new Error('BLISS_APPLICATION_ORIGIN must be an HTTPS origin without credentials, path, query or fragment.');
}
const source = resolve(root, 'frontend/public');
const output = resolve(root, '.vercel-portal');
let html = await readFile(resolve(source, 'global-media.html'), 'utf8');
// Open the whole protected workspace on the application origin. Never split
// its session, CSRF and OIDC requests across Vercel and Railway origins.
for (const route of ['/', '/production.html', '/brand-intake.html', '/login.html']) {
  html = html.replaceAll(`href="${route}"`, `href="${new URL(route, origin).href}"`);
}
html = html.replaceAll('href="/global-media.html"', 'href="/"');
await mkdir(resolve(output, 'assets'), { recursive: true });
await writeFile(resolve(output, 'index.html'), html);
await copyFile(resolve(source, 'global-media.css'), resolve(output, 'global-media.css'));

// Public interest forms, policy pages and the creator account explainer may
// be published on Vercel. The authenticated dashboard and password setup stay
// on Railway so OIDC cookies, CSRF and API calls remain same-origin.
for (const filename of [
  'join-creator.html', 'join-advertiser.html', 'privacy-request.html',
  'creator-account.html', 'creator-login.html', 'advertiser-login.html',
  'alpha-privacy.html', 'alpha-terms.html', 'services.html', 'contact.html',
  'alpha-intake.css', 'alpha-intake.js'
]) {
  await copyFile(resolve(source, filename), resolve(output, filename));
}
let creatorAccount = await readFile(resolve(source, 'creator-account.html'), 'utf8');
for (const route of [
  '/creator-dashboard.html',
  '/api/auth/login?returnUrl=%2Fcreator-dashboard.html'
]) {
  creatorAccount = creatorAccount.replaceAll(
    `href="${route}"`,
    `href="${new URL(route, origin).href}"`
  );
}
await writeFile(resolve(output, 'creator-account.html'), creatorAccount);

// Only account login redirects leave Vercel: passwords and sessions remain
// on Railway. Never publish protected dashboards or their API requests here.
for (const [filename, routes] of [
  ['creator-login.html', ['/creator-signin-preview.html']],
  ['advertiser-login.html', ['/advertiser-signin-preview.html']]
]) {
  let page = await readFile(resolve(source, filename), 'utf8');
  for (const route of routes) {
    page = page.replaceAll(`href="${route}"`, `href="${new URL(route, origin).href}"`);
  }
  await writeFile(resolve(output, filename), page);
}

await copyFile(resolve(source, 'wedding-planner.js'), resolve(output, 'wedding-planner.js'));
await copyFile(resolve(source, 'assets/bliss-chapel-hero.jpg'), resolve(output, 'assets/bliss-chapel-hero.jpg'));
// Fail Vercel builds if the public inquiry hero loses safe routes or disclosures.
await import('./task19-public-cta-smoke.mjs');
console.log('Built Vercel workspace portal. Application origin is public configuration, not a credential.');
