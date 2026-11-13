import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

// This is an OFFLINE, no-network proof that public navigation points only to
// published, reviewed pages. No inquiry submission, outreach or payment.
const out = resolve(".vercel-portal");
const pages = [
  "index.html", "services.html", "contact.html",
  "alpha-privacy.html", "alpha-terms.html",
  "join-creator.html", "join-advertiser.html", "privacy-request.html",
  "creator-login.html", "advertiser-login.html", "creator-account.html"
];
const read = file => readFileSync(resolve(out, file), "utf8");
for (const file of pages) {
  assert.ok(existsSync(resolve(out, file)), "Missing public page: " + file);
  const html = read(file);
  assert.ok(html.includes("ALPHA GLOBAL MEDIA"), "Business identity absent in " + file);
  assert.ok(html.includes('href="/contact.html"'), "Public contact route absent from " + file);
  const linked = [...html.matchAll(/\bhref="([^"]+)"/g)].map(m => m[1]);
  for (const href of linked) {
    if (href.startsWith("#")) {
      const id = href.slice(1);
      if (id) assert.ok(html.includes('id="' + id + '"'), file + " broken fragment " + href);
      continue;
    }
    if (!href.startsWith("/") || href.startsWith("//")) continue;
    const dest = href.split(/[?#]/, 1)[0];
    if (dest === "/") continue;
    const clean = dest.slice(1);
    assert.ok(clean && !clean.includes(".."), "Unsafe relative path " + href);
    assert.ok(existsSync(resolve(out, clean)), file + " links to missing " + dest);
  }
}
for (const file of ["creator-registration.html", "creator-registration.js", "advertiser-registration.html", "advertiser-registration.js"])
  assert.ok(!existsSync(resolve(out, file)), file + " must stay on the Railway application origin");
const contact = read("contact.html");
for (const route of ["join-advertiser.html", "join-creator.html", "privacy-request.html"])
  assert.ok(contact.includes('href="/' + route + '"'), "Contact inquiry route unavailable");
const services = read("services.html");
assert.ok(services.includes("No online checkout is enabled"), "Do not claim live payments");
assert.ok(services.includes("transaction currency"), "Order-specific currency disclosure");
const interestTerms = read("alpha-terms.html");
assert.ok(interestTerms.includes("These terms govern only public interest inquiries"), "Distinguish paid-service terms");
for (const unpublished of [
  "commercial-terms.html", "refund-cancellation.html",
  "CAPTAIN-APPROVAL-PACKET-20261106.md",
  "refund-cancellation-policy-CAPTAIN-REVIEW.md",
  "commercial-terms-CAPTAIN-REVIEW.md"
]) {
  assert.ok(!existsSync(resolve(out, unpublished)), unpublished + " MUST remain nonpublic");
  for (const file of pages) {
    assert.ok(!read(file).includes('href="/' + unpublished + '"'), "Public page incorrectly links to unapproved policy");
  }
}
for (const file of pages) {
  const html = read(file);
  assert.ok(!html.includes("[CAPTAIN"), "Unapproved placeholders in " + file);
  assert.ok(!html.includes("DRAFT — OWNER APPROVAL"), "Owner-review draft exposed in " + file);
}
console.log("STRIPE_PUBLIC_NAVIGATION_AND_UNAPPROVED_POLICY_FIREBREAK_PASS: " + pages.length + " pages inspected, zero network submissions.");
