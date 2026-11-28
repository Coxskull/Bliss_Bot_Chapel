-- Disposable PostgreSQL 16/18 CI-only stub. Supabase has real pg_cron;
-- this fixture proves schedule-creation SQL and guard flow, NOT job execution.
-- Never deploy this fixture, and never run it against Supabase or production.
CREATE SCHEMA cron;
CREATE TABLE cron.job(
 jobid bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 jobname text NOT NULL UNIQUE,
 active boolean NOT NULL DEFAULT true,
 schedule text NOT NULL,
 command text NOT NULL
);
CREATE FUNCTION cron.schedule(p_name text,p_schedule text,p_command text)
RETURNS bigint LANGUAGE plpgsql AS $stub$
DECLARE v_id bigint;
BEGIN
 INSERT INTO cron.job(jobname,schedule,command)
 VALUES(p_name,p_schedule,p_command)
 RETURNING jobid INTO v_id;
 RETURN v_id;
END $stub$;
