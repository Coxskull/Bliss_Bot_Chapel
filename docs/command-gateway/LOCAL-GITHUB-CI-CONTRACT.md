# AGM-A2A-005 bounded GitHub/Codex/CI contract

Pure local validation only. It grants no GitHub token, Codex tool, shell, CI run,
merge, deployment or owner approval. A trusted engineering principal and policy
bind one exact repository/base branch, allowed source/documentation prefixes,
required check names and a maximum changed-file count.

Proposals require distinct lowercase 40-character base/head identities, a feature
branch and unique normalized relative paths. Workflow, deployment, infrastructure,
Kubernetes, environment and key/certificate paths are refused regardless of an
allowed prefix. CI evidence must bind the same repository/head, contain exactly
the required successful checks once each and report a 64-character uppercase
artifact SHA256. Hashes are reported correlation identities, not fetched artifact
verification or signatures.

A valid result is readyForHumanReview=true with approvalGranted, mergeAuthorized,
deploymentAuthorized and toolExecutionAuthorized all false. No pull request is
created and no command is executed by this contract. Actual GitHub/Codex/CI
adapters require separately approved identities, token custody/rotation, isolated
target, tool/path permissions, cost limits, independent QA and owner acceptance.
