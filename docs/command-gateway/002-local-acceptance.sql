-- Apply after001 in disposable gateway_test ONLY; not an EF/hosted migration.
CREATE TABLE command_gateway.acceptance_audit(
 message_id uuid PRIMARY KEY,tenant_id uuid NOT NULL,actor_agent_id uuid NOT NULL,
 event_type text NOT NULL CHECK(event_type='MESSAGE_ACCEPTED'),content_hash varchar(64) NOT NULL,
 recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(tenant_id,message_id) REFERENCES command_gateway.messages(tenant_id,message_id),
 FOREIGN KEY(tenant_id,actor_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id)
);
CREATE TABLE command_gateway.outbox_intents(
 message_id uuid PRIMARY KEY,tenant_id uuid NOT NULL,recipient_agent_id uuid NOT NULL,
 created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(tenant_id,message_id) REFERENCES command_gateway.messages(tenant_id,message_id),
 FOREIGN KEY(tenant_id,recipient_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id)
);
CREATE TRIGGER audit_no_mutation BEFORE UPDATE OR DELETE ON command_gateway.acceptance_audit FOR EACH ROW EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER audit_no_truncate BEFORE TRUNCATE ON command_gateway.acceptance_audit FOR EACH STATEMENT EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER outbox_no_mutation BEFORE UPDATE OR DELETE ON command_gateway.outbox_intents FOR EACH ROW EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER outbox_no_truncate BEFORE TRUNCATE ON command_gateway.outbox_intents FOR EACH STATEMENT EXECUTE FUNCTION command_gateway.reject_mutation();
