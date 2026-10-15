// Connector injection keeps token custody in the authorized host, not this module.
function result(value) {
  if (value?.isError) throw new Error('Connected Drive action failed');
  const payload = value?.structuredContent ?? value;
  if (!payload || payload.success === false) throw new Error('Drive response failed');
  return payload;
}

async function upload_to_drive(connector, plan) {
  if (!plan.folderId || !Array.isArray(plan.files) || plan.files.length === 0)
    throw new Error('Explicit upload plan required');
  const names = new Set();
  for (const file of plan.files) {
    if (!file.name || !file.localPath || names.has(file.name)) throw new Error('Ambiguous upload plan');
    names.add(file.name);
  }
  const receipts = [];
  for (const file of plan.files) {
    const uploaded = result(await connector.uploadFile({file_uri:file.localPath, file_name:file.name,
      mime_type:file.mimeType, parent_folder_id:plan.folderId}));
    if (!uploaded.id) throw new Error('Drive file ID missing');
    const metadata = result(await connector.getMetadata({fileId:uploaded.id,
      fields:'id,name,mimeType,size,parents,webViewLink'}));
    if (metadata.id !== uploaded.id || !metadata.parent_ids?.includes(plan.folderId))
      throw new Error('Drive folder identity mismatch');
    receipts.push({path:file.name, file_id:uploaded.id, parent_id:plan.folderId,
      url:metadata.url ?? uploaded.url, bytes:metadata.size ?? null});
  }
  if (new Set(receipts.map(r=>r.file_id)).size !== receipts.length) throw new Error('Drive ID reused');
  return receipts;
}

async function verify_drive_upload(connector, receipts) {
  // Returns authenticated file references for materialization. Caller must rehash
  // complete raw bytes with the Python verifier; this does not assert integrity.
  const downloads = [];
  for (const receipt of receipts) {
    if (!receipt.url || !receipt.file_id) throw new Error('Verified receipt required');
    const fetched = result(await connector.fetchFile({url:receipt.url,
      download_raw_file:true, include_base64:false}));
    downloads.push({path:receipt.path, file_id:receipt.file_id, raw_result:fetched});
  }
  return {contentIntegrityVerified:false, downloads};
}

export {upload_to_drive, verify_drive_upload};
