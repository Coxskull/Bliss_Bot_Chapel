# AGM-LAUNCH-010 R11 — local packaging acceptance

Build a local artifact with `bash scripts/package-financial-review.sh /absolute/new/output` using .NET8, Node24 (minimum22.12), Python3 and npm. Output must not exist. The script installs locked dependencies with lifecycle scripts disabled, runs frontend tests/build, publishes .NET Release, runs the existing secret scanner, checks entry JS/CSS and every generated financial asset hash, and writes an exact-head manifest. It does not run migrations, start an application, deploy, merge or activate a provider.

The standalone package can serve `/operations/financial-review/index.html` using the existing .NET static host. A disposable Development rehearsal with authentication disabled must show sign-in/access requirements and no financial controls. Liveness is not financial readiness; mock financial endpoints remain disabled by default. Real OIDC/hosted acceptance is separate from this static-package check and R10's synthetic-identity API/DB rehearsal.

Known deployment gap: current Dockerfile executes only dotnet publish and does not build Node frontend assets. This local package script does not change that Dockerfile or production pipeline. Container packaging requires a separate reviewed Node build stage, context hygiene and image acceptance before deployment. Do not claim merging this script automatically publishes the financial UI on Railway.

Stripe remains UNVERIFIED / OWNER DECISION REQUIRED. Cancellation policy/quorum/release, real account/onboarding, legal merchant/payees/fees, exposed-secret replacement, real sandbox and hosted identity remain pending. Development continues using local/mock artifacts. Task-ID reports and exact-head logs/hashes/browser evidence are submitted for review, not production acceptance.
