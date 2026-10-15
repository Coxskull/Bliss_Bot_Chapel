# AGM-LAUNCH-010 R18 — isolated container CI checks

PR175 was merged externally at 53bcae1d8a4eeb12d861a15952e4cb1d9e6be94b.
Its remote CI156 built an image but did not run it. The local environment still
denies the Microsoft registry redirect; do not bypass that restriction.

Extend the existing GitHub container job, with its existing read-only repository
permission, to label the image with the actual checked-out commit (the PR merge
preview in pull-request CI), then test that immutable image ID. Record the source
revision, image ID, configured and observed nonzero UID, entry JS/CSS and all
financial asset SHA256 values. Check media/fonts, absence of Node/npm and the
transient public build CA. Preserve the existing Production/8080/entrypoint defaults.

Run with no network, no published ports, no host mounts or forwarded credentials,
a read-only root, bounded ephemeral /tmp, dropped capabilities and no new
privileges. The default application must exit nonzero with the exact missing-OIDC
message, without ever logging server startup. Timeout, unrelated exception,
successful exit and OOM are failures, not evidence of authentication refusal.
Remove the task container in a finally block. Upload the partial JSON/log evidence
even when the gate fails; success requires the job itself to pass.

This is partial packaging and startup-refusal acceptance. It does not prove a
running authenticated service, HTTP authorization/CSRF, PostgreSQL payment flow,
browser recovery/export, OS vulnerability audit, real OIDC/provider connectivity,
hosted readiness or owner acceptance. Those gates remain open. Existing .NET
regression CI is separate; this change adds no payment code, schema or secrets.
Stripe account remains UNVERIFIED / OWNER DECISION REQUIRED. No push of images,
production access, merge, deployment or paid activation is performed by this gate.
