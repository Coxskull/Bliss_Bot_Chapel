// Local fictional data only. Requires Playwright on NODE_PATH and Chromium.
const { chromium } = require("playwright");
const assert = require("node:assert/strict");
const pixel = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", "base64");
(async () => {
  const base = process.env.BLISS_SMOKE_BASE || "http://127.0.0.1:5083";
  assert(["127.0.0.1", "localhost"].includes(new URL(base).hostname));
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || "/usr/bin/chromium", headless: true });
  try {
    const context = await browser.newContext(), page = await context.newPage(), errors = [];
    page.on("pageerror", error => errors.push(error.message));
    async function api(path, data) {
      const result = data ? await context.request.post(base + path, { data }) : await context.request.get(base + path);
      assert(result.ok(), `${path}: ${result.status()} ${await result.text()}`); return result.json();
    }
    assert.equal((await api("/api/auth/session")).authenticationEnabled, false);
    const advertiser = (await api("/api/advertisers"))[0];
    const workspace = await api("/api/wedding-planner/workspaces", { advertiserId: advertiser.id, sourceSystem: "PHASE3_SMOKE", idempotencyKey: crypto.randomUUID() });
    const brandPath = `/api/wedding-planner/workspaces/${workspace.workspaceId}/brand-dna`;
    const versions = await api(brandPath), version = versions.at(-1)?.version || 0;
    const draft = await api(brandPath, { expectedVersion: version, idempotencyKey: crypto.randomUUID(), fields: {
      brandName: "TEST Phase 3 Pharmacy", niche: "pharmacy", market: "Taipei, Taiwan", language: "English", colors: "Gold", font: "Montserrat", goals: "Neighborhood awareness", restrictions: "No medical claims" } });
    await api(brandPath + "/confirm", { expectedVersion: draft.revision.version, idempotencyKey: crypto.randomUUID() });
    await page.goto(base); await page.getByRole("link", { name: "Open Creative Academy workspace" }).click();
    async function open(target) {
      await target.waitForFunction(() => document.querySelector("#workspace").options.length > 0);
      await target.locator("#workspace").selectOption(workspace.workspaceId);
      await target.getByRole("button", { name: "Open production workspace", exact: true }).click();
      await target.waitForFunction(() => !document.querySelector("#brief-fields").disabled);
    }
    await open(page);
    const headline = `TEST Phase 3 brief ${Date.now()}`;
    await page.locator("#family").selectOption("pharmacy");
    await page.locator("#headline").fill(headline); await page.locator("#cta").fill("Visit our store");
    await page.getByRole("button", { name: "Save production brief", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Production brief saved"));
    const briefId = await page.locator("#brief").inputValue();
    // The saved brand brief can produce original PNG art with no provider call.
    // No candidate is persisted until a separate human submits the existing form.
    await page.locator("#composer-format").selectOption("landscape");
    await page.getByRole("button", { name: "Render original PNG from saved brief" }).click();
    await page.waitForFunction(() => document.querySelector("#composer-status").textContent.includes("Original PNG rendered locally"));
    const firstArtwork = await page.locator("#composer-preview").evaluate(async image => {
      await image.decode(); return { width: image.naturalWidth, height: image.naturalHeight };
    });
    assert.deepEqual(firstArtwork, { width: 1280, height: 720 });
    const prepared = await page.locator("#image").evaluate(input =>
      ({ size: input.files[0]?.size, type: input.files[0]?.type, name: input.files[0]?.name }));
    assert(prepared.size > 0 && prepared.size <= 1024 * 1024);
    assert.equal(prepared.type, "image/png");
    assert.match(prepared.name, /alpha-original-landscape/);
    assert.equal((await api(`/api/wedding-planner/workspaces/${workspace.workspaceId}/production/briefs/${briefId}`)).candidates.length, 0);
    await page.locator("#composer-format").selectOption("square");
    await page.getByRole("button", { name: "Render original PNG from saved brief" }).click();
    await page.waitForFunction(() => document.querySelector("#composer-status").textContent.includes("Original PNG rendered locally"));
    const secondArtwork = await page.locator("#composer-preview").evaluate(async image => {
      await image.decode(); return { width: image.naturalWidth, height: image.naturalHeight };
    });
    assert.deepEqual(secondArtwork, { width: 1080, height: 1080 });
    console.log("ZERO_CREDIT_AD_COMPOSER_PASS: original 1280x720 and 1080x1080 PNGs rendered from confirmed saved brief; no automatic candidate submission.");

    assert.match(await page.locator("#brief-details").textContent(), /Gold/);
    assert.match(await page.locator("#brief-details").textContent(), /Reference SHA-256/);
    async function candidate(target, label) {
      await target.locator("#candidate-label").fill(label);
      await target.locator("#notes").fill("<script>alert('test')</script>");
      await target.locator("#image").setInputFiles({ name: "candidate.png", mimeType: "image/png", buffer: pixel });
      await target.getByRole("button", { name: "Save candidate revision", exact: true }).click();
    }
    await candidate(page, "First candidate");
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Candidate revision saved"));
    assert.equal(await page.locator("#history script").count(), 0);
    await page.locator("#history img").first().scrollIntoViewIfNeeded();
    await page.locator("#history img").first().evaluate(image => image.decode());
    for (const gate of ["customization", "qualityParity", "originality", "geographicAuthenticity"]) await page.locator(`[name="${gate}"]`).selectOption(gate === "qualityParity" ? "FAIL" : "PASS");
    await page.locator("#decision").selectOption("PASS"); await page.locator("#rationale").fill("Test image is a pixel, not a finished advertisement.");
    await page.getByRole("button", { name: "Record assessment", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").dataset.error === "true");
    assert.match(await page.locator("#status").textContent(), /PASS requires/);
    await page.locator("#decision").selectOption("REVISE"); await page.locator("#repair").fill("Replace this test pixel with a real independently produced candidate.");
    await page.getByRole("button", { name: "Record assessment", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Human assessment saved"));
    const other = await context.newPage(); await other.goto(base + "/production.html"); await open(other);
    await other.locator("#brief").selectOption(briefId); await other.getByRole("button", { name: "Resume brief", exact: true }).click();
    await other.waitForFunction(() => !document.querySelector("#candidate-fields").disabled);
    await candidate(page, "Revised candidate");
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Candidate revision saved"));
    await candidate(other, "Stale candidate");
    await other.waitForFunction(() => document.querySelector("#status").dataset.error === "true");
    assert.match(await other.locator("#status").textContent(), /changed/);
    assert.equal(await other.locator("#candidate-label").inputValue(), "Stale candidate");
    await other.getByRole("button", { name: "Refresh saved history", exact: true }).click();
    await other.waitForFunction(() => document.querySelectorAll("#history article").length === 2 && !document.querySelector("#candidate-fields").disabled);
    assert.equal(await other.locator("#history .review").count(), 1);
    await page.reload(); await open(page); await page.locator("#brief").selectOption(briefId); await page.getByRole("button", { name: "Resume brief", exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll("#history article").length === 2);
    assert.equal(await page.locator("#history .review").count(), 1);
    await page.setViewportSize({ width: 390, height: 844 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    assert.deepEqual(errors, []);
    console.log("PASS: confirmed DNA brief, recipe/provenance, private PNG, review gates, REVISE history, candidate revisions, stale-tab conflict, resume, safe text and mobile layout.");
    await context.close();
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
