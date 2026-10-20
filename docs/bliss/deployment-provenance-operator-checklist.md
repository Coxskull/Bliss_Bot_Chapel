# Bliss Bot read-only deployment provenance verification

Status: proposed offline control only. This document does not approve a deployment, database action, or public activation.

## Three independent source records

1. GitHub repository `Coxskull/Bliss_Bot_Chapel`, default branch `main`: record the full HEAD commit SHA from the GitHub branch API.
2. Railway > project `bliss-bot` > production > app service: record the source repository, branch and configured source SHA.
3. Railway > same service > Deployments: select the currently active successful deployment, record its deployment ID and **deployment metadata** `commitHash`. Do not accidentally select an old removed deployment or a newer failed deployment.

An active deployment's commit metadata is NOT a cryptographic attestation of the currently running image. The configured source SHA and a deployment's commit metadata can differ. Never collapse these records into one field.

Prepare a local file named `provenance.json` containing only this non-secret metadata:

```json
{
  "main_repository": "Coxskull/Bliss_Bot_Chapel",
  "main_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "configured_repository": "Coxskull/Bliss_Bot_Chapel",
  "configured_branch": "main",
  "configured_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "deployment_metadata_sha": "cccccccccccccccccccccccccccccccccccccccc",
  "deployment_status": "SUCCESS"
}
```

All example SHAs are placeholders; replace with freshly retrieved, complete real commit IDs. Use null for unknown configured or deployment commit metadata (and null status with unknown deployment), never a guessed SHA. Unknown extra fields are rejected.

Run from repository root, offline:

```sh
python3 scripts/bliss-deployment-provenance.py provenance.json
python3 -m unittest discover -s scripts -p 'test_bliss_deployment_provenance.py' -v
```

Results: `FAIL_DRIFT` means at least one independently known source disagrees; `BLOCKED_INCOMPLETE` means input evidence is missing; `INVALID_INPUT` rejects malformed input; `MATCH_UNATTESTED` means only that supplied metadata are consistent. The tool exits nonzero for all outcomes except `MATCH_UNATTESTED`. **Even a zero exit does not authorize release** or verify real image content, merged changes, ingress, HTTPS, OIDC, backup/restoration, or private features. The tool never authenticates to GitHub or Railway and never modifies either provider.

Observed read-only state on 2026-10-20:

- Configured source SHA: `121fe37a5d2d9b26281324f1af8306cfaed509ee`
- Current active Railway SUCCESS deployment metadata SHA: `7751d20be61b97cc89309bf2e11f80e6bf20e5d2`
- GitHub `main` SHA: `72028391eb236bce2a0c77d6452fd0581e07be6c`

Those three disagree: expected synthetic classification from actual observed non-secret metadata is `FAIL_DRIFT`. GitHub compare showed main 20 commits after configured SHA, including merged PR #303 and #306, which are absent from the source recorded for the active deployment. Do not assert their presence in the running application.

Before production authorization, an independent operator must establish an immutable deployed artifact digest/build provenance and reconcile both source identifiers to the actual running code. Review the preview-locked rollout and backup/restore gate separately. Ingress/HTTPS and real OIDC cannot be inferred from matching Git metadata.

Related: issue #302; pending draft PR #307 handles a complementary two-way inventory check. No overlap with the separate operational approval workflow.
