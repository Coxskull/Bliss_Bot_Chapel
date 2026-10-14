import test from 'node:test';
import assert from 'node:assert/strict';
import {usd} from './format.js';
test('USD remains exact beyond JavaScript safe integer range',()=>assert.equal(usd('9007199254740993'),'$90,071,992,547,409.93 USD'));
test('rejects malformed monetary values',()=>{for(const x of ['-1','1.2','NaN',''])assert.throws(()=>usd(x));});
