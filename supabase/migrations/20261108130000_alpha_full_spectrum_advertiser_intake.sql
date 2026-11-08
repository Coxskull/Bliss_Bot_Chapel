-- Additive advertiser structured-intake contract. Review and apply only with
-- the matching Edge Function + public form release; existing rows stay intact.
begin;

alter table public.alpha_public_intake_requests
  add column if not exists budget_amount numeric(20,2),
  add column if not exists budget_amount_minor bigint,
  add column if not exists budget_currency text,
  add column if not exists market_code text,
  add column if not exists campaign_objective text,
  add column if not exists preferred_media_formats text[],
  add column if not exists audience_requirements text,
  add column if not exists preferred_languages text[],
  add column if not exists contact_consent boolean not null default false,
  add column if not exists contact_consent_at timestamptz;

alter table public.alpha_public_intake_requests
  drop constraint if exists alpha_public_intake_requests_notice_version_check;
alter table public.alpha_public_intake_requests
  add constraint alpha_public_intake_requests_notice_version_check
  check (notice_version = any (array[
    'AGM-INTEREST-2026-11-05',
    'AGM-INTEREST-2026-11-05-GHL',
    'AGM-INTEREST-2026-11-08-GHL-01',
    'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
  ]::text[]));

alter table public.alpha_public_intake_requests
  add constraint alpha_intake_contact_consent_timestamp_check
  check (contact_consent or contact_consent_at is null);

alter table public.alpha_public_intake_requests
  add constraint alpha_intake_structured_advertiser_check
  check (
    notice_version <> 'AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
    or kind <> 'advertiser'
    or (
      contact_consent is true
      and contact_consent_at is not null
      and budget_amount is not null and budget_amount > 0
      and budget_amount_minor is not null and budget_amount_minor > 0
      and budget_currency = any (array['DOP','PAB','COP','PHP','MYR','IDR']::text[])
      and market_code = any (array['SANTO_DOMINGO','PANAMA_CITY','MEDELLIN','MANILA','KUALA_LUMPUR','JAKARTA']::text[])
      and (
        (market_code='SANTO_DOMINGO' and budget_currency='DOP') or
        (market_code='PANAMA_CITY' and budget_currency='PAB') or
        (market_code='MEDELLIN' and budget_currency='COP') or
        (market_code='MANILA' and budget_currency='PHP') or
        (market_code='KUALA_LUMPUR' and budget_currency='MYR') or
        (market_code='JAKARTA' and budget_currency='IDR')
      )
      and campaign_objective = any (array[
        'awareness','engagement','website_visits','leads','sales','community_reach','other'
      ]::text[])
      and preferred_media_formats is not null
      and cardinality(preferred_media_formats) between 1 and 7
      and preferred_media_formats <@ array[
        'podcast','digital_creator','radio','television','newspaper','magazine','other'
      ]::text[]
      and array_position(preferred_media_formats, null::text) is null
      and preferred_languages is not null
      and cardinality(preferred_languages) between 1 and 12
      and array_position(preferred_languages, null::text) is null
      and array_to_string(preferred_languages, E'\n') !~ '(^|\n)[[:space:]]*(\n|$)'
      and (audience_requirements is null or char_length(audience_requirements) <= 500)
      and (
        (budget_currency='IDR' and budget_amount=trunc(budget_amount)
          and budget_amount_minor=budget_amount::bigint)
        or
        (budget_currency<>'IDR'
          and budget_amount_minor=round(budget_amount*100)::bigint)
      )
    )
  );

create or replace function public.alpha_ghl_delivery_on_intake()
returns trigger
language plpgsql
security definer
set search_path to 'pg_catalog'
as $function$
begin
  if new.kind in ('creator','advertiser')
     and new.notice_version='AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1'
     and new.contact_consent is true
  then
    insert into public.alpha_ghl_delivery(request_id) values (new.id);
  end if;
  return new;
end
$function$;

commit;
