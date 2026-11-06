# ALPHA Task #4 — gated creator-owned account implementation

**Status: review-only and default-disabled. Do not deploy or activate.** The public creator interest form is already live. Neither that record, an email address nor a HighLevel contact may create a creator session.

## Implementation boundary

- Reuses the existing Supabase OIDC code that validates the ID and access tokens and binds subject and client ID.
- Authentication:CreatorSelfServiceRequested defaults to false; Authentication:ApprovedCreatorSubjects defaults to empty; currently approved test/staff subjects must be disjoint.
- Provisioning requires the subject to be explicitly allowlisted, a signed email_verified=true claim, and *verified provider access token* app_metadata with exactly one role [bliss.creator], bliss_creator_verified=true and a valid bliss_creator_id UUID.
- A creator cannot also have advertiser or reviewer workspace assignments. User-editable user_metadata, public form or CRM data never grant privileges.
- A read-only GET /api/creators/me requires exact issuer-bound creator role and ID. The matching authoritative marketplace Creator row must have AuthorityStatus=VERIFIED; response is a minimal creator summary with Cache-Control: no-store.
- No public signup, payment, creator edit, inventory, research, outreach, administrative privilege or automatic identity linking is added.

## Gates still requiring owner approval and hosted acceptance

1. Do not enable until shared recovery, protected Railway ingress, consent and rollback security gates (#1, #3 shared auth, #7, #8) are accepted. Default-off must remain confirmed on actual deployed service.
2. Verify hosted OIDC returns the authenticated and signed email_verified claim and server-controlled app_metadata using the current Supabase OAuth contract; do not downgrade to user metadata.
3. Human verification must approve the creator and securely assign the real provider subject, verified flag and correct marketplace creator ID. No public-submission auto-promotion.
4. Demonstrate hosted self-only access and cross-tenant denial: anonymous, advertiser, unrelated subject, mixed role, invalid issuer, expired/revoked session, stale cookie, unverified creator. Test rollback and backups.
5. Separate issuer-side session revocation and short-lived cookie policy remain open: provider JWT and application cookies can contain stale authorization until refreshed or expired.
6. Independently read back the HighLevel creator contact using an authorized provider session; SQL DELIVERED is not a direct provider GET.
7. Design and review actual self-signup/invitation UI only after identities and role-scope acceptance pass.

## Synthetic-only test

dotnet test Bliss.Tests/Bliss.Tests.csproj --filter FullyQualifiedName~CreatorSelfServiceGateTests

The unit/HTTP test validates a default-denied boundary only. It does not create real users or assert live creator account registration success. This branch is not permitted to bypass standing STOP, review or migration requirements.
