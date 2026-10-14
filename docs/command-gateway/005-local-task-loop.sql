-- Disposable gateway_test only, after001–004. No hosted/EF migration.
CREATE TABLE command_gateway.task_runs(
 tenant_id uuid NOT NULL, task_id varchar(80) NOT NULL, correlation_id uuid NOT NULL,
 executive_agent_id uuid NOT NULL, engineering_agent_id uuid NOT NULL CHECK(engineering_agent_id<>executive_agent_id),
 stage text NOT NULL CHECK(stage IN ('NEW','DETECTED','ASSIGNED','CORRECTION_PROPOSED','TEST_REQUESTED','TEST_REPORTED','EVIDENCE_SUBMITTED','REVIEWED','APPROVAL_REQUESTED')),
 turns integer NOT NULL CHECK(turns BETWEEN 0 AND 8), last_message_id uuid,
 commit_hash text CHECK(commit_hash ~ '^[0-9a-f]{40}$'), evidence_hash text CHECK(evidence_hash ~ '^[0-9A-F]{64}$'),
 passed integer CHECK(passed BETWEEN 0 AND 10000), failed integer CHECK(failed BETWEEN 0 AND 10000), verdict text CHECK(verdict IN ('PASS','FAIL')),
 PRIMARY KEY(tenant_id,task_id,correlation_id),
 FOREIGN KEY(tenant_id,executive_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 FOREIGN KEY(tenant_id,engineering_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 FOREIGN KEY(tenant_id,last_message_id) REFERENCES command_gateway.messages(tenant_id,message_id)
);
CREATE TABLE command_gateway.task_events(
 message_id uuid PRIMARY KEY, tenant_id uuid NOT NULL, task_id varchar(80) NOT NULL, correlation_id uuid NOT NULL,
 actor_agent_id uuid NOT NULL, turn integer NOT NULL CHECK(turn BETWEEN 1 AND 8), stage text NOT NULL,
 commit_hash text, evidence_hash text, passed integer, failed integer, verdict text,
 recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(tenant_id,task_id,correlation_id) REFERENCES command_gateway.task_runs(tenant_id,task_id,correlation_id),
 FOREIGN KEY(tenant_id,message_id) REFERENCES command_gateway.messages(tenant_id,message_id),
 FOREIGN KEY(tenant_id,actor_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 UNIQUE(tenant_id,task_id,correlation_id,turn)
);
CREATE TRIGGER task_events_no_mutation BEFORE UPDATE OR DELETE ON command_gateway.task_events FOR EACH ROW EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER task_events_no_truncate BEFORE TRUNCATE ON command_gateway.task_events FOR EACH STATEMENT EXECUTE FUNCTION command_gateway.reject_mutation();
