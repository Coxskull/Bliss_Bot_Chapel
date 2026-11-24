-- ALPHA #330: owner-approved SAME support operator as a CANDIDATE for a
-- SEPARATE, narrowly scoped read-only intake review permission.
-- READ ONLY / PRE-ENROLLMENT: NO DML, DDL, GRANTS, Auth UPDATE or UUID output.
-- Does not attest backup/restore, deploy an RPC or enable the hard source lock.
-- IMPORTANT: this query fails closed if the owner-approved exact email is
-- not supplied as a transaction-local configuration in the SAME DB session.
-- In owner-only SQL Editor, execute these statements as one transaction:
-- BEGIN READ ONLY;
-- SELECT set_config('alpha.task330_operator_email', '<CONFIRMED_EMAIL>', true);
-- [paste only the SELECT/WITH statement below]
-- ROLLBACK;
-- Do not save an operator email, UUID, service key or query result to GitHub.
WITH selected_identity AS (
  SELECT NULLIF(lower(btrim(current_setting('alpha.task330_operator_email', true))), '') AS approved_email
), candidates AS (
  SELECT g.user_id,
         u.email_confirmed_at IS NOT NULL AS email_verified,
         EXISTS (
           SELECT 1 FROM auth.sessions s
           JOIN auth.mfa_factors f ON f.id = s.factor_id
             AND f.user_id = s.user_id AND f.status::text = 'verified'
           WHERE s.user_id = g.user_id AND s.aal::text = 'aal2'
             AND (s.not_after IS NULL OR s.not_after > now())
         ) AS verified_aal2,
         u.raw_app_meta_data->>'alpha_intake_operator' = 'true' AS already_review_authorized
  FROM alpha_support_private.manual_operator_grants g
  JOIN auth.users u ON u.id = g.user_id
  JOIN selected_identity target
    ON target.approved_email IS NOT NULL
    AND lower(btrim(u.email)) = target.approved_email
  WHERE g.role = 'GENERAL_SUPPORT' AND g.enabled IS TRUE
    AND g.allowed_origin = 'https://alphacreativecontent.com'
), summary AS (
  SELECT count(*)::int AS candidates,
    count(*) FILTER (WHERE email_verified AND verified_aal2)::int AS confirmed_mfa_candidates,
    count(*) FILTER (WHERE already_review_authorized)::int AS currently_authorized_reviewers
  FROM candidates
)
SELECT candidates, confirmed_mfa_candidates, currently_authorized_reviewers,
  (SELECT count(*)::int FROM pg_proc p
    JOIN pg_namespace n ON n.oid = p.pronamespace
    WHERE n.nspname = 'public'
      AND p.proname = 'alpha_intake_operator_session_active') AS session_rpc_count,
  CASE WHEN candidates = 1 AND confirmed_mfa_candidates = 1
            AND currently_authorized_reviewers = 0
            AND (SELECT count(*) FROM auth.users
                 WHERE raw_app_meta_data->>'alpha_intake_operator'='true') = 0
       THEN 'OWNER_APPROVED_EXACT_OPERATOR_PENDING_RECOVERY'
       ELSE 'HOLD_EXACT_IDENTITY_OR_EXISTING_REVIEW_SCOPE' END AS status,
  'BACKUP_AND_ISOLATED_RESTORE_MUST_BE_VERIFIED_SEPARATELY' AS backup_gate,
  'PRODUCTION_EDGE_SOURCE_HARD_HOLD_MUST_REMAIN_ENGAGED' AS endpoint_gate
FROM summary;
