import assert from "node:assert/strict";
import test from "node:test";
import {spawnSync} from "node:child_process";
import {fileURLToPath} from "node:url";

// Offline-only contract: never run the hosted privacy-replay script in this
// test process. Spawn it with synthetic settings and prove it rejects unsafe
// targets before it can perform any HTTP POST.
const script = fileURLToPath(new URL("./agm-privacy-repeat-hosted.mjs", import.meta.url));
const stageRef = "s".repeat(20);
const stageUrl = "https://" + stageRef + ".supabase.co/functions/v1/alpha-public-intake";
const envBase = {...process.env};
for (const k of [
  "ALPHA_PRIVACY_STAGING_PROJECT_REF","ALPHA_PRIVACY_STAGING_ENDPOINT",
  "ALPHA_PRIVACY_STAGING_AUTHORIZED","ALPHA_PRIVACY_STAGING_DRY_RUN"
]) delete envBase[k];
const execute = (env = {}) => spawnSync(process.execPath, [script], {
  env: {...envBase, ...env}, encoding: "utf8", timeout: 6000
});
const valid = {
  ALPHA_PRIVACY_STAGING_PROJECT_REF: stageRef,
  ALPHA_PRIVACY_STAGING_ENDPOINT: stageUrl,
  ALPHA_PRIVACY_STAGING_AUTHORIZED: "CONFIRMED_ISOLATED_STAGING",
  ALPHA_PRIVACY_STAGING_DRY_RUN: "true"
};

test("hosted script denies absent authorization and endpoint by default", () => {
  const r=execute();
  assert.notEqual(r.status,0);
  assert.match(r.stderr,/ALPHA_PRIVACY_STAGING_TARGET_NOT_CONFIRMED/);
  assert.equal(r.stdout,"");
});

test("live Bliss Bot, Alpha, and lead-generation Supabase project refs are prohibited", () => {
  const refs = ["bkutbglzivfdnoerigyb","giujjtscdvtpyjpnceag","yyesqcyjhqjryaskrqip"];
  for (const ref of refs) {
    const r=execute({...valid,
      ALPHA_PRIVACY_STAGING_PROJECT_REF:ref,
      ALPHA_PRIVACY_STAGING_ENDPOINT:"https://"+ref+".supabase.co/functions/v1/alpha-public-intake"
    });
    assert.notEqual(r.status,0);
    assert.match(r.stderr,/ALPHA_PRIVACY_PRODUCTION_PROJECT_FORBIDDEN/);
    assert.equal(r.stdout,"");
  }
});

test("a mismatched project domain cannot satisfy the isolated target check", () => {
  const r=execute({...valid,ALPHA_PRIVACY_STAGING_ENDPOINT:
    "https://otherproject.supabase.co/functions/v1/alpha-public-intake"});
  assert.notEqual(r.status,0);
  assert.match(r.stderr,/ALPHA_PRIVACY_STAGING_ENDPOINT_PROJECT_MISMATCH/);
});

test("the endpoint path must name only the existing intake function", () => {
  const r=execute({...valid,ALPHA_PRIVACY_STAGING_ENDPOINT:
    "https://"+stageRef+".supabase.co/functions/v1/anything"});
  assert.notEqual(r.status,0);
  assert.match(r.stderr,/ALPHA_PRIVACY_STAGING_ENDPOINT_NOT_INTAKE/);
});

test("HTTP, localhost and query-bearing endpoints are rejected", () => {
  for (const target of [
    "http://"+stageRef+".supabase.co/functions/v1/alpha-public-intake",
    "https://localhost/functions/v1/alpha-public-intake",
    stageUrl+"?token=unsafe"
  ]) {
    const r=execute({...valid,ALPHA_PRIVACY_STAGING_ENDPOINT:target});
    assert.notEqual(r.status,0);
    assert.match(r.stderr,/ALPHA_PRIVACY_STAGING_ENDPOINT_PROJECT_MISMATCH/);
  }
});

test("hosted privacy-run gate requires explicit operator authorization even in dry-run mode", () => {
  const r=execute({...valid,ALPHA_PRIVACY_STAGING_AUTHORIZED:""});
  assert.notEqual(r.status,0);
  assert.match(r.stderr,/ALPHA_PRIVACY_STAGING_TARGET_NOT_CONFIRMED/);
});

test("safe isolated synthetic target supports an offline preflight with no POST", () => {
  const r=execute(valid);
  assert.equal(r.status,0,r.stderr);
  assert.match(r.stdout,/ALPHA_PRIVACY_STAGING_TARGET_PREFLIGHT_PASS/);
  assert.doesNotMatch(r.stdout,/https?:\/\//);
});
