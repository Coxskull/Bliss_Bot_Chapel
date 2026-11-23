-- Additive structured advertiser intake and separate one-time contact consent.
-- Existing requests stay on hold; CRM retries require all review flags to be clear.
ALTER TABLE public.alpha_public_intake_requests
  ADD COLUMN IF NOT EXISTS budget_amount numeric(20,4),
  ADD COLUMN IF NOT EXISTS budget_amount_minor bigint,
  ADD COLUMN IF NOT EXISTS budget_currency text,
  ADD COLUMN IF NOT EXISTS market_code text,
  ADD COLUMN IF NOT EXISTS campaign_objective text,
  ADD COLUMN IF NOT EXISTS preferred_media_formats jsonb,
  ADD COLUMN IF NOT EXISTS audience_requirements text,
  ADD COLUMN IF NOT EXISTS preferred_languages jsonb,
  ADD COLUMN IF NOT EXISTS contact_consent boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS contact_consent_version text,
  ADD COLUMN IF NOT EXISTS contact_consent_at timestamptz,
  ADD COLUMN IF NOT EXISTS followup_gate text NOT NULL DEFAULT 'HOLD',
  ADD COLUMN IF NOT EXISTS business_identity_verified boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS decision_authority_verified boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS crm_suppression_checked boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS followup_approved_at timestamptz;

ALTER TABLE public.alpha_public_intake_requests
  DROP CONSTRAINT IF EXISTS alpha_public_intake_requests_notice_version_check;
ALTER TABLE public.alpha_public_intake_requests
  ADD CONSTRAINT alpha_public_intake_requests_notice_version_check
  CHECK (notice_version IN (
    'AGM-INTEREST-2026-11-05',
    'AGM-INTEREST-2026-11-05-GHL',
    'AGM-INTEREST-2026-11-08-GHL-01',
    'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
  ));

ALTER TABLE public.alpha_public_intake_receipts
  DROP CONSTRAINT IF EXISTS alpha_public_intake_receipts_notice_version_check;
ALTER TABLE public.alpha_public_intake_receipts
  ADD CONSTRAINT alpha_public_intake_receipts_notice_version_check
  CHECK (notice_version IN (
    'AGM-INTEREST-2026-11-05',
    'AGM-INTEREST-2026-11-05-GHL',
    'AGM-INTEREST-2026-11-08-GHL-01',
    'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
  ));

ALTER TABLE public.alpha_public_intake_requests
  ADD CONSTRAINT alpha_public_intake_requests_budget_first_fields_check
  CHECK (
    notice_version <> 'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
    OR kind <> 'advertiser'
    OR (
      budget_amount IS NOT NULL AND budget_amount > 0
      AND budget_amount_minor IS NOT NULL
      AND budget_amount_minor BETWEEN 1 AND 9007199254740991
      AND budget_currency IS NOT NULL
      AND market_code IS NOT NULL
      AND campaign_objective IS NOT NULL
      AND campaign_objective IN ('awareness','engagement','website_visits','leads','sales','community_reach','other')
      AND CASE WHEN jsonb_typeof(preferred_media_formats) = 'array'
        THEN jsonb_array_length(preferred_media_formats) BETWEEN 1 AND 7 ELSE false END
      AND (audience_requirements IS NULL OR char_length(audience_requirements) <= 500)
      AND CASE WHEN jsonb_typeof(preferred_languages) = 'array'
        THEN jsonb_array_length(preferred_languages) BETWEEN 1 AND 12 ELSE false END
      AND (market_code,budget_currency) IN (
        ('SANTO_DOMINGO','DOP'),('PANAMA_CITY','PAB'),('MEDELLIN','COP'),
        ('MANILA','PHP'),('KUALA_LUMPUR','MYR'),('JAKARTA','IDR')
      )
    )
  );

ALTER TABLE public.alpha_public_intake_requests
  ADD CONSTRAINT alpha_public_intake_requests_contact_consent_check
  CHECK (
    notice_version <> 'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
    OR kind = 'privacy'
    OR (
      contact_consent = true
      AND contact_consent_version = notice_version
      AND contact_consent_at IS NOT NULL
    )
  );

ALTER TABLE public.alpha_public_intake_requests
  ADD CONSTRAINT alpha_public_intake_requests_followup_gate_check
  CHECK (
    (followup_gate = 'HOLD' AND followup_approved_at IS NULL)
    OR
    (followup_gate = 'CLEARED'
      AND contact_consent = true
      AND contact_consent_version = notice_version
      AND contact_consent_at IS NOT NULL
      AND business_identity_verified = true
      AND decision_authority_verified = true
      AND crm_suppression_checked = true
      AND followup_approved_at IS NOT NULL)
  );

CREATE OR REPLACE FUNCTION public.alpha_ghl_delivery_on_intake()
RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
SET search_path = pg_catalog AS $$
BEGIN
  IF NEW.kind IN ('creator','advertiser')
     AND NEW.notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
     AND NEW.contact_consent = true
     AND NEW.contact_consent_version = NEW.notice_version
  THEN
    INSERT INTO public.alpha_ghl_delivery(request_id)
    VALUES(NEW.id)
    ON CONFLICT (request_id) DO NOTHING;
  END IF;
  RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.alpha_ghl_claim(p_request_id uuid)
RETURNS TABLE(request_id uuid, claim_token uuid)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
BEGIN
 RETURN QUERY
 UPDATE public.alpha_ghl_delivery d
 SET state='SENDING',
     attempts=d.attempts+1,
     claim_token=gen_random_uuid(),
     last_attempt_at=clock_timestamp(),
     next_attempt_at=clock_timestamp()+interval '5 minutes'
 WHERE d.request_id=p_request_id
   AND d.attempts<5
   AND EXISTS (
     SELECT 1
     FROM public.alpha_public_intake_requests r
     WHERE r.id=d.request_id
       AND r.kind IN ('creator','advertiser')
       AND r.notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
       AND r.contact_consent=true
       AND r.contact_consent_version=r.notice_version
       AND r.contact_consent_at IS NOT NULL
       AND r.followup_gate='CLEARED'
       AND r.business_identity_verified=true
       AND r.decision_authority_verified=true
       AND r.crm_suppression_checked=true
       AND r.followup_approved_at IS NOT NULL
   )
   AND (
     d.state='PENDING'
     OR (d.state IN ('RETRY','SENDING') AND d.next_attempt_at<=clock_timestamp())
   )
 RETURNING d.request_id,d.claim_token;
END;
$$;

REVOKE EXECUTE ON FUNCTION public.alpha_ghl_delivery_on_intake() FROM PUBLIC,anon,authenticated;
REVOKE EXECUTE ON FUNCTION public.alpha_ghl_claim(uuid) FROM PUBLIC,anon,authenticated;
GRANT EXECUTE ON FUNCTION public.alpha_ghl_claim(uuid) TO service_role;
