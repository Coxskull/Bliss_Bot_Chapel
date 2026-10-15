// AGM-LAUNCH-015 Stage H: isolated browser-only consent/withdrawal UX.
// This is NOT legal visitor consent and MUST NOT contact the backend.
const { chromium } = require("playwright");
const assert = require("node:assert/strict");
(async () => {
  const base = process.env.BLISS_SMOKE_BASE || "http://127.0.0.1:5086";
  assert(["localhost", "127.0.0.1"].includes(new URL(base).hostname));
  const browser = await chromium.launch({
    executablePath: process.env.CHROMIUM_PATH || "/usr/bin/chromium", headless: true
  });
  try {
    const context = await browser.newContext(), page = await context.newPage();
    const unsafeRequests = [], pageErrors = [];
    page.on("request", request => {
      if (!["GET", "HEAD"].includes(request.method())) unsafeRequests.push(request.method() + " " + request.url());
      if (new URL(request.url()).origin !== new URL(base).origin) unsafeRequests.push("FOREIGN " + request.url());
    });
    page.on("pageerror", error => pageErrors.push(error.message));
    const result = await page.goto(base + "/ask-alpha-consent-preview.html");
    assert.equal(result.status(), 200);
    assert.match(await page.locator("h1").innerText(), /Control how your conversation/i);
    assert.match(await page.locator("#preview-warning").innerText(), /NON-PRODUCTION DEMONSTRATION/);
    assert.match(await page.locator("#state").innerText(), /NOT RECORDED/);
    assert.equal(await page.locator("#opt-in").isChecked(), false);
    assert.equal(await page.locator("#accept").isDisabled(), true);
    assert.equal(await page.locator("#withdraw").isDisabled(), true);
    await page.locator("#opt-in").check();
    assert.equal(await page.locator("#accept").isEnabled(), true);
    await page.locator("#opt-in").uncheck();
    assert.equal(await page.locator("#accept").isDisabled(), true);
    await page.locator("#opt-in").check();
    await page.locator("#accept").click();
    assert.match(await page.locator("#state").innerText(), /Preview opt-in selected/);
    assert.match(await page.locator("#state").innerText(), /NOT RECORDED/);
    assert.equal(await page.locator("#accept").isDisabled(), true);
    assert.equal(await page.locator("#withdraw").isEnabled(), true);
    assert.equal(await page.locator("#opt-in").isDisabled(), true);
    await page.locator("#withdraw").click();
    assert.match(await page.locator("#state").innerText(), /withdrawn.*NOT RECORDED/i);
    assert.equal(await page.locator("#withdraw").isDisabled(), true);
    assert.equal(await page.locator("#accept").isDisabled(), true);
    const storage = await page.evaluate(() => ({
      local: localStorage.length, session: sessionStorage.length, cookies: document.cookie
    }));
    assert.equal(storage.local, 0);
    assert.equal(storage.session, 0);
    assert.equal(storage.cookies, "");
    await page.reload();
    assert.equal(await page.locator("#opt-in").isChecked(), false);
    assert.equal(await page.locator("#accept").isDisabled(), true);
    assert.equal(await page.locator("#withdraw").isDisabled(), true);
    assert.match(await page.locator("#timeline").innerText(), /No demonstration actions yet/);
    await page.setViewportSize({ width: 390, height: 844 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    assert.deepEqual(pageErrors, []);
    assert.deepEqual(unsafeRequests, []);
    await context.close();
    console.log("PASS: opt-in default-off, purpose-only browser preview, immediate withdrawal, reload reset, no storage, no POST, no external requests, mobile layout; real consent NOT RECORDED. No paid AI calls.");
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
