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
// Owner-authorized fictional, browser-only advertising trial. Never expose protected
// Creative Academy, registered accounts, campaigns, payment, or live creator inventory.
if (!html.includes('</main>') || html.includes('id="alpha-demo-heading"')) {
  throw new Error('Public demo placement missing or duplicate');
}
html = html.replace('</main>', '<section class="journey" aria-labelledby="alpha-demo-heading"><div><p class="eyebrow">FICTIONAL ADVERTISING DEMONSTRATION</p><h2 id="alpha-demo-heading">Try our 2, 4 and 6-space creative layout.</h2><p>Preview original fictional advertisement frames in your browser. This is not a creator booking, paid campaign, or available placement.</p></div><div><p>Generate an original PNG draft or a short fictional WebM illustration. Zero AI credits and no account required; nothing is uploaded or delivered.</p><p><a href="/real-estate-showcase.html">Open the free fictional ad composer</a></p></div></section>\n</main>');


if (process.env.ALPHA_ASK_ALPHA_FRONTEND_ENABLED === 'true') {
  // Website release switch: NOT sufficient for paid API activation.
  // The existing Supabase gateway independently checks consent, network
  // provenance, provider configuration and a fail-closed $9 SQL governor.
  if (!html.includes('data-ask-alpha-ai="false"') ||
      !html.includes('id="ask-alpha-ai-consent-panel" hidden'))
    throw Error('Ask Alpha AI consent/release controls missing; refuse public build');
  html = html.replace('data-ask-alpha-ai="false"', 'data-ask-alpha-ai="true"');
  html = html.replace(
    'This is a scripted informational guide, not a live AI model or human support. The chat itself does not collect account data or submit forms.',
    'Optional AI answers are available with your explicit processing consent. AI can make mistakes and is not a human representative, approved campaign or binding quote.'
  );
  html = html.replace(
    '12 questions per session. Messages are only shown in this tab and are never submitted or stored.',
    'Up to 12 questions per session. When AI is explicitly selected, chat context is sent to OpenAI and ALPHA stores limited consent/usage receipts; the guided mode stays local.'
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
  'alpha-privacy.html', 'alpha-terms.html', 'services.html', 'contact.html', 'support.html',
  'support-operator.html', 'support-operator.css', 'support-operator.js',
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
  if (filename === 'advertiser-login.html') {
    // Account creation and auth cookies must remain on the application origin.
    // Until a separately verified hosted-registration release, do not send
    // Vercel visitors to an unbuilt registration page (a misleading 404).
    const link = '<a class="submit secondary-action" href="/advertiser-registration.html">Register advertiser account</a>';
    if (!page.includes(link)) throw new Error('Advertiser registration entry changed; fail closed');
    if (process.env.ALPHA_ADVERTISER_REGISTRATION_PUBLIC_ENABLED === 'true') {
      page = page.replace(link, link.replace(
        'href="/advertiser-registration.html"',
        `href="${new URL('/advertiser-registration.html', origin).href}"`
      ));
    } else {
      page = page.replace(link,
        '<span class="submit secondary-action" aria-disabled="true">Account registration not yet available</span>');
    }
  }
  await writeFile(resolve(output, filename), page);
}

// Public-only files: deterministic vectors and non-sensitive browser-side rendering.
for (const filename of [
  'ad-composer.js', 'real-estate-showcase.js',
  'real-estate-showcase-page.js', 'production.css'
]) await copyFile(resolve(source, filename), resolve(output, filename));
let demoHtml = await readFile(resolve(source, 'real-estate-showcase.html'), 'utf8');
demoHtml = demoHtml.replace('href="/production.html"', 'href="/"');
await writeFile(resolve(output, 'real-estate-showcase.html'), demoHtml);

await copyFile(resolve(source, 'wedding-planner.js'), resolve(output, 'wedding-planner.js'));
await copyFile(resolve(source, 'assets/bliss-chapel-hero.jpg'), resolve(output, 'assets/bliss-chapel-hero.jpg'));
// Fail Vercel builds if the public inquiry hero loses safe routes or disclosures.
await import('./task19-public-cta-smoke.mjs');
await import('./task21-public-manual-support-smoke.mjs');
await import('./task21-production-operator-browser-safety.mjs');
await import('./alpha-public-demo-safety.test.mjs');
console.log('Built Vercel workspace portal. Application origin is public configuration, not a credential.');
