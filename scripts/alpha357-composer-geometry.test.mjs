import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import vm from "node:vm";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const originalComposer = resolve(root, "assets/alpha-prototypes/advertising-real-estate/ALPHA-Composer-Quality-357-Offline.html");
const source = readFileSync(originalComposer, "utf8");
const scripts = [...source.matchAll(/<script(?:\s+[^>]*)?>([\s\S]*?)<\/script>/gi)].map(match => match[1]);
const adapter = scripts.find(block => block.includes("window.AlphaAdvertisingRealEstate=Object.freeze"));
assert.ok(adapter, "Existing Composer adapter must be embedded in the source");
const window = {};
vm.runInNewContext(adapter, { window }, { timeout: 2000 });
const engine = window.AlphaAdvertisingRealEstate;
assert.ok(engine.validate(), "Fail-closed source geometry validation");
const W = 1920, H = 1080;

const expected = {
  2: [
    ["BOTTOM_BLUE", 0, 810, 960, 270],
    ["BOTTOM_ORANGE", 960, 810, 960, 270]
  ],
  4: [
    ["TOP_BLUE", 0, 0, 960, 180],
    ["TOP_ORANGE", 960, 0, 960, 180],
    ["BOTTOM_GREEN", 0, 900, 960, 180],
    ["BOTTOM_PURPLE", 960, 900, 960, 180]
  ],
  6: [
    ["TOP_BLUE", 0, 0, 960, 180],
    ["TOP_ORANGE", 960, 0, 960, 180],
    ["LEFT_PURPLE", 0, 180, 270, 720],
    ["RIGHT_GREEN", 1650, 180, 270, 720],
    ["BOTTOM_BLUE", 0, 900, 960, 180],
    ["BOTTOM_PURPLE", 960, 900, 960, 180]
  ]
};

for (const [label, rectangles] of Object.entries(expected)) {
  test(`${label} ads partition the exact 1920x1080 canvas with no gaps, overlaps or creator encroachment`, () => {
    const item = engine.inventory[label];
    assert.equal(item.canvasWidth, W);
    assert.equal(item.canvasHeight, H);
    assert.equal(item.advertiserCapacity, Number(label));
    assert.equal(item.geometryVersion, "ALPHA-ARE-COMPOSER-QUALITY-357-V2");
    assert.deepEqual(
      Array.from(item.slots, s => [s.id, s.x, s.y, s.width, s.height]),
      rectangles);
    const mask = new Uint8Array(W * H);
    const mark = (x,y,w,h,value) => {
      assert.ok(x >= 0 && y >= 0 && w > 0 && h > 0 && x+w <= W && y+h <= H);
      for (let row=y; row<y+h; row++) {
        const start=row*W+x;
        for (let col=0; col<w; col++) mask[start+col] += value;
      }
    };
    for (const r of rectangles) mark(r[1],r[2],r[3],r[4],1);
    const p=item.protectedCreatorArea;
    mark(p.x,p.y,p.width,p.height,2);
    let gaps=0, overlaps=0, creatorIntrusion=0;
    for (const value of mask) {
      if (value===0) gaps++;
      if (value>2) overlaps++;
      if (value===3) creatorIntrusion++;
    }
    assert.equal(gaps, 0);
    assert.equal(overlaps, 0);
    assert.equal(creatorIntrusion, 0);
    assert.equal(new Set(rectangles.map(r => r[0])).size, rectangles.length);
  });

  test(`${label} ads have continuous equal-purpose bands and exact boundaries`, () => {
    const slots = engine.inventory[label].slots;
    const rows = ["TOP_", "BOTTOM_"];
    for (const prefix of rows) {
      const band = slots.filter(s => s.id.startsWith(prefix));
      if (!band.length) continue;
      assert.equal(band.length, 2);
      assert.equal(band[0].x, 0);
      assert.equal(band[0].width, W/2);
      assert.equal(band[0].x+band[0].width, band[1].x);
      assert.equal(band[1].x+band[1].width, W);
      assert.equal(band[0].y, band[1].y);
      assert.equal(band[0].height, band[1].height);
    }
  });
}

test("6 ads fill both side strips from top advertising band to bottom band", () => {
  const slots = engine.inventory[6].slots;
  const top = slots.find(s=>s.id.startsWith("TOP_"));
  const bottom = slots.find(s=>s.id.startsWith("BOTTOM_"));
  const left = slots.find(s=>s.id.startsWith("LEFT_"));
  const right = slots.find(s=>s.id.startsWith("RIGHT_"));
  assert.equal(left.y, top.y+top.height);
  assert.equal(right.y, top.y+top.height);
  assert.equal(left.y+left.height, bottom.y);
  assert.equal(right.y+right.height, bottom.y);
  assert.equal(left.height, right.height);
});

test("existing Composer uses aspect-preserving artwork, bounded text and one uniform divider pass", () => {
  assert.match(adapter, /const fit = Math\.min\(w \/ img\.width, h \/ img\.height\)/);
  assert.doesNotMatch(adapter, /drawImage\(img,\s*0,\s*0,\s*img\.width,\s*img\.height,\s*x,\s*y,\s*w,\s*h\)/);
  assert.match(adapter, /ctx\.measureText\(text\)\.width <= availableWidth/);
  assert.match(adapter, /fitLabel\(headline, 650/);
  assert.match(adapter, /const divider = 4/);
  assert.doesNotMatch(adapter, /ctx\.strokeRect\(/);
  assert.equal((source.match(/<canvas\s+id="stage"/g)||[]).length, 1);
  assert.match(source, /FICTIONAL OFFLINE ENGINEERING DEMO/);
  assert.doesNotMatch(source, /<script[^>]+src="https?:/);
  assert.doesNotMatch(source, /<iframe/);
  assert.doesNotMatch(source, /fetch\(/);
  assert.doesNotMatch(source, /XMLHttpRequest/);
});
