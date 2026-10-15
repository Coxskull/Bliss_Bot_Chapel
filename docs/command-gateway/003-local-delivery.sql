-- Disposable gateway_test only, after001/002. No EF/hosted migration.
CREATE TABLE command_gateway.delivery_state(
 message_id uuid PRIMARY KEY,tenant_id uuid NOT NULL,
 state text NOT NULL DEFAULT 'PENDING' CHECK(state IN ('PENDING','LEASED','DELIVERED','CANCELED','DEAD_LETTER','EXPIRED','UNKNOWN')),
 attempts integer NOT NULL DEFAULT 0 CHECK(attempts BETWEEN 0 AND 3),fence bigint NOT NULL DEFAULT 0 CHECK(fence>=0),
 worker_id uuid,lease_until timestamptz,
 CHECK((state='LEASED' AND worker_id IS NOT NULL AND lease_until IS NOT NULL AND attempts>0) OR (state<>'LEASED' AND worker_id IS NULL AND lease_until IS NULL)),
 FOREIGN KEY(tenant_id,message_id) REFERENCES command_gateway.messages(tenant_id,message_id)
);
CREATE TABLE command_gateway.delivery_events(
 id uuid PRIMARY KEY,message_id uuid NOT NULL,tenant_id uuid NOT NULL,actor_agent_id uuid NOT NULL,
 fence bigint NOT NULL,event_type text NOT NULL CHECK(event_type IN ('LEASED','DELIVERED','CANCELED','DEAD_LETTER','EXPIRED','UNKNOWN','PENDING')),recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(tenant_id,message_id) REFERENCES command_gateway.messages(tenant_id,message_id),
 FOREIGN KEY(tenant_id,actor_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 UNIQUE(message_id,fence,event_type)
);
CREATE TRIGGER delivery_events_no_mutation BEFORE UPDATE OR DELETE ON command_gateway.delivery_events FOR EACH ROW EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER delivery_events_no_truncate BEFORE TRUNCATE ON command_gateway.delivery_events FOR EACH STATEMENT EXECUTE FUNCTION command_gateway.reject_mutation();
