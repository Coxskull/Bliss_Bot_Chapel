// Zero-cost local HTTPS provider fixture + PostgreSQL only. Not a live model test.
const { chromium } = require("playwright");
const assert = require("node:assert/strict");
(async () => {
  const base = process.env.BLISS_SMOKE_BASE || "http://127.0.0.1:5084";
  assert(["localhost", "127.0.0.1"].includes(new URL(base).hostname));
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || "/usr/bin/chromium", headless: true });
  try {
    const context = await browser.newContext(), page = await context.newPage(), errors = [];
    page.on("pageerror", error => errors.push(error.message));
    async function api(path, data) {
      const response = data ? await context.request.post(base + path, { data }) : await context.request.get(base + path);
      assert(response.ok(), `${path}: ${response.status()} ${await response.text()}`); return response.json();
    }
    assert.equal((await api("/api/auth/session")).authenticationEnabled, false);
    const advertiser = (await api("/api/advertisers"))[0];
    const workspace = await api("/api/wedding-planner/workspaces", { advertiserId: advertiser.id, sourceSystem: "PHASE4_FIXTURE", idempotencyKey: crypto.randomUUID() });
    const brandPath = `/api/wedding-planner/workspaces/${workspace.workspaceId}/brand-dna`;
    const brands = await api(brandPath);
    const draft = await api(brandPath, { expectedVersion: brands.at(-1)?.version || 0, idempotencyKey: crypto.randomUUID(), fields: {
      brandName: "TEST Phase 4 Pharmacy", niche: "pharmacy", market: "Taipei", language: "English", colors: "Gold", font: "Montserrat", goals: "Local awareness", restrictions: "No medical claims" } });
    const confirmed = await api(brandPath + "/confirm", { expectedVersion: draft.revision.version, idempotencyKey: crypto.randomUUID() });
    await api(`/api/wedding-planner/workspaces/${workspace.workspaceId}/production/briefs`, { brandDnaId: confirmed.revision.id, family: "pharmacy", headline: "Neighborhood care", cta: "Visit", idempotencyKey: crypto.randomUUID() });
    const aiPath = `/api/wedding-planner/workspaces/${workspace.workspaceId}/ai`;
    assert.equal((await api(aiPath + "/readiness")).provider, "LOCAL_HTTPS_FIXTURE", "Never point this test at a real provider");
    await page.goto(base); await page.getByRole("link", { name: "Open AI planning workspace" }).click();
    async function open() {
      await page.waitForFunction(() => document.querySelector("#workspace").options.length > 0);
      await page.locator("#workspace").selectOption(workspace.workspaceId);
      await page.getByRole("button", { name: "Open AI workspace", exact: true }).click();
      await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Workspace loaded"));
    }
    await open(); await page.getByRole("button", { name: "Create planning session", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#session").options.length > 0);
    assert.equal(await page.getByRole("button", { name: "Request AI suggestion", exact: true }).isDisabled(), true);
    await page.locator("#consent").check(); await page.locator("#prompt").fill("Offer one campaign idea");
    await page.getByRole("button", { name: "Request AI suggestion", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#runs").textContent.includes("SUCCEEDED_PENDING_REVIEW"));
    assert.match(await page.locator("#runs").textContent(), /LOCAL TEST FIXTURE/);
    const firstRuns = await api(aiPath + "/runs"); assert.equal(firstRuns.length, 1); assert.equal(firstRuns[0].actualUsd, 0);
    await page.reload(); await open(); await page.locator("#consent").check(); await page.locator("#prompt").fill("Offer one campaign idea");
    await page.getByRole("button", { name: "Request AI suggestion", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Suggestion queued"));
    assert.equal((await api(aiPath + "/runs")).length, 1, "Reloaded identical request must replay");
    await page.getByRole("button", { name: "Request AI image draft", exact: true }).click();
    await page.getByRole("button", { name: "Attach draft for human assessment", exact: true }).waitFor();
    await page.getByRole("button", { name: "Attach draft for human assessment", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#runs").textContent.includes("Attached to Creative Academy"));
    const withImage = await api(aiPath + "/runs"); const image = withImage.find(x => x.kind === "IMAGE"); assert(image.attachedCandidateId);
    await page.locator("#prompt").fill("FIXTURE_WAIT");
    await page.getByRole("button", { name: "Request AI suggestion", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#runs").textContent.includes("RUNNING"));
    await page.getByRole("button", { name: "Cancel run", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#runs").textContent.includes("CANCELLED"));
    const runs = await api(aiPath + "/runs"); assert.equal(runs.length, 3);
    const cancelled = runs.find(x => x.status === "CANCELLED"); assert(cancelled); assert.equal(cancelled.actualUsd, undefined);
    const quota = await api(aiPath + "/readiness"); assert.equal(quota.callsReservedToday, 3); assert.equal(quota.reservedTotalUsd, .03);
    await page.getByRole("button", { name: "Start a fresh AI request", exact: true }).click(); assert.equal(await page.locator("#consent").isChecked(), false);
    await page.setViewportSize({ width: 390, height: 844 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)); assert.deepEqual(errors, []);
    console.log("PASS: local TLS fixture, consent, durable text/image drafts, reload replay, explicit candidate attachment, running cancellation, retained budget and mobile layout. External model calls: 0; cost: $0.");
    await context.close();
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
