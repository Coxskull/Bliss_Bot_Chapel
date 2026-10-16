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
for (const route of ['/', '/production.html', '/brand-intake.html']) {
  html = html.replaceAll(`href="${route}"`, `href="${new URL(route, origin).href}"`);
}
html = html.replaceAll('href="/global-media.html"', 'href="/"');
await mkdir(resolve(output, 'assets'), { recursive: true });
await writeFile(resolve(output, 'index.html'), html);
await copyFile(resolve(source, 'global-media.css'), resolve(output, 'global-media.css'));
await copyFile(resolve(source, 'assets/bliss-chapel-hero.jpg'), resolve(output, 'assets/bliss-chapel-hero.jpg'));
console.log('Built Vercel workspace portal. Application origin is public configuration, not a credential.');
