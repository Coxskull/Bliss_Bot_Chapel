-- Disposable gateway_test only. Synthetic integer test units, never USD or spending authority.
CREATE TABLE command_gateway.execution_control(
 tenant_id uuid PRIMARY KEY, owner_agent_id uuid NOT NULL,
 unit text NOT NULL DEFAULT 'SYNTHETIC_TEST_UNIT' CHECK(unit='SYNTHETIC_TEST_UNIT'),
 capacity bigint NOT NULL CHECK(capacity>0), held bigint NOT NULL DEFAULT 0 CHECK(held>=0), spent bigint NOT NULL DEFAULT 0 CHECK(spent>=0),
 stopped boolean NOT NULL DEFAULT false, epoch bigint NOT NULL DEFAULT 0 CHECK(epoch>=0),
 CHECK(held<=capacity-spent), FOREIGN KEY(tenant_id,owner_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id)
);
CREATE TABLE command_gateway.budget_reservations(
 message_id uuid NOT NULL, fence bigint NOT NULL CHECK(fence>0), tenant_id uuid NOT NULL, actor_agent_id uuid NOT NULL,
 units bigint NOT NULL CHECK(units>0), actual bigint CHECK(actual>=0 AND actual<=units),
 state text NOT NULL DEFAULT 'RESERVED' CHECK(state IN ('RESERVED','UNKNOWN','SETTLED')),
 CHECK((state='SETTLED' AND actual IS NOT NULL) OR (state<>'SETTLED' AND actual IS NULL)),
 PRIMARY KEY(message_id,fence), FOREIGN KEY(tenant_id,message_id) REFERENCES command_gateway.messages(tenant_id,message_id),
 FOREIGN KEY(tenant_id,actor_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id)
);
CREATE TABLE command_gateway.control_events(
 id uuid PRIMARY KEY, tenant_id uuid NOT NULL, actor_agent_id uuid NOT NULL, epoch bigint NOT NULL,
 event_type text NOT NULL CHECK(event_type IN ('STOPPED','RESERVED','UNKNOWN','SETTLED')),
 message_id uuid, fence bigint, units bigint NOT NULL CHECK(units>=0), recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 CHECK((event_type='STOPPED' AND message_id IS NULL AND fence IS NULL) OR (event_type<>'STOPPED' AND message_id IS NOT NULL AND fence IS NOT NULL)),
 FOREIGN KEY(tenant_id,actor_agent_id) REFERENCES command_gateway.agents(tenant_id,agent_id),
 FOREIGN KEY(message_id,fence) REFERENCES command_gateway.budget_reservations(message_id,fence),
 UNIQUE(message_id,fence,event_type)
);
CREATE UNIQUE INDEX control_stop_epoch ON command_gateway.control_events(tenant_id,epoch) WHERE event_type='STOPPED';
CREATE TRIGGER control_events_no_mutation BEFORE UPDATE OR DELETE ON command_gateway.control_events FOR EACH ROW EXECUTE FUNCTION command_gateway.reject_mutation();
CREATE TRIGGER control_events_no_truncate BEFORE TRUNCATE ON command_gateway.control_events FOR EACH STATEMENT EXECUTE FUNCTION command_gateway.reject_mutation();
ALTER TABLE command_gateway.delivery_state ADD COLUMN control_epoch bigint;
