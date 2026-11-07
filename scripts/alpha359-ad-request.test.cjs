"use strict";
const test = require("node:test");
const assert = require("node:assert/strict");
const { parseFictionalAdRequest } = require("../frontend/public/ad-request.js");

test("parses and normalizes a bounded fictional landscape request", () => {
  const result = parseFictionalAdRequest(" Create a landscape ad for Fictional Luna Verde in Santo   Domingo; headline: Fresh flavors, brighter days; CTA: Visit us today ");
  assert.deepEqual(result, {
    templateId: "alpha-original-landscape-v1", format: "landscape",
    brandName: "Fictional Luna Verde", market: "Santo Domingo",
    headline: "Fresh flavors, brighter days", cta: "Visit us today"
  });
});

test("selects the existing square template deterministically", () => {
  assert.equal(parseFictionalAdRequest("Create a square ad for Fictional Nube Cafe in Medellin; headline: A little joy in every cup; CTA: Explore the menu").templateId,
    "alpha-original-square-v1");
});

test("rejects real, malformed, markup, control-character, and oversized requests", () => {
  for (const bad of [
    "Create a landscape ad for Real Luna Verde in Santo Domingo; headline: Fresh; CTA: Visit",
    "Create a portrait ad for Fictional Luna Verde in Santo Domingo; headline: Fresh; CTA: Visit",
    "Create a landscape ad for Fictional Luna Verde in Santo Domingo; headline: <img src=x>; CTA: Visit",
    "Create a landscape ad for Fictional Luna Verde in Santo Domingo; headline: Fresh\nNow; CTA: Visit",
    "x".repeat(501)
  ]) assert.throws(() => parseFictionalAdRequest(bad));
});
