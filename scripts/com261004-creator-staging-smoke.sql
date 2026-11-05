-- Run only in disposable agm_creator_ci; client identifiers are fictional.
CREATE ROLE agm_creator_ci_writer NOLOGIN;
CREATE ROLE agm_creator_ci_anon NOLOGIN;
GRANT USAGE ON SCHEMA agm_creator_fixture TO agm_creator_ci_writer;
GRANT EXECUTE ON FUNCTION agm_creator_fixture.register_synthetic(
  text,text,text,text,text,text,boolean,boolean) TO agm_creator_ci_writer;

DO $$
BEGIN
  IF has_table_privilege('agm_creator_ci_writer','agm_creator_fixture.intakes','SELECT')
     OR has_table_privilege('agm_creator_ci_writer','agm_creator_fixture.intakes','INSERT')
     OR has_table_privilege('agm_creator_ci_writer','agm_creator_fixture.receipts','SELECT')
  THEN RAISE EXCEPTION 'Unexpected table permissions'; END IF;
  IF has_function_privilege('agm_creator_ci_anon',
     'agm_creator_fixture.register_synthetic(text,text,text,text,text,text,boolean,boolean)','EXECUTE')
  THEN RAISE EXCEPTION 'RPC exposed to PUBLIC'; END IF;
END $$;

SET ROLE agm_creator_ci_writer;
SELECT intake_id,receipt_id,is_replay FROM agm_creator_fixture.register_synthetic(
  'testkey000001','Synthetic Creator One','  CREATOR@example.invalid ',
  'https://example.invalid/channel/podcast','Test City','podcast',true,false);
-- Writer can call narrowly granted function, not read underlying rows.
DO $$
DECLARE rejected boolean := false;
BEGIN
  BEGIN
    PERFORM 1 FROM agm_creator_fixture.intakes LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN rejected := true;
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'Direct table read unexpectedly allowed'; END IF;
END $$;
RESET ROLE;

-- Idempotent retry, including canonical email case and trim.
DO $$
DECLARE v_first uuid; v_receipt uuid; v_repeat uuid; v_replay boolean;
BEGIN
  SELECT i.id,r.id INTO v_first,v_receipt FROM agm_creator_fixture.intakes i
  JOIN agm_creator_fixture.receipts r ON r.intake_id=i.id;
  SELECT intake_id,receipt_id,is_replay INTO v_repeat,v_receipt,v_replay
    FROM agm_creator_fixture.register_synthetic(
      'testkey000001','Synthetic Creator One','creator@example.invalid',
      'https://example.invalid/channel/podcast','Test City','podcast',true,false);
  IF v_first IS DISTINCT FROM v_repeat OR NOT v_replay
  THEN RAISE EXCEPTION 'Idempotent replay invalid'; END IF;
END $$;

-- Denial must be atomic: same identity under new request key, changed
-- marketing preference, absent acknowledgement, ordinary email or URL.
DO $$
DECLARE rejected boolean;
BEGIN
  rejected:=false;
  BEGIN
    PERFORM agm_creator_fixture.register_synthetic(
      'differentkey0001','Synthetic Creator One','creator@example.invalid',
      'https://example.invalid/channel/podcast','Test City','podcast',true,false);
  EXCEPTION WHEN invalid_parameter_value THEN rejected:=true;
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'Identity collision accepted'; END IF;

  rejected:=false;
  BEGIN
    PERFORM agm_creator_fixture.register_synthetic(
      'testkey000001','Synthetic Creator One','creator@example.invalid',
      'https://example.invalid/channel/podcast','Test City','podcast',true,true);
  EXCEPTION WHEN invalid_parameter_value THEN rejected:=true;
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'Changed consent preference accepted'; END IF;

  rejected:=false;
  BEGIN
    PERFORM agm_creator_fixture.register_synthetic(
      'testkey000002','Synthetic Creator Two','creator2@example.invalid',
      'https://example.invalid/channel/podcast','Test City','podcast',false,false);
  EXCEPTION WHEN invalid_parameter_value THEN rejected:=true;
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'Consent refusal accepted'; END IF;

  rejected:=false;
  BEGIN
    PERFORM agm_creator_fixture.register_synthetic(
      'testkey000003','Synthetic Creator Three','creator@gmail.com',
      'https://example.invalid/channel/podcast','Test City','podcast',true,false);
  EXCEPTION WHEN invalid_parameter_value THEN rejected:=true;
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'Ordinary email accepted'; END IF;

  rejected:=false;
  BEGIN
    PERFORM agm_creator_fixture.register_synthetic(
      'testkey000004','Synthetic Creator Four','creator4@example.invalid',
      'https://real-site.com/channel','Test City','podcast',true,false);
  EXCEPTION WHEN invalid_parameter_value THEN rejected:=true;
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'Ordinary channel accepted'; END IF;
END $$;

DO $$
DECLARE denied boolean;
BEGIN
  denied:=false;
  BEGIN
    UPDATE agm_creator_fixture.intakes SET market='Test Elsewhere';
  EXCEPTION WHEN check_violation THEN denied:=true;
  END;
  IF NOT denied THEN RAISE EXCEPTION 'Intake mutation was allowed'; END IF;

  denied:=false;
  BEGIN
    DELETE FROM agm_creator_fixture.receipts;
  EXCEPTION WHEN check_violation THEN denied:=true;
  END;
  IF NOT denied THEN RAISE EXCEPTION 'Receipt deletion was allowed'; END IF;

  denied:=false;
  BEGIN
    TRUNCATE agm_creator_fixture.receipts;
  EXCEPTION WHEN check_violation THEN denied:=true;
  END;
  IF NOT denied THEN RAISE EXCEPTION 'Receipt truncate was allowed'; END IF;
END $$;
-- Independent admin readback, no PII printed.
DO $$
DECLARE n_intakes int; n_receipts int; n_orphans int; n_bad_consent int;
BEGIN
  SELECT count(*) INTO n_intakes FROM agm_creator_fixture.intakes;
  SELECT count(*) INTO n_receipts FROM agm_creator_fixture.receipts;
  SELECT count(*) INTO n_orphans FROM agm_creator_fixture.intakes i
    LEFT JOIN agm_creator_fixture.receipts r ON r.intake_id=i.id WHERE r.id IS NULL;
  SELECT count(*) INTO n_bad_consent FROM agm_creator_fixture.intakes i
    JOIN agm_creator_fixture.receipts r ON r.intake_id=i.id
    WHERE i.notice_version<>r.notice_version OR
          i.consent_recorded_at<>r.consent_recorded_at;
  IF n_intakes<>1 OR n_receipts<>1 OR n_orphans<>0 OR n_bad_consent<>0
  THEN RAISE EXCEPTION 'Wrong receipt readback %, %, %, %',
    n_intakes,n_receipts,n_orphans,n_bad_consent; END IF;
END $$;
SELECT 'COM-261-004 initial fictional intake and receipt readback PASS' AS result;
