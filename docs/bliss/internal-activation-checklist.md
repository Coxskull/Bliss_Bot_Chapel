# Internal activation sequence — execution gated by owner approval

Target: existing bliss-bot Railway project fb33c1b7-03fb-42c8-89b1-081951a738fd,
production environment b67a9836-36bf-4083-93f7-00c867b13f39. Never use Alpha Auto's
service or any other Supabase project as a substitute. No commercial activation.

1. Obtain approval for the actual Pro bill/backup costs and the reviewed loopback
   ingress replacement. Keep the old environment-wide trust option disabled. Confirm
   existing container vulnerabilities are addressed or explicitly reviewed for the
   limited internal acceptance scope; a passing scan workflow is not risk acceptance.
2. Configure real backup schedules and PITR only after paid approval. Wait for the
   first completed snapshot/base backup and record the actual available restore
   range. Daily volume snapshots retain 6 days, weekly 27, monthly 89; PostgreSQL
   PITR retains four full backups (roughly four weeks). A newly enabled archive has
   no pre-enable history. Record real settings, not placeholders, in Runtime:Backup.
3. Review/deploy the approved source with restricted preview still true. Enable the
   loopback ingress policy only after security approval, keep domain port 8080 and
   API port 8081 unpublished. Validate real startup, HTTPS scheme/callback generation,
   caller-header spoof rejection and nonloopback API isolation before user access.
4. Inspect database catalog metadata without reading customer rows. Verify this is
   the dedicated database and that its unexpected-object list is empty. The deployed
   --verify-hosted-database=true diagnostic must report Connected/TlsVerified=true.
   PublicTableCount alone does not certify every nonpublic schema is empty. Stop if
   existing objects differ from the reviewed initial-schema assumption.
5. With a restore point verified, apply the reviewed EF migrations as a separate
   release process. Keep the web service's Runtime__RestrictedPreview=true. In the
   existing app container, use the existing secret configuration and app identity:

   ```sh
   setpriv --reuid=app --regid=app --init-groups --inh-caps=-all --ambient-caps=-all --no-new-privs dotnet /app/Bliss.Api.dll --Runtime:RestrictedPreview=false --migrate
   ```

   The preview override is process-local to the migration command, not a production
   setting or Development-mode exemption. All production posture checks still run.
   The command returns before starting HTTP listeners, the local proxy or hosted jobs.
   Do not run it before backup/TLS/auth/session/ingress prerequisites are satisfied.
6. Re-run the read-only diagnostic and verify applied/pending migration metadata.
   In CI, verify-initial-recovery-fixture.sh exercises a matching PostgreSQL client,
   custom-format dump, restoration into a different disposable database, migration
   history and a synthetic marker. This does NOT certify Railway snapshot/PITR
   recovery. Perform a separately approved hosted restore drill without overwriting
   the source; account for temporary restored-service storage and compute charges.
7. After approvals, initialize limited internal authenticated access. Preserve both
   approved subject UUIDs and server-controlled bliss.operator roles; add no admin
   role, Supabase membership or unapproved identity. The user enters credentials via
   a secure sign-in handoff; never request a password/code in chat. One invitation
   remains unconfirmed as of the last read. Login acceptance requires the complete
   Supabase -> exact /signin-oidc -> secure HttpOnly session -> permitted API flow.
8. Hosted acceptance must prove: anonymous API rejection; wrong-role review rejection;
   two separate sessions and logout/restart behavior; synthetic workspace create,
   read and version-conflict handling; ownership/role boundaries; frontend links to
   the real backend; synthetic DB write/read and audit records. Use only approved
   identities. Additional reviewer/advertiser identities require explicit approval
   before provisioning. Do not claim broader role isolation from two Operator users.

Close activation only with actual hosted results and executive acceptance. Preserve
CampaignReady=false and Delivery=NOT_SENT; no payments, outreach or live campaigns.
