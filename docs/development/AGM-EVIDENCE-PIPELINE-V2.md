# AGM-OPS-011 — reusable evidence pipeline v2

The Python standard-library helper prepares CSPRNG UUIDv4 evidence IDs, explicit
artifact inventories, canonical manifests and exclusive ZIP packages. It
validates paths, byte hashes, source/task/test references, bounded inventories,
revision links and absence of engineering acceptance. Failed/skipped tests stay
reported; failed verification cannot produce a ready submission.

Drive operations use the session's authorized connected tools. The helper does
not obtain credentials, make network calls, change sharing, update approval
fields or invoke independent agents. The orchestration sequence is:

1. Execute tests; collect exact logs/results and source references.
2. Generate a secure ID; query existing Drive registration for collisions.
3. Create one submission folder under the existing task/evidence folder.
4. Upload declared artifacts and canonical manifest, then download their bytes.
5. Verify returned IDs/parents, exact inventory and every downloaded SHA256.
6. Bind owner-confirmed reviewer accounts to existing readable folder ACLs.
7. Recheck registry uniqueness and artifact bytes; create a separate submission
   index. The manifest stays PACKAGING; the index owns the committed state.
8. Upload/download the index. Generate the owner-ready notification only when
   that index matches exactly and reviewer access is verified.

`SUBMISSION_INCOMPLETE` is preserved when reviewer access or other gates fail.
The submission index hashes the uploaded manifest; neither file hashes itself,
avoiding circular hashing. Submitted files are never replaced by this helper;
revisions get new evidence IDs linked with `supersedes_evidence_id`. A Drive
folder name is a lookup key, not guaranteed provider-enforced uniqueness;
registry collisions must be checked before and after creation.

Caller-provided connector receipt/account-binding evidence is not authenticated
by this local Python helper. Rehashing verifies bytes in the downloaded files,
not third-party truth, provenance signatures or independent Executive review.
Tests and their synthetic receipts demonstrate local controls; actual Drive
execution and readback must be documented separately. No recurring scheduler or
cross-session autonomous communication is established. Permission metadata plus
owner-confirmed account binding verifies the access policy; it does not prove a
reviewer has retrieved or accepted the submission. Secret/PII redaction remains
an explicit package-preparation responsibility; hashes do not sanitize files.

Run `python3 scripts/test_evidence_pipeline.py` for the local control suite.
No .NET application, database schema, payment or campaign behavior changes.
