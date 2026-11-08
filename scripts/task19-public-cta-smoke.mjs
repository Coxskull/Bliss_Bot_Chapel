import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { resolve, dirname } from "node:path";

// Task #19 — actual generated-public-portal assertions; no POST, secrets or paid providers.
const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const read = path => readFileSync(resolve(root, path), "utf8");
const pagePath = ".vercel-portal/index.html";
assert.ok(existsSync(resolve(root, pagePath)), "generated public homepage must exist");
const page = read(pagePath);
const source = read("frontend/public/global-media.html");
const css = read(".vercel-portal/global-media.css");
const fail = message => { throw new Error("TASK19 PUBLIC CTA: " + message); };
const first = page.indexOf('<div class="hero-actions"');
const last = first < 0 ? -1 : page.indexOf("</div>", first);
if (first < 0 || last < 0) fail("Missing public hero call-to-action group");
const hero = page.slice(first, last + 6);
for (const [route, label] of [
  ["/join-advertiser.html", "Advertiser inquiry"],
  ["/join-creator.html", "Creator inquiry"],
  ["#ask-alpha", "Ask Alpha guided help"]
]) {
  assert.ok(hero.includes('href="' + route + '"'), label + " is directly reachable from homepage hero");
}
for (const [route, kind] of [
  ["join-advertiser.html", "advertiser"],
  ["join-creator.html", "creator"]
]) {
  assert.ok(existsSync(resolve(root, ".vercel-portal", route)), route + " exists in PUBLIC output");
  const html = read(".vercel-portal/" + route);
  assert.ok(html.includes('data-kind="' + kind + '"'), route + " has correct inquiry kind");
  assert.ok(html.includes('name="processing_acknowledged" required'), route + " requires privacy processing acknowledgement");
  assert.ok(html.includes('name="adult_confirmed" required'), route + " requires adult confirmation");
  assert.ok(html.includes('name="marketing_opt_in"'), route + " keeps marketing consent optional");
}
for (const route of ["/alpha-privacy.html", "/alpha-terms.html"]) {
  assert.ok(page.includes('href="' + route + '"'), route + " linked from homepage");
  assert.ok(existsSync(resolve(root, ".vercel-portal", route.slice(1))), route + " published");
}
assert.ok(source.includes('href="#main-content"') && page.includes('<main id="main-content">'), "functional keyboard skip link");
assert.ok(page.includes('<meta name="viewport"'), "mobile viewport provided");
assert.match(hero, /role="group" aria-label="Choose a public next step"/, "CTA grouping must be announced");
assert.ok(page.includes("not create an account or purchase a campaign"), "interest-only scope is clear");
assert.ok(page.includes("not a live AI model"), "Ask Alpha remains transparently scripted");
assert.match(css, /\.hero-actions a:focus-visible\s*\{/, "visible keyboard focus style exists");
assert.match(css, /\.hero-actions a\s*\{[^}]*min-height:\s*48px/, "tap targets >= 48px");
assert.match(css, /@media\s*\(max-width:\s*600px\)[\s\S]*?\.hero-actions a\s*\{[^}]*width:\s*100%/, "mobile CTA stacking exists");
assert.ok(!existsSync(resolve(root, ".vercel-portal/creator-dashboard.html")), "protected creator dashboard never copied");
assert.ok(!existsSync(resolve(root, ".vercel-portal/advertiser-dashboard.html")), "protected advertiser dashboard never copied");
assert.ok(!hero.includes("https://"), "public inquiry hero must use same-origin URLs");
console.log("TASK19_PUBLIC_CTA_STATIC_BUILD_PASS — both forms, consent, mobile, accessibility and restricted routing validated");
