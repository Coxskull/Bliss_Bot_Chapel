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
if (process.env.ALPHA_ASK_ALPHA_FRONTEND_ENABLED === 'true') {
  // Explicit release gate. A code merge or Vercel build alone cannot enable AI.
  // Backend metering, approved privacy notice and hosted acceptance are separate gates.
  html = html.replace('data-ask-alpha-ai="false"', 'data-ask-alpha-ai="true"');
  html = html.replace(
    'This is a scripted informational guide, not a live AI model or human support. The chat itself does not collect account data or submit forms.',
    'AI answers require your explicit consent and may contain mistakes. This is not a human support channel, an approved campaign or a binding quote.'
  );
  html = html.replace(
    '12 questions per session. Messages are only shown in this tab and are never submitted or stored.',
    'Up to 12 AI requests per session. With consent, chat context is sent to OpenAI; ALPHA stores limited request and usage receipts. No automatic CRM submission.'
  );
}

await mkdir(resolve(output, 'assets'), { recursive: true });
await writeFile(resolve(output, 'index.html'), html);
await copyFile(resolve(source, 'global-media.css'), resolve(output, 'global-media.css'));

// Public interest forms, policy pages and the creator account explainer may
// be published on Vercel. Authenticated registration, passwords, CSRF and API
// calls stay on Railway so the session and endpoints remain same-origin.
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
  '/creator-registration.html',
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
  ['advertiser-login.html', ['/advertiser-signin-preview.html', '/advertiser-registration.html']]
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
