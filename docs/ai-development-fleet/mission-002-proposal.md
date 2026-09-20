# Mission 002 proposal

**Do not start Mission 002 until Erwin approves.** Mission 001 ends here.

## Recommended Mission 002 (mandatory first)

**Name:** Alpha Auto credential rotation and secret-hygiene freeze  

**Why first:** STOP condition — production-style database passwords and JWT keys are committed in `alpha-backend` and `Alpha-Auto-MVP` `appsettings.json`.

**In scope**

- Rotate database and JWT secrets in the hosting providers
- Remove secrets from default branches (env vars / user secrets / secret store)
- Disable or protect `DatabaseDiagnostic`, unauthenticated financial GETs, unsigned PayMongo webhook, open commission test endpoint
- Confirm Bliss remains untouched except docs

**Out of scope**

- Bliss Phase 2 data
- Fishing Fleet
- Live Wise/PayPal for media
- Merging Auto and Bliss databases
- Auth redesign for Bliss

**Risk:** 🔴 high (secrets, production auth) — explicit approval required.

**Acceptance**

- No hosted passwords/JWT in git
- Apps start with environment variable **names** documented
- Evidence: redacted screenshots / “secret not in file” grep of default branch

---

## Recommended Mission 002-B (only after 002 or in parallel if Bliss-only)

**Name:** Bliss Phase 2 Engineering Contract (controlled test data)

This matches the prior Bliss stop-point: **do not implement Phase 2 until a contract exists.**

Contract must define: requirements, tasks, acceptance tests, forbidden changes, evidence, definition of done.

Still forbidden without a later mission: Chaperone/Officiant engines, n8n, live affiliates, payments.

---

## Explicitly not Mission 002

- “Finish Alpha”
- “Build Bliss Bot Chapel”
- Replacing Bliss architecture
- Creating country-specific creator tables
- Treating Alpha Auto order ledger as the media financial engine
