# AGM-LAUNCH-010 container acceptance gate

PR175 remains a development draft. Refresh against current main includes case
export and reload recovery; the original seven-test Node result does not verify
these additions. Record exact source revision and clean git-archive context.

Required isolated acceptance sequence:
1. Build financial-ui target with the supplied public CA secret; execute current
   locked frontend tests. Extract entry/JS/CSS and record SHA256. Check context
   exclusions and that the transient CA mount is absent from the stage.
2. Build full image with SOURCE_REVISION equal to the exact tested head. Record
   image ID, base digests, revision label and build log. No registry push.
3. Inspect non-root configured user, Production/8080 defaults and generated assets;
   verify actual runtime UID. Use only task-owned, loopback-bound fixtures with
   synthetic identity, mock options and disposable financial_test PostgreSQL.
4. Verify startup, default unauthenticated denial, scoped/CSRF-protected mock
   operations, refund/reconciliation and browser recovery/export contracts.
   Production startup with missing authentication must fail closed. Never inject
   configured hosted credentials to make the fixture start.
5. Audit OS packages and application dependencies; report scanner/database date,
   findings and any unavailable scan independently. Re-run applicable Release
   and financial PostgreSQL tests for changed application code.
6. Preserve logs/checksums, clean up only owned resources, submit evidence for
   independent review. A successful Node target is not full-image acceptance.

Registry prerequisite: the enforced environment policy must permit the observed
Microsoft registry redirect westus.data.mcr.microsoft.com through the supported
configuration workflow. Generic session network permission is insufficient.
Do not bypass the proxy, use an alternate registry to evade policy, or edit the
policy snapshot. Until this prerequisite is met, full build/runtime/browser/OS
acceptance is NOT VERIFIED. No merge, deployment, provider activation or payment
is authorized by this gate. Stripe account remains UNVERIFIED / OWNER DECISION
REQUIRED; software development continues with mocks.
