import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
const js=readFileSync(new URL("../frontend/public/campaign-handoff.js",import.meta.url),"utf8");
test("workspace change and failed refresh invalidate placement authority",async()=>{
 const nodes=new Map();
 const node=(tag="")=>({tagName:tag.toUpperCase(),value:"",checked:false,disabled:false,hidden:false,textContent:"",dataset:{},children:[],handlers:{},
  get options(){return this.children;},replaceChildren(){this.children=[];this.value="";},append(n){this.children.push(n);if(!this.value&&n.value)this.value=n.value;},
  querySelectorAll(selector){if(selector!=="button")throw Error("Unexpected selector: "+selector);return this.children.flatMap(child=>[...(child.tagName==="BUTTON"?[child]:[]),...child.querySelectorAll(selector)]);},
  addEventListener(k,v){this.handlers[k]=v;}});
 const get=id=>{if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);};
 const ctx=vm.createContext({document:{querySelector:get,createElement:node},crypto:{randomUUID:()=>"fixture-key"},fetch:async()=>({ok:true,json:async()=>({accessAllowed:false})})});
 vm.runInContext(js,ctx);await new Promise(resolve=>setImmediate(resolve));
 vm.runInContext('workspace="fixture";evidenceReady=true;permissions={canPlan:true};$("#plan-consent").checked=true;$("#match").value="match";$("#slot").value="slot";$("#candidate").value="candidate";$("#quote").value="quote";$("#campaign").value="campaign";sync();',ctx);
 assert.equal(get("#plan-fields").disabled,false);
 get("#workspace").handlers.change();
 assert.equal(get("#plan-fields").disabled,true);
 assert.equal(get("#match").value,"");
 assert.equal(get("#plan-consent").checked,false);
 vm.runInContext('workspace="fixture";evidenceReady=true;permissions={canPlan:true};$("#match").value="match";$("#plan-consent").checked=true;',ctx);
 await vm.runInContext('action(async()=>{throw Error("fixture unavailable");})',ctx);
 assert.equal(get("#plan-fields").disabled,true);
 assert.equal(get("#plan-consent").checked,false);
 assert.equal(vm.runInContext("evidenceReady",ctx),false);
});
