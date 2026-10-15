import {strict as assert} from 'node:assert';
import {upload_to_drive,verify_drive_upload} from './evidence_drive_adapter.mjs';
let count=0;
const plan={folderId:'fixture-folder',files:[{name:'test.log',localPath:'/synthetic/test.log',mimeType:'text/plain'}]};
const connector={
  uploadFile:async()=>({structuredContent:{id:'fixture-file',url:'https://drive.google.com/file/d/fixture-file/view'}}),
  getMetadata:async()=>({structuredContent:{id:'fixture-file',parent_ids:['fixture-folder'],size:'3',url:'https://drive.google.com/file/d/fixture-file/view'}}),
  fetchFile:async()=>({structuredContent:{file_uri:{fixtureOnly:true}}})
};
const receipt=await upload_to_drive(connector,plan); assert.equal(receipt[0].file_id,'fixture-file');count++;
const read=await verify_drive_upload(connector,receipt);assert.equal(read.contentIntegrityVerified,false);count++;
await assert.rejects(upload_to_drive(connector,{folderId:'fixture-folder',files:[plan.files[0],plan.files[0]]}));count++;
await assert.rejects(upload_to_drive({...connector,uploadFile:async()=>({isError:true})},plan));count++;
await assert.rejects(upload_to_drive({...connector,getMetadata:async()=>({structuredContent:{id:'fixture-file',parent_ids:['wrong-folder']}})},plan));count++;
await assert.rejects(upload_to_drive({...connector,uploadFile:async()=>({structuredContent:{success:false}})},plan));count++;
console.log(JSON.stringify({passed:count,failed:0,skipped:0,scope:'Synthetic connector contract; actual Drive execution is separate'}));
