import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const read = (name) => readFileSync("scripts/" + name, "utf8");

const capture = read("task3b-bliss-pg18-capture.ps1");
const upload = read("task3b-bliss-pg18-offhost.ps1");
const restore = read("task3b-bliss-pg18-isolated-restore.ps1");
const install = read("task3b-guarded-railway-install.ps1");
const matches = (text, pattern, meaning) =>
  assert.match(text, pattern, meaning);

for (const text of [capture, upload, restore, install]) {
  matches(text, /fb33c1b7-03fb-42c8-89b1-081951a738fd/, "exact project");
  assert.doesNotMatch(text, /create_tcp_proxy|--no-ssl|--sslmode=disable|Trust Server Certificate\s*=\s*true/i,
    "must not expose or bypass database TLS");
  assert.doesNotMatch(text, /Authentication__AdvertiserRegistrationRequested\s*=\s*true|Runtime__RestrictedPreview\s*=\s*false/,
    "must not turn on public account registration");
}
matches(capture, /6aa78407-1632-48c9-8ea4-0289a7d36a5b/, "Postgres DB service pinned");
matches(capture, /b67a9836-36bf-4083-93f7-00c867b13f39/, "environment pinned");
matches(capture, /RAILWAY_SERVICE_ID/, "asserts the running container identity");
matches(capture, /pg_dump --dbname="\$db" --format=custom --no-owner --no-acl (?:2>\/dev\/null )?\| base64 -w0/, "one read-only dump");
matches(capture, /-- bash -c \$runner 2>\$null/, "Railway must accept one direct command; keep CLI stderr out of binary backup receipt");
matches(capture, /ToBase64String\(\[Text\.Encoding\]::UTF8\.GetBytes\(\$remote\)\)/, "encoded only reviewed remote script text");
matches(capture, /set -o pipefail; printf %s /, "remote internal pipefail and command source");
matches(capture, /Connection to ssh\.railway\.com closed\./, "scrub known Railway CLI transport banner");
assert.doesNotMatch(capture, /\$remote\s*\|\s*&\s*\$railway\.Source ssh/, "do not use Railway stdin shell passthrough");

matches(capture, /TASK3B_SOURCE=bliss\\\|18\\\|/, "source strict PG18 receipt");
matches(capture, /Get-Command gpg/, "GPG is mandatory");
matches(capture, /--encrypt --recipient/, "recipient encryption required");
matches(capture, /FromBase64String/, "decode only in owner process");
matches(capture, /PGDMP/, "verify pg_dump archive magic");
matches(capture, /64 \* 1024 \* 1024/, "explicit bounded capture");
matches(capture, /offhostSha256Verified = \$false/, "cannot claim unperformed upload");
matches(capture, /isolatedRestoreVerified = \$false/, "cannot claim unperformed restore");
assert.doesNotMatch(capture, /pg_dump.*--file=|Write-Output\s+\$raw|Write-Output\s+\$plain/,
  "no plaintext dump file or raw output to console");

