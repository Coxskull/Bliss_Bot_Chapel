// Run against the disposable Phase 6 database after the Phase 5 handoff fixture.
const { chromium } = require("playwright");
const assert = require("node:assert/strict");
(async () => {
  const base = process.env.BLISS_SMOKE_BASE || "http://127.0.0.1:5088";
  assert(["127.0.0.1", "localhost"].includes(new URL(base).hostname));
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || "/usr/bin/chromium", headless: true });
  try {
    const context = await browser.newContext(), page = await context.newPage(), errors = [];
    page.on("pageerror", error => errors.push(error.message));
    async function api(path) {
      const response = await context.request.get(base + path);
      assert(response.ok(), `${path}: ${response.status()} ${await response.text()}`); return response.json();
    }
    assert.equal((await api("/api/auth/session")).authenticationEnabled, false);
    const workspace = (await api("/api/wedding-planner/workspaces")).find(x => x.advertiserId === "33333333-3333-3333-3333-333333333334");
    assert(workspace);
    const root = `/api/wedding-planner/workspaces/${workspace.workspaceId}`;
    const handoffs = await api(root + "/handoff/plans"); assert.equal(handoffs.length, 1);
    await page.goto(base + "/campaign-handoff.html"); await page.getByRole("link", { name: /Record observations and review research/ }).click();
    async function open() {
      await page.waitForFunction(() => document.querySelector("#workspace").options.length > 0);
      await page.locator("#workspace").selectOption(workspace.workspaceId);
      await page.getByRole("button", { name: "Open measurement workspace", exact: true }).click();
      await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Measurement workspace loaded"));
    }
    await open(); assert.match(await page.locator("#lineage").textContent(), /PLANNED · NOT_SENT/);
    if (process.env.BLISS_VERIFY_RESTORE !== "1") {
      await page.locator("#kind").selectOption("LOCAL_VALIDATION");
      await page.locator("#source").fill("TEST local workflow drill");
      await page.locator("#evidence").fill("TEST <script>unsafe text</script> synthetic observation. No actual traffic.");
      await page.getByRole("button", { name: "Save observation", exact: true }).click();
      await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Sourced observation saved"));
      assert.equal(await page.locator("#observations script").count(), 0);
      await page.locator("#hypothesis").fill("TEST improve creative readability");
      await page.locator("#experiment").fill("Compare fictional layouts with independent human review.");
      await page.locator("#criterion").fill("A reviewer can identify the offer.");
      await page.getByRole("button", { name: "Save research proposal", exact: true }).click();
      await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Research proposal saved"));
      await page.locator("#rationale").fill("TEST accept research only; no matching or pricing changes.");
      await page.getByRole("button", { name: "Record research review", exact: true }).click();
      await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Research review saved"));
      await page.reload(); await open();
    }
    const history = (await api(root + "/measurement")).history;
    assert.equal(history.observations.length, 1); assert.equal(history.proposals.length, 1); assert.equal(history.reviews.length, 1);
    assert.equal(history.observations[0].lineageSnapshotJson, handoffs[0].snapshotJson);
    assert.equal(history.observations[0].evidenceStatus, "LOCAL_TEST_OBSERVATION");
    assert.equal(history.observations[0].impressions, undefined); assert.equal(history.reviews[0].decision, "ACCEPT_FOR_RESEARCH");
    assert.equal(history.productionChanged, false); assert.equal(history.delivery, "NOT_SENT");
    assert.match(await page.locator("#proposals").textContent(), /ACCEPT_FOR_RESEARCH/);
    await page.getByRole("button", { name: "Check operating readiness", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Operating readiness checked"));
    assert.match(await page.locator("#readiness-results").textContent(), /UP_TO_DATE/);
    assert.match(await page.locator("#readiness-results").textContent(), /PENDING_HOSTED_VALIDATION/);
    const readiness = await api("/api/operations/campaign-readiness");
    assert.equal(readiness.hostedAcceptanceClaimed, false); assert.equal(readiness.liveCallsEnabled, false); assert.equal(readiness.databaseReachable, true);
    await page.setViewportSize({ width: 390, height: 844 }); assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    assert.deepEqual(errors, []);
    console.log(process.env.BLISS_VERIFY_RESTORE === "1"
      ? "PASS: restored application serves identical observation, frozen lineage, proposal and research review; database/migration readiness healthy."
      : "PASS: sourced observation, frozen handoff lineage, research proposal/review, reload history, safe text, staff readiness and mobile layout. No real traffic or paid AI calls.");
    await context.close();
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
