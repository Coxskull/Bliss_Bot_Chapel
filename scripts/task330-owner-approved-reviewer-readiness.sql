-- ALPHA #330: owner-approved SAME support operator as a CANDIDATE for a
-- SEPARATE, narrowly scoped read-only intake review permission.
-- READ ONLY / PRE-ENROLLMENT: NO DML, DDL, GRANTS, Auth UPDATE or UUID output.
-- Does not attest backup/restore, deploy an RPC or enable the hard source lock.
WITH candidates AS (
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
       THEN 'OWNER_APPROVED_CANDIDATE_ONLY'
       ELSE 'HOLD_IDENTITY_OR_EXISTING_REVIEW_SCOPE' END AS status,
  'BACKUP_AND_ISOLATED_RESTORE_MUST_BE_VERIFIED_SEPARATELY' AS backup_gate,
  'PRODUCTION_EDGE_SOURCE_HARD_HOLD_MUST_REMAIN_ENGAGED' AS endpoint_gate
FROM summary;
