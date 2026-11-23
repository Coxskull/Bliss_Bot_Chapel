import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";

const source = readFileSync(new URL("../frontend/public/alpha-intake.js", import.meta.url), "utf8");
const receipt = "11111111-2222-4333-8444-555555555555";

function harness() {
  const events = new Map();
  const fields = Object.fromEntries(
    Object.entries({
      full_name: "Fictional Test Person", email: "test@example.invalid",
      organization: "", website: "https://example.com", market: "Manila, Philippines",
      category: "Podcast", description: "", middle_name: ""
    }).map(([name, value]) => [name, { value, checked: false, disabled: name === "middle_name" }])
  );
  for (const name of ["adult_confirmed", "processing_acknowledged", "contact_consent"])
    fields[name] = { value: "on", checked: true, disabled: false };
  fields.marketing_opt_in = { value: "on", checked: false, disabled: false };
  const submitButton = { disabled: false, textContent: "Submit interest →" };
  const controls = [...Object.values(fields), submitButton];
  const requests = [];
  let uuidCount = 0;
  const crypto = { randomUUID() {
    uuidCount++;
    return "aaaaaaaa-aaaa-4aaa-8aaa-" + String(uuidCount).padStart(12, "0");
  }};
  const form = {
    dataset: {
      kind: "creator",
      endpoint: "https://bkutbglzivfdnoerigyb.supabase.co/functions/v1/alpha-public-intake"
    },
    elements: { namedItem: name => fields[name] ?? null },
    reportValidity: () => true,
    addEventListener: (type, listener) => events.set(type, listener),
    querySelectorAll: selector => selector.startsWith("input[name=") ? [] : controls
  };
  const status = {
    hidden: true, textContent: "", className: "", focus() {}
  };
  const elements = {
    "#alpha-intake-form": form, "#form-status": status, "#submit-button": submitButton
  };
  const fetch = (url, options) => new Promise((resolve, reject) => requests.push({ url, options, resolve, reject }));
  runInNewContext(source, {
    document: { querySelector: selector => elements[selector] ?? null },
    window: { crypto, fetch }, crypto, fetch, AbortSignal: { timeout: () => undefined }
  });
  const submit = () => events.get("submit")({ preventDefault() {} });
  const input = () => events.get("input")();
  return { fields, controls, requests, submitButton, status, submit, input,
    get uuidCount() { return uuidCount; } };
}

// An uncertain response must preserve the original intent and original request key.
const a = harness();
const first = a.submit();
assert.equal(a.requests.length, 1, "first request submitted");
assert.ok(a.controls.every(control => control.disabled), "fields and button locked in flight");
await a.submit();
assert.equal(a.requests.length, 1, "programmatic double submit cannot create a second POST");
a.input(); // Even a synthetic input event must not rotate the in-flight intent.
a.requests[0].resolve({ status: 503, json: async () => ({ error: "Temporary outage" }) });
await first;
assert.equal(a.submitButton.disabled, false, "button unlocked on error");
assert.equal(a.fields.middle_name.disabled, true, "previously disabled control stays disabled");
assert.equal(a.fields.full_name.disabled, false, "editable fields restored on error");

const retry = a.submit();
assert.equal(a.requests.length, 2);
assert.equal(
  JSON.parse(a.requests[0].options.body).request_key,
  JSON.parse(a.requests[1].options.body).request_key,
  "failed network/request attempt retries the same immutable request key"
);
assert.equal(a.uuidCount, 1, "no new intent invented by error or in-flight event");
a.requests[1].resolve({ status: 201, json: async () => ({
  status: "RECEIVED_NOT_YET_VERIFIED", receipt_id: receipt
}) });
await retry;
assert.ok(a.status.textContent.includes(receipt), "verified receipt shown");
assert.ok(a.controls.every(control => control.disabled), "confirmed form remains locked");
await a.submit();
assert.equal(a.requests.length, 2, "saved receipt cannot submit again");

// Genuine edits AFTER a failed response must create a fresh intent/key.
const b = harness();
const attempted = b.submit();
b.requests[0].reject(new Error("lost response"));
await attempted;
b.fields.market.value = "Cebu, Philippines";
b.input();
const changed = b.submit();
assert.notEqual(
  JSON.parse(b.requests[0].options.body).request_key,
  JSON.parse(b.requests[1].options.body).request_key,
  "user edit after failed request creates a new request intent"
);
b.requests[1].resolve({ status: 503, json: async () => ({ error: "Still offline" }) });
await changed;
assert.equal(b.submitButton.disabled, false, "failed edited intent can be retried");
console.log("PASS: in-flight lock, no double submit, idempotent retry, confirmed receipt, legitimate edited intent");
