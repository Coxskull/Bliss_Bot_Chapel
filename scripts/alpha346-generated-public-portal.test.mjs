import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile, access} from 'node:fs/promises';

const generated = file => new URL('../.vercel-portal/' + file, import.meta.url);
const load = file => readFile(generated(file), 'utf8');

test('offline Vercel portal build contains the existing consented advertiser inquiry', async () => {
  const advertiser = await load('join-advertiser.html');
  const privacy = await load('alpha-privacy.html');
  const terms = await load('alpha-terms.html');
  assert.match(advertiser, /data-kind="advertiser"/);
  assert.match(advertiser, /processing_acknowledged/);
  assert.match(advertiser, /marketing_opt_in/);
  assert.match(advertiser, /alpha-public-intake/);
  assert.match(advertiser, /href="\/alpha-privacy.html"/);
  assert.match(advertiser, /does not purchase ads/);
  assert.ok(privacy.length > 100);
  assert.ok(terms.length > 100);
});

test('generated services explain exactly five planning-only outputs and no checkout', async () => {
  const services = await load('services.html');
  const start = services.indexOf('<h2>Campaign Planning &amp; Coordination');
  const end = services.indexOf('<h2>How a commercial engagement', start);
  assert.ok(start >= 0 && end > start);
  const offer = services.slice(start, end);
  assert.equal((offer.match(/<li>/g) || []).length, 5);
  assert.ok(offer.includes('href="/join-advertiser.html?service=planning"'));
  assert.match(offer, /creates no order or payment obligation/);
  assert.match(services, /No online checkout is enabled/);
});

test('generated contact provides the selected inquiry mailbox without claiming paid support', async () => {
  const contact = await load('contact.html');
  assert.match(contact, /mailto:dejesuserwin1@gmail.com/);
  assert.match(contact, /paid-customer support service and response time are not yet verified/);
  assert.match(contact, /not currently available/);
  assert.match(contact, /href="\/privacy-request.html"/);
});

test('public advertiser login does not expose a missing registration route before hosted release', async () => {
  const login = await load('advertiser-login.html');
  assert.match(login, /Account registration not yet available/);
  assert.match(login, /aria-disabled="true"/);
  assert.ok(!login.includes('href="/advertiser-registration.html"'));
  assert.ok(login.includes('href="https://')); // Private sign-in remains on Railway.
});

test('generated portal does not publish internal proposal tools or restricted operator routes', async () => {
  const index = await load('index.html');
  assert.ok(index.length > 100);
  for (const privatePath of ['alpha346-proposal-desk.html', 'tools/commercial/alpha346-proposal-desk.html', 'internal/admin/index.html', 'creator-dashboard.html']) {
    await assert.rejects(access(generated(privatePath)), { code: 'ENOENT' });
  }
});


test('consolidated advertiser page agrees with the five planning outputs, not the conflicting four-output package', async () => {
  const advertiser = await load('join-advertiser.html');
  const services = await load('services.html');
  for (const item of ['discovery brief', 'creator/channel suitability', 'creative direction', 'coordination schedule', 'final planning summary']) {
    assert.ok(advertiser.includes(item), 'advertiser explanation missing ' + item);
  }
  assert.match(advertiser, /revision count, price, currency and delivery dates require separate written approval/);
  assert.doesNotMatch(advertiser, /up to three content formats|one in-scope revision/);
  assert.match(services, /five proposed deliverables/);
});

test('consolidated homepage directly exposes consented public forms with accessible actions', async () => {
  const homepage = await load('index.html');
  const css = await load('global-media.css');
  assert.match(homepage, /class="hero-actions" role="group"/);
  assert.match(homepage, /href="\/join-advertiser.html"/);
  assert.match(homepage, /href="\/join-creator.html"/);
  assert.match(homepage, /href="#ask-alpha"/);
  assert.match(css, /\.hero-actions a:focus-visible/);
  assert.match(css, /min-height:\s*48px/);
  assert.doesNotMatch(homepage, /payment checkout is now available|book your creator now/i);
});
