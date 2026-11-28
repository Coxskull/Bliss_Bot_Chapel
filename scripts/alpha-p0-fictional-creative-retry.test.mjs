import test from "node:test";
import assert from "node:assert/strict";
import {verifyHostedRetry} from "./alpha-p0-fictional-creative-roundtrip.mjs";

test("reject first unverified receipt without issuing a second request", async () => {
 let calls=0;
 await assert.rejects(verifyHostedRetry(async()=>{calls++;return {release:"RELEASED"};}),/Unverified fictional result/);
 assert.equal(calls,1);
});
test("do not retry transport failures", async () => {
 let calls=0;
 await assert.rejects(verifyHostedRetry(async()=>{calls++;throw Error("transport refused");}),/transport refused/);
 assert.equal(calls,1);
});
