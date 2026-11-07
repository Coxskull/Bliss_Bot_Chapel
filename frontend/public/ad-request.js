"use strict";
(function (root) {
  const commandPattern = /^Create a (landscape|square) ad for (Fictional [A-Za-z0-9][A-Za-z0-9 &'().-]{0,78}) in ([A-Za-z0-9][A-Za-z0-9 ,&'().-]{0,78}); headline: ([^;\r\n]{1,180}); CTA: ([^;\r\n]{1,80})$/i;
  function normalized(value) { return value.normalize("NFC").replace(/\s+/g, " ").trim(); }
  function parseFictionalAdRequest(value) {
    if (typeof value !== "string" || value.length > 500 || /[\u0000-\u001f\u007f]/.test(value)) {
      throw new Error("Use the sample format with a fictional brand, market, headline, and CTA (maximum 500 characters).");
    }
    const match = value.trim().match(commandPattern);
    if (!match) throw new Error("Expected: Create a landscape ad for Fictional Brand in Market; headline: Your headline; CTA: Your call to action");
    const [, rawFormat, rawBrand, rawMarket, rawHeadline, rawCta] = match;
    const brandName = normalized(rawBrand), market = normalized(rawMarket);
    const headline = normalized(rawHeadline), cta = normalized(rawCta);
    if (!/^Fictional\s/i.test(brandName) || !headline || !cta ||
        /[<>]/.test([brandName, market, headline, cta].join(""))) {
      throw new Error("Only plain-text fictional advertiser requests are supported.");
    }
    return Object.freeze({
      templateId: rawFormat.toLowerCase() === "square" ? "alpha-original-square-v1" : "alpha-original-landscape-v1",
      format: rawFormat.toLowerCase(), brandName, market, headline, cta
    });
  }
  const api = Object.freeze({ parseFictionalAdRequest });
  if (root) root.BlissAdRequest = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})(typeof window !== "undefined" ? window : globalThis);
