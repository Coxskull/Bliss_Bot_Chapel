const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

const source = fs.readFileSync(path.join(__dirname, '../frontend/public/preview-lock.js'), 'utf8');

function element(attributes = {}) {
  return {
    attributes: { ...attributes },
    style: {},
    disabled: false,
    textContent: '',
    getAttribute(name) { return this.attributes[name] ?? null; },
    setAttribute(name, value) { this.attributes[name] = String(value); },
    removeAttribute(name) { delete this.attributes[name]; }
  };
}

async function run(json, ok = true) {
  const links = [
    element({ href: '#about' }), element({ href: '/' }),
    element({ href: '/source-media/index.html' }),
    element({ href: '/ai-workspace.html' }),
    element({ href: 'https://outside.example/action' })
  ];
  const menu = element();
  const controls = [element(), element(), element()];
  const online = element();
  const banners = [];
  const document = {
    body: { prepend(node) { banners.push(node); } },
    createElement() { return element(); },
    querySelector(sel) { return sel === '.planner-profile .online' ? online : null; },
    querySelectorAll(sel) {
      if (sel === 'a[href]') return links;
      if (sel.includes('.suggestions button')) return controls;
      if (sel === '.menu-button') return [menu];
      return [];
    }
  };
  const window = { location: { href: 'https://preview.example/', origin: 'https://preview.example' } };
  const fetch = async () => ({ ok, json: async () => json });
  vm.runInNewContext(source, { fetch, document, window, URL });
  await new Promise(resolve => setImmediate(resolve));
  return { links, controls, menu, online, banners };
}

test('restricted mode banners and disables blocked links and business controls', async () => {
  const r = await run({ restrictedPreview: true, operational: false });
  assert.equal(r.banners.length, 1);
  assert.match(r.banners[0].textContent, /INTERFACE PREVIEW/);
  assert.equal(r.online.textContent, 'Preview only · Offline');
  assert.equal(r.links[0].getAttribute('href'), '#about');
  assert.equal(r.links[1].getAttribute('href'), '/');
  for (const link of r.links.slice(2)) {
    assert.equal(link.getAttribute('href'), null);
    assert.equal(link.getAttribute('aria-disabled'), 'true');
  }
  assert.ok(r.controls.every(c => c.disabled));
  assert.equal(r.menu.disabled, false);
});

test('operational and unmarked hosts are not modified', async () => {
  for (const status of [
    { restrictedPreview: false, operational: true },
    { restrictedPreview: true, operational: true },
    { restrictedPreview: false, operational: false }
  ]) {
    const r = await run(status);
    assert.equal(r.banners.length, 0);
    assert.equal(r.links[2].getAttribute('href'), '/source-media/index.html');
    assert.equal(r.controls[0].disabled, false);
  }
});

test('failed status response leaves ordinary site unchanged', async () => {
  const r = await run(null, false);
  assert.equal(r.banners.length, 0);
  assert.equal(r.links[3].getAttribute('href'), '/ai-workspace.html');
});