matches(upload, /007cf7213de8a2ca89232d840afe2e1d98e2e69cef221acb6fbbbdc94c5fbd83/, "pin actual connected existing DO droplet");
matches(upload, /TASK3B_OFFHOST=REFUSED_DROPLET_IDENTITY/, "refuse unknown destination");
matches(upload, /StrictHostKeyChecking=yes/g, "SSH host key verification");
matches(upload, /Get-FileHash/, "verify local archive");
matches(upload, /sha256sum/, "verify independent offhost ciphertext");
matches(upload, /test ! -e/, "reject existing remote destination collision");
matches(upload, /dr362\|supabase\|factory/, "block unrelated DR362 destination");
matches(upload, /findmnt -T "\$existing" -n -o TARGET/, "preflight effective mounted volume");
matches(upload, /mount_target" != "\/"/, "reject root filesystem");
matches(upload, /mount_source" in \/dev\/\*/, "require disk-backed source");
matches(upload, /mount_device" != "\$root_device"/, "refuse same block device as root, including bind mounts");
matches(upload, /ext4\|xfs\|btrfs/, "require disk-backed volume filesystem");
matches(upload, /free_kib.*df -Pk/, "check free disk capacity before encrypted upload");
matches(upload, /NONROOT_ATTACHED_VOLUME_OR_SPACE_NOT_VERIFIED/, "fail closed before scp");
matches(upload, /test ! -L "\$archive"/, "refuse symlink archive collision");
assert.ok(upload.indexOf('findmnt -T "$existing"') < upload.indexOf('& $scp -o BatchMode=yes'),
  "no encrypted copy until non-root block volume verified");
matches(upload, /offhostSha256Verified = \$true/, "only after remote checksum verified");
assert.doesNotMatch(upload, /pg_dump|pg_restore|curl\s+.*(dump|backup)/,
  "upload only the encrypted file, not a new data dump");

matches(restore, /--network none/, "isolated container must not have networking");
matches(restore, /postgres:18/, "PostgreSQL 18 target");
matches(restore, /pg_restore --no-owner --no-acl --exit-on-error/, "fail closed actual restore");
matches(restore, /StandardOutput\.BaseStream\.CopyTo\(\$restoreProc\.StandardInput\.BaseStream\)/,
  "binary GPG decrypt stream directly to PG18");
matches(restore, /sourcePublicTableCount/, "table inventory checks");
matches(restore, /rm -f \$container/, "remove only its ephemeral container");
matches(restore, /\$cleanupComplete/, "container teardown required");
matches(restore, /\.recovery-evidence\.json/, "only write acceptance after restore");

matches(restore, /\[switch\] \$LocalVerificationOnly/, "explicit opt-in local restore-only flag");
matches(restore, /REQUIRES_ORIGINAL_PENDING_MANIFEST/, "local-only mode requires original pending manifest");
matches(restore, /offhostProvider -cne 'NOT_VERIFIED'/, "local-only manifest must not pretend DigitalOcean verified");
matches(restore, /offhostSha256Verified -ne \$false/, "local mode refuses fabricated off-host verification");
matches(restore, /offhostProvider -cne 'DigitalOcean'/, "unchanged default mode requires DigitalOcean");
matches(restore, /offhostRemoteChecksum -cne \$m\.archiveSha256/, "original DigitalOcean path also checks remote ciphertext");
matches(restore, /\.pending\\\.json\$', '.local-restore-evidence\.json'/, "local-only receipt is distinct from production receipt");
matches(restore, /LOCAL_ONLY_NOT_PRODUCTION/, "explicit nonproduction scope on local restore-only evidence");
matches(restore, /TASK3B_DIGITALOCEAN_OFFHOST_SHA256=NOT_VERIFIED/, "local mode never reports DigitalOcean copy PASS");
matches(restore, /TASK3B_PG18_LOCAL_ISOLATED_RESTORE=PASS/, "local acceptance has unique marker");
matches(restore, /TASK3B_ADVERTISER_SCHEMA=NOT_ESTABLISHED_ZERO_PUBLIC_TABLES/, "zero-table source is not registration-ready");
matches(restore, /TASK3B_HOSTED_CUSTOMER_REGISTRATION=NOT_TESTED/, "hosted acceptance remains held");
matches(restore, /Get-FileHash -LiteralPath \$archive -Algorithm SHA256/, "local source archive still requires true ciphertext SHA256");
assert.doesNotMatch(install,/LocalVerificationOnly|local-restore-evidence/,
  "do not let a source-only restore receipt bypass the production installer gates");

assert.doesNotMatch(restore, /pg_restore.*-h\s+\$?PGHOST|docker.*-p\s+5432|docker.*--network host/,
  "restore must never target source DB or expose test DB");

matches(install, /alpha-bliss-pg18-recovery\/v1/, "structured receipt required");
matches(install, /isolatedRestoreVerified -ne \$true/, "restore acceptance required");
matches(install, /offhostSha256Verified -ne \$true/, "DO upload acceptance required");
matches(install, /isolatedRestoreContainerRemoved -ne \$true/, "cleanup acceptance required");
matches(install, /offhostRemoteChecksum -cne \$evidence\.archiveSha256/, "remote ciphertext integrity");
matches(install, /Get-FileHash -LiteralPath \$encryptedArchivePath -Algorithm SHA256/, "local archive integrity");
matches(install, /BLISS-PG18-RESTORE-VERIFIED/, "additional typed owner attestation");
matches(install, /--migrate --initial-install-empty-hosted-db/, "only existing guarded migration");
assert.doesNotMatch(install, /generic\+file\+is\+enough|skip-backup|--force-schema/,
  "cannot use an unrelated receipt or bypass");

console.log("Task3B private Bliss PG18 backup, offhost encrypted custody, isolated restore and guarded install contract: PASS (no production write)");
