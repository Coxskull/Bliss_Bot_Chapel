// Fictional local workflow only. Separate hosted principals are covered by API tests.
const { chromium } = require("playwright");
const assert = require("node:assert/strict");
const png = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAQCAYAAAB3AH1ZAAAAI0lEQVR4nGMI0Oj5P5CYYdQBow4YdcCoA0YdMOqAUQcMtAMAujoGPVlqwssAAAAASUVORK5CYII=";
(async () => {
  const base = process.env.BLISS_SMOKE_BASE || "http://127.0.0.1:5086";
  assert(["127.0.0.1", "localhost"].includes(new URL(base).hostname));
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || "/usr/bin/chromium", headless: true });
  try {
    const context = await browser.newContext(), page = await context.newPage(), errors = [];
    page.on("pageerror", error => errors.push(error.message));
    async function api(path, data) {
      const response = data ? await context.request.post(base + path, { data }) : await context.request.get(base + path);
      assert(response.ok(), `${path}: ${response.status()} ${await response.text()}`); return response.json();
    }
    assert.equal((await api("/api/auth/session")).authenticationEnabled, false);
    const advertiserId = "33333333-3333-3333-3333-333333333334", match = "77777777-7777-7777-7777-777777777776";
    const creator = "11111111-1111-1111-1111-111111111113", opportunity = "55555555-5555-5555-5555-555555555554";
    const slot = "99999999-9999-9999-9999-999999999995";
    const workspace = await api("/api/wedding-planner/workspaces", { advertiserId, sourceSystem: "PHASE5_SMOKE", idempotencyKey: crypto.randomUUID() });
    const root = `/api/wedding-planner/workspaces/${workspace.workspaceId}`;
    // Brand Intake and Academy may already have authored append-only revisions
    // in this same fictional workspace; never assume a fresh version-zero state.
    const existingDna = await api(root + "/brand-dna");
    const currentDnaVersion = existingDna.at(-1)?.version || 0;
    const draft = await api(root + "/brand-dna", { expectedVersion: currentDnaVersion, idempotencyKey: crypto.randomUUID(), fields: {
      brandName: "TEST handoff pharmacy", niche: "pharmacy", market: "São Paulo, Brazil", language: "Portuguese", colors: "Burgundy", font: "Montserrat", goals: "Fictional awareness", restrictions: "Workflow fixture only" } });
    const confirmed = await api(root + "/brand-dna/confirm", { expectedVersion: draft.revision.version, idempotencyKey: crypto.randomUUID() });
    const brief = await api(root + "/production/briefs", { brandDnaId: confirmed.revision.id, family: "pharmacy", headline: "TEST handoff brief", cta: "TEST visit", idempotencyKey: crypto.randomUUID() });
    const briefPath = root + `/production/briefs/${brief.brief.id}`;
    const candidate = await api(briefPath + "/candidates", { expectedVersion: 0, label: "TEST synthetic 32 × 16 candidate", notes: "Synthetic test asset, no real visual acceptance", imageBase64: png, idempotencyKey: crypto.randomUUID() });
    await api(briefPath + `/candidates/${candidate.candidate.id}/reviews`, { expectedVersion: 0, customization: "PASS", qualityParity: "PASS", originality: "PASS", geographicAuthenticity: "PASS", decision: "PASS", criticalDefect: false, rationale: "Fictional review for workflow validation", preserve: "", repair: "", idempotencyKey: crypto.randomUUID() });
    const recommendation = await api("/api/economics/recommendations", { creatorId: creator, adInventorySlotId: slot, geographicMarketId: "ec000001-0000-0000-0000-000000000001", pricingModelCode: "CPM", durationSeconds: 60, advertiserOpportunityId: opportunity, blissMatchId: match, industryCategory: "CREATOR_TOOLS", campaignObjective: "TEST fixture", sourceSystem: "PHASE5_SMOKE", idempotencyKey: crypto.randomUUID() });
    const quote = await api("/api/economics/quotes", { advertiserOpportunityId: opportunity, requestedBy: "Fixture planner", revisionReason: "Adopt fictional Economics amount", lineItems: [{ rateRecommendationId: recommendation.id, description: "TEST overlay quote", quantity: 1, unitAmount: recommendation.rangeTarget }], sourceSystem: "PHASE5_SMOKE", idempotencyKey: crypto.randomUUID() });
    await api(`/api/economics/quotes/${quote.id}/approvals`, { quoteVersionId: quote.newQuoteVersionId, decision: "APPROVED", reviewerLabel: "Fixture reviewer", rationale: "TEST approval", sourceSystem: "PHASE5_SMOKE", idempotencyKey: crypto.randomUUID() });
    await api(`/api/economics/quotes/${quote.id}/outcomes`, { quoteVersionId: quote.newQuoteVersionId, response: "ACCEPTED", actorLabel: "Fixture owner", rationale: "TEST acceptance", negotiatedLineItems: null, sourceSystem: "PHASE5_SMOKE", idempotencyKey: crypto.randomUUID() });
    await page.goto(base + "/production.html"); await page.getByRole("link", { name: /Review approvals and plan a campaign handoff/ }).click();
    async function open() {
      await page.waitForFunction(() => document.querySelector("#workspace").options.length > 0);
      await page.locator("#workspace").selectOption(workspace.workspaceId);
      await page.getByRole("button", { name: "Open handoff workspace", exact: true }).click();
      await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Handoff workspace loaded"));
      // Re-select the exact test-owned approved brief/candidate on every open,
      // including after reload. A workspace can contain older REVISE candidates.
      await page.locator("#brief").selectOption(brief.brief.id);
      await page.getByRole("button", { name: "Load candidate history", exact: true }).click();
      await page.waitForFunction(id => [...document.querySelector("#candidate").options].some(option => option.value === id), candidate.candidate.id);
      await page.locator("#candidate").selectOption(candidate.candidate.id);
      await page.waitForFunction(id => document.querySelector("#creative img")?.getAttribute("src")?.includes(id), candidate.candidate.id);
      assert.match(await page.locator("#creative").textContent(), /PASS/);
      await page.locator("#match").selectOption(match);
      await page.locator("#slot").selectOption(slot);
    }
    await open();
    assert.match(await page.locator("#price").textContent(), new RegExp(String(quote.versions[0].lineItems[0].lineAmount).replace('.', '\\.')));
    await page.locator("#creative img").evaluate(image => image.decode());
    assert.match(await page.locator("#identity").textContent(), /separate authenticated principals/);
    await page.locator("#campaign-name").fill("TEST browser campaign");
    await page.getByRole("button", { name: "Create draft campaign", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("Draft campaign created"));
    await page.locator("#plan-consent").check();
    await page.getByRole("button", { name: "Record planned handoff", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").dataset.error === "true");
    assert.match(await page.locator("#status").textContent(), /all be approved/);
    for (const stage of ["ADVERTISER", "ALPHA_QA", "INVENTORY_FIT"]) {
      await page.locator("#stage").selectOption(stage);
      await page.locator("#rationale").fill("TEST <script>fixture</script> human workflow decision");
      if (stage === "INVENTORY_FIT") for (const id of ["dimensions", "format", "safe-area"]) await page.locator(`#${id}`).check();
      // A deliberately rejected plan leaves #status in error state. Await the
      // actual approval POST plus a newly rendered audit row instead of reading
      // a stale status message from the earlier negative test.
      const previousApprovalCount = await page.locator("#approvals h3").count();
      const [approvalResponse] = await Promise.all([
        page.waitForResponse(response => response.request().method() === "POST" &&
          response.url().includes(`/handoff/candidates/${candidate.candidate.id}/approvals`)),
        page.getByRole("button", { name: "Record approval decision", exact: true }).click()
      ]);
      assert(approvalResponse.ok(), `Approval ${stage} failed: ${approvalResponse.status()} ${await approvalResponse.text()}`);
      await page.waitForFunction(count => document.querySelectorAll("#approvals h3").length > count, previousApprovalCount);
      assert.equal(await page.locator("#status").getAttribute("data-error"), "false", await page.locator("#status").textContent());
    }
    assert.equal(await page.locator("#approvals script").count(), 0);
    await page.getByRole("button", { name: "Record planned handoff", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").textContent.includes("handoff recorded as PLANNED"));
    const plans = await api(root + "/handoff/plans"); assert.equal(plans.length, 1); assert.equal(plans[0].status, "PLANNED"); assert.equal(plans[0].delivery, "NOT_SENT");
    const snapshot = JSON.parse(plans[0].snapshotJson); assert.equal(snapshot.lineAmount, quote.versions[0].lineItems[0].lineAmount); assert.equal(snapshot.imageWidth, 32); assert.equal(snapshot.imageHeight, 16);
    // A client can bypass the browser's disabled controls. The API must refuse
    // an invented Economics quote line even after all three human gates passed.
    const planPath = root + "/handoff/plans";
    const originalPlan = plans[0];
    const originalSnapshot = originalPlan.snapshotJson;
    const directPlan = {
      candidateId: originalPlan.candidateId, reviewId: originalPlan.reviewId,
      advertiserApprovalId: originalPlan.advertiserApprovalId,
      alphaQaApprovalId: originalPlan.alphaQaApprovalId,
      inventoryFitId: originalPlan.inventoryFitId,
      blissMatchId: snapshot.matchId, campaignId: snapshot.campaignId,
      adInventorySlotId: snapshot.slotId,
      quoteLineItemId: originalPlan.quoteLineItemId,
      idempotencyKey: crypto.randomUUID()
    };
    const forgedQuote = await context.request.post(base + planPath, {
      data: { ...directPlan, quoteLineItemId: crypto.randomUUID() }
    });
    assert.equal(forgedQuote.status(), 404,
      `Direct plan with invented Economics line was not rejected: ${await forgedQuote.text()}`);
    let unchangedPlans = await api(planPath);
    assert.equal(unchangedPlans.length, 1, "Rejected quote must not add a handoff");
    assert.equal(unchangedPlans[0].snapshotJson, originalSnapshot,
      "Rejected quote must not change persisted Economics evidence");
    await page.reload(); await open(); assert.match(await page.locator("#plans").textContent(), /PLANNED · NOT_SENT/);
    assert.equal(await page.locator("#approvals h3").count(), 3);
    await page.locator("#stage").selectOption("ADVERTISER"); await page.locator("#decision").selectOption("WITHHOLD");
    await page.locator("#rationale").fill("TEST revoke new planning authority");
    const previousRevokeCount = await page.locator("#approvals h3").count();
    const [revocationResponse] = await Promise.all([
      page.waitForResponse(response => response.request().method() === "POST" &&
        response.url().includes(`/handoff/candidates/${candidate.candidate.id}/approvals`)),
      page.getByRole("button", { name: "Record approval decision", exact: true }).click()
    ]);
    assert(revocationResponse.ok(), `Withdrawal failed: ${revocationResponse.status()} ${await revocationResponse.text()}`);
    await page.waitForFunction(count => document.querySelectorAll("#approvals h3").length > count, previousRevokeCount);
    await page.locator("#plan-consent").check(); await page.getByRole("button", { name: "Record planned handoff", exact: true }).click();
    await page.waitForFunction(() => document.querySelector("#status").dataset.error === "true"); assert.match(await page.locator("#status").textContent(), /all be approved/);
    // WITHHOLD must be enforced server-side, not just by the UI. Re-submit
    // the previously valid decision IDs with a fresh operation key to simulate
    // a forged direct API client after the advertiser has revoked authority.
    const afterWithdrawal = await context.request.post(base + planPath, {
      data: { ...directPlan, idempotencyKey: crypto.randomUUID() }
    });
    assert.equal(afterWithdrawal.status(), 400,
      `Revoked approvals unexpectedly authorized direct planning: ${await afterWithdrawal.text()}`);
    unchangedPlans = await api(planPath);
    assert.equal(unchangedPlans.length, 1, "Withdrawal cannot create a second handoff");
    assert.equal(unchangedPlans[0].id, originalPlan.id, "Original handoff ID changed");
    assert.equal(unchangedPlans[0].snapshotJson, originalSnapshot,
      "Withdrawal must not rewrite original Economics evidence");
    assert.equal(unchangedPlans[0].status, "PLANNED");
    assert.equal(unchangedPlans[0].delivery, "NOT_SENT");
    await page.setViewportSize({ width: 390, height: 844 }); assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    assert.deepEqual(errors, []);
    console.log("PASS: private candidate preview, missing-gate refusal, draft campaign, three manual approvals, explicit PLANNED/NOT_SENT handoff using Economics amount, reload history, revocation, safe text, and mobile layout. No paid AI calls.");
    await context.close();
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
