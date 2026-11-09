import test from "node:test";
import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import { resolve, relative, extname } from "node:path";

// ALPHA-346 must remain an inquiry-only website until a *separate*
// merchant, tax, contract, support and production release authorization.
// These checks inspect the actual generated public build, not just sources.
// No Stripe connection, provider calls, customer data or checkout creation.
const publicRoot = resolve(".vercel-portal");
const inspectExtensions = new Set([".html", ".js", ".mjs", ".json", ".css"]);
const forbidden = [
  ["Stripe hosted checkout or payment link", /(?:https?:)?\/\/(?:buy|checkout|billing)\.stripe\.com\//i],
  ["Stripe JS checkout bootstrap", /(?:https?:)?\/\/js\.stripe\.com\/v3\/?/i],
  ["Embedded checkout redirect", /\bredirectToCheckout\s*\(/i],
  ["Checkout session or Payment Link reference", /\b(?:plink|cs_(?:test|live))_[A-Za-z0-9]{8,}\b/i],
  ["Unapproved fixed $149 public checkout price", /(?:\$\s*149(?:\.00)?\b|\bUSD\s+149(?:\.00)?\b)/i]
];
function violations(source) {
  // HTML and built JS sometimes escape forward slashes in embedded JSON.
  const normalized = source.replaceAll("\\/", "/").replaceAll(/&#(?:x2f|47);/gi, "/");
  return forbidden.filter(([, pattern]) => pattern.test(normalized)).map(([label]) => label);
}
async function collect(root, current=root, paths=[]) {
  for (const entry of await readdir(current, {withFileTypes:true})) {
    const full = resolve(current,entry.name);
    if (entry.isDirectory()) await collect(root,full,paths);
    else if (entry.isFile() && inspectExtensions.has(extname(entry.name).toLowerCase()))
      paths.push(full);
  }
  return paths;
}
test("payment-surface denial recognizes synthetic hosted and embedded checkout attempts", () => {
  for (const injected of [
    '<a href="https://buy.stripe.com/test_00wFake">Pay now</a>',
    '<a href="https:\\/\\/buy.stripe.com\\/test_synthetic">Buy</a>',
    '<a href="https://checkout.stripe.com/c/pay/synthetic">Checkout</a>',
    '<script src="https://js.stripe.com/v3/"></script>',
    'stripe.redirectToCheckout({sessionId: "fake"})',
    'const checkoutLink = "plink_SyntheticReference123";',
    '<p>Only $149.00 today</p>',
    '<p>USD 149.00 package</p>'
  ]) assert.ok(violations(injected).length > 0, "failed to detect synthetic fixture: " + injected);
  for (const legitimate of [
    'There is no online checkout; please request a written proposal.',
    'Any amount and currency require customer-specific written approval.',
    'Stripe eligibility is not yet verified; no payment is requested.',
    '<a href="/join-advertiser.html">Request a proposal</a>'
  ]) assert.deepEqual(violations(legitimate), [], "false positive in inquiry-only copy");
});
test("actual generated website does not expose unapproved payment links or price", async () => {
  const paths = await collect(publicRoot);
  assert.ok(paths.length >= 8,"generated Vercel portal missing; run the builder first");
  const required = ["index.html","services.html","contact.html","join-advertiser.html","join-creator.html"];
  for (const filename of required) assert.ok(paths.some(p=>relative(publicRoot,p)===filename),filename+" missing");
  for (const path of paths) {
    const source=await readFile(path,"utf8");
    assert.deepEqual(violations(source),[],relative(publicRoot,path)+" exposed an unapproved payment surface");
  }
  const services=await readFile(resolve(publicRoot,"services.html"),"utf8");
  assert.match(services,/No online checkout is enabled on this public site/i);
  assert.match(services,/written approval/i);
  assert.match(services,/before any payment/i);
});
