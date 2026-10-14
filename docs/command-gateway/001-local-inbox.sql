-- AGM-LAUNCH-011 local protocol preparation ONLY. Not an EF or hosted migration.
CREATE SCHEMA command_gateway;
CREATE TABLE command_gateway.agents(tenant_id uuid NOT NULL,agent_id uuid NOT NULL,PRIMARY KEY(tenant_id,agent_id));
CREATE TABLE command_gateway.messages(
 message_id uuid PRIMARY KEY,tenant_id uuid NOT NULL,sender_agent_id uuid NOT NULL,recipient_agent_id uuid NOT NULL,
 task_id varchar(80) NOT NULL,correlation_id uuid NOT NULL,causation_id uuid CHECK(causation_id IS NULL OR causation_id<>message_id),
 idempotency_key varchar(120) NOT NULL,content_hash varchar(64) NOT NULL CHECK(content_hash ~ '^[0-9A-F]{64}$'),
 canonical_json text NOT NULL,environment text NOT NULL CHECK(environment='LOCAL_MOCK'),
 accepted_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(tenant_id,sender_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 FOREIGN KEY(tenant_id,recipient_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 UNIQUE(tenant_id,message_id),
 FOREIGN KEY(tenant_id,causation_id) REFERENCES command_gateway.messages(tenant_id,message_id),
 UNIQUE(tenant_id,sender_agent_id,idempotency_key)
);
CREATE FUNCTION command_gateway.reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Gateway accepted evidence is immutable'; END $$;
CREATE TRIGGER messages_no_mutation BEFORE UPDATE OR DELETE ON command_gateway.messages FOR EACH ROW EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER messages_no_truncate BEFORE TRUNCATE ON command_gateway.messages FOR EACH STATEMENT EXECUTE FUNCTION command_gateway.reject_mutation();
-- Dispatcher/outbox/task transitions, leases, grants and owner decisions are NOT implemented here.
-- Application adapter must atomically authorize/validate/replay-check before accepting delivery.
