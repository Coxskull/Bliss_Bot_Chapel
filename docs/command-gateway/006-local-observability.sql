-- Disposable gateway_test only, after 001-005. Indexed reads; no hosted migration.
CREATE INDEX control_events_tenant_recent ON command_gateway.control_events(tenant_id,recorded_at DESC,id DESC);
CREATE INDEX budget_reservations_tenant_state ON command_gateway.budget_reservations(tenant_id,state);
CREATE INDEX delivery_state_tenant_state ON command_gateway.delivery_state(tenant_id,state);
