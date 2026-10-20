# Phase 4C — Repository-only logical backup prototype
Evidence: ALPHA-EV-20261020-X4K8RM

Scope: **non-production only**. No production connections, scheduled tasks, storage provisioning, payments, deployments, migrations or restoration.

## Components
- `scripts/phase4c-nonprod-backup.sh`: uses PostgreSQL `pg_dump --format=custom`, client-side `age` encryption, and SHA-256 checksum. Requires explicit development/test/ci environment.
- `tests/phase4c-backup-guard.sh`: checks syntax and rejects `BACKUP_ENVIRONMENT=production` before any database access.

## Prerequisites
Use PostgreSQL 18 client tools for a PostgreSQL 18 source, `age`, `sha256sum`, a dedicated non-production database, and an age recipient public key. Never commit passwords, private keys, or database URLs. Run only against synthetic non-production data.

## Non-production validation
```bash
bash tests/phase4c-backup-guard.sh
BACKUP_ENVIRONMENT=test BACKUP_DATABASE_URL='postgresql://...' BACKUP_OUTPUT_DIR='./tmp-backups' BACKUP_ENCRYPTION_RECIPIENT='age1...' bash scripts/phase4c-nonprod-backup.sh
sha256sum --check ./tmp-backups/*.sha256
# In a disposable, isolated PostgreSQL 18 test instance only:
# age --decrypt --identity <test-key-file> -o restored.dump <test-backup.dump.age>
# pg_restore --dbname=<disposable-test-db> --no-owner --no-acl restored.dump
```

## Acceptance still required
1. Verify actual current-plan scheduler and independent object storage eligibility, egress and total monthly cost.
2. Implement secure object-store upload, immutable retention where supported, expiry, alerting and audit evidence (not included in this prototype).
3. Verify encryption key escrow/recovery, least-privilege access, and upload/checksum validation.
4. Complete a synthetic isolated restore with row-count/content validation and timed RPO/RTO evidence.
5. Obtain **separate explicit owner approval** before any production backup connection or configuration.
6. Obtain **another separate authorization** for restoration of an authentic production backup in isolation.

Daily dumps provide at best approximately 24-hour RPO if jobs succeed; no PITR. Actual RTO must be measured. No recovery acceptance or public release is implied.
