import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const js=readFileSync(new URL("../frontend/public/advertiser-campaign-drafts.js",import.meta.url),"utf8");
test("accepted order record preserves scope and terms but never claims payment",()=>{
 const start=js.indexOf('const receipt=document.createElement("button");receipt.type="button";receipt.textContent="Save order acceptance record (TXT)"');
 const end=js.indexOf('const refresh=document.createElement("button");',start);
 assert.ok(start>js.indexOf('if(result.status==="OFFERED")'));
 assert.ok(end>start);
 const record=js.slice(start,end);
 for(const marker of ["!csrf","current!==generation","!card.isConnected","!receipt.isConnected","offerButton.disabled","result.offerId","row.campaignId","result.offerSha256","result.scope","result.terms","Not a tax invoice, payment receipt","text/plain;charset=utf-8","previewUrls.push(url)","/support.html"])assert.ok(record.includes(marker),marker);
 assert.doesNotMatch(record,/fetch\(|localStorage|sessionStorage|innerHTML/);
});
