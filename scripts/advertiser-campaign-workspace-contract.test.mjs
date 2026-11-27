import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const html=readFileSync(new URL("../frontend/public/advertiser-dashboard.html",import.meta.url),"utf8");
const script=readFileSync(new URL("../frontend/public/advertiser-campaign-drafts.js",import.meta.url),"utf8");
test("campaign search and readback are private, bounded and independent of writes",()=>{
 for(const id of ["draft-search","draft-refresh","draft-count","draft-readback"])assert.match(html,new RegExp('id="'+id+'"'));
 assert.match(script,/search\.addEventListener\("input",filterCampaigns\)/);
 assert.match(script,/card\.dataset\.campaignSearch = \(row\.name \+ " " \+ row\.campaignId\)/);
 assert.match(script,/rows\.length <= 50/);
 assert.match(script,/row\.status === "DRAFT"/);
 assert.match(script,/sequence === loadSequence && !el\("profile-panel"\)\.hidden/);
 assert.match(script,/generation\+\+; loadSequence\+\+; csrf = null/);
 assert.match(script,/if \(!validRows\(rows\)\) throw Error\("readback"\)/);
 assert.match(script,/Saving is paused until verification succeeds/);
 assert.doesNotMatch(script,/localStorage|sessionStorage/);
});
