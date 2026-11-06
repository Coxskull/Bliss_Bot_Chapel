'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const script = fs.readFileSync(
  path.join(__dirname, '../frontend/public/wedding-planner.js'),
  'utf8'
);

function runPrompt(prompt) {
  const listeners = new Map();
  const messages = [];
  const elements = new Map();

  const makeNode = tag => ({
    tag,
    children: [],
    className: '',
    textContent: '',
    value: '',
    disabled: false,
    dataset: {},
    append(...nodes) { this.children.push(...nodes); },
    addEventListener(type, handler) { listeners.set(this.tag + ':' + type, handler); },
    setAttribute() {},
    classList: { toggle() { return false; }, remove() {} }
  });

  const input = makeNode('input');
  const send = makeNode('button');
  const thread = makeNode('thread');
  const menu = makeNode('menu');
  const nav = makeNode('nav');
  elements.set('#ask-alpha-input', input);
  elements.set('#ask-alpha-send', send);
  elements.set('#ask-alpha-thread', thread);
  const suggestion = makeNode('suggestion');
  suggestion.dataset.askAlphaPrompt = prompt;

  const document = {
    querySelector(selector) {
      if (selector === '.menu-button') return menu;
      if (selector === '.site-header nav') return nav;
      return elements.get(selector) || null;
    },
    querySelectorAll(selector) {
      if (selector === '.site-header nav a') return [];
      if (selector === '[data-ask-alpha-prompt]') return [suggestion];
      return [];
    },
    createElement: makeNode
  };

  let fetchCalls = 0;
  vm.runInNewContext(script, {
    document,
    fetch() { fetchCalls++; throw new Error('Ask Alpha must not make network calls'); }
  });
  listeners.get('button:click')();
  assert.equal(fetchCalls, 0);
  const guide = thread.children.find(node => node.className.includes('ask-alpha-guide') && node.children.some(child => child.tag === 'a'));
  assert.ok(guide, 'guided answer should include a direct inquiry action');
  return {
    text: guide.children.find(child => child.tag === 'span').textContent,
    actions: guide.children.filter(child => child.tag === 'a').map(link => ({ label: link.textContent, href: link.href }))
  };
}

test('advertiser and creator guidance routes to the matching public form', () => {
  const advertiser = runPrompt('How does advertising with Alpha work?');
  assert.ok(advertiser.actions.some(action => action.href === '/join-advertiser.html'));
  const creator = runPrompt('How do creators join Alpha?');
  assert.ok(creator.actions.some(action => action.href === '/join-creator.html'));
});

test('pricing guidance acknowledges open inquiries and routes to advertiser intake', () => {
  const result = runPrompt('How much does an ad cost?');
  assert.match(result.text, /form is available now/i);
  assert.match(result.text, /no public checkout/i);
  assert.ok(result.actions.some(action => action.href === '/join-advertiser.html'));
});

test('human-help guidance routes to contact options and both inquiry forms', () => {
  const result = runPrompt('Can I speak to a human?');
  assert.doesNotMatch(result.text, /once the commercial intake (?:opens|is activated)/i);
  assert.deepEqual(result.actions.map(action => action.href), [
    '/contact.html',
    '/join-advertiser.html',
    '/join-creator.html'
  ]);
});

test('privacy guidance offers the notice and privacy request route', () => {
  const result = runPrompt('What happens to my chat message?');
  assert.ok(result.actions.some(action => action.href === '/alpha-privacy.html'));
  assert.ok(result.actions.some(action => action.href === '/privacy-request.html'));
});

test('inquiry actions have mobile-sized touch targets and keyboard focus styling', () => {
  const css = fs.readFileSync(path.join(__dirname, '../frontend/public/global-media.css'), 'utf8');
  assert.match(css, /\.ask-alpha-action[^}]*min-height:\s*44px/);
  assert.match(css, /a:focus-visible\{[^}]*outline:3px/);
});
