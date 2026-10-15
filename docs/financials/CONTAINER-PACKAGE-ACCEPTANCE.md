# AGM-LAUNCH-010 R12 — container financial UI packaging

Dockerfile now builds the separate frontend with Node24, locked npm dependencies, lifecycle scripts disabled, seven frontend tests and Vite build. A separate .NET8 SDK stage copies generated assets into the existing frontend tree before Release publish and checks the financial entry exists. Runtime remains ASP.NET8, Production by default, port8080, non-root APP_UID, existing ffmpeg/fonts. No Node/npm/source tree is intentionally copied into runtime.

Context excludes node_modules, local generated financial assets, development environment files, secrets folders and task work/output. Clean committed `git archive` context is used for isolated rehearsal; local generated assets cannot substitute for the Node build. `SOURCE_REVISION` can label an exact commit; default unknown requires the eventual approved build pipeline to supply provenance.

Optional BuildKit secret `proxy_ca` accepts a public combined CA bundle for managed proxy builds. Node uses NODE_EXTRA_CA_CERTS and .NET uses SSL_CERT_FILE only inside networked build steps. TLS verification stays enabled; bundle is not copied into final image. Ordinary builds need not supply this secret. Preserve environment proxy configuration. Runtime apt behavior is unchanged.

Acceptance records the exact source head and image digest, generated entry/JS/CSS and asset hashes, bounded publish-secret scan, runtime identity, local browser startup/access refusal and production-disabled-auth refusal. No image push, registry publishing, deployment, hosted migration or real provider activation. Native container/image acceptance must be reported accurately if the execution environment cannot finish the build; local R11 package acceptance cannot substitute for it.

Stripe UNVERIFIED / OWNER DECISION REQUIRED. Mock development continues. Real OIDC/hosted/account/legal merchant/payee/fee/sandbox decisions and cancellation-policy/quorum/release remain pending. OS image vulnerability scanning is separate from NuGet/npm audits; no full container security certification is claimed.
