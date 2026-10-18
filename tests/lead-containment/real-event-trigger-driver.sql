\set ON_ERROR_STOP on
\echo -- REAL-POSTGRES FIXTURE START --
\ir real-event-trigger-fixture.sql
\echo -- BASELINE FUNCTION ACCEPTANCE (must fail) --
\ir ../../docs/security/lead-generation-function-security-verification.sql
\echo -- APPLY EXACT REVIEW-ONLY PROPOSAL TO DISPOSABLE DB --
\ir ../../docs/security/lead-generation-function-security-hardening-proposal.sql
\echo -- AFTER FUNCTION ACCEPTANCE (must pass) --
\ir ../../docs/security/lead-generation-function-security-verification.sql
\ir real-event-trigger-assertions.sql
\echo -- REAPPLY TO TEST IDEMPOTENCY --
\ir ../../docs/security/lead-generation-function-security-hardening-proposal.sql
\echo -- REAPPLIED FUNCTION ACCEPTANCE (must pass) --
\ir ../../docs/security/lead-generation-function-security-verification.sql
