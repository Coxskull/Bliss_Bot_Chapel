-- Apply once to the existing Bliss Bot production PostgreSQL database only
-- after approved backup and schema installation. NOT a Factory/DR362 migration.
BEGIN;
CREATE TABLE public.alpha_starter_stripe_receipts (
    event_id varchar(128) PRIMARY KEY,
    session_id varchar(128) NOT NULL UNIQUE,
    payment_intent_id varchar(128) NOT NULL UNIQUE,
    quote_reference varchar(64),
    amount_cents bigint NOT NULL CHECK (amount_cents = 14900),
    currency varchar(3) NOT NULL CHECK (currency = 'usd'),
    event_type varchar(64) NOT NULL CHECK (event_type IN ('checkout.session.completed','checkout.session.async_payment_succeeded')),
    received_at timestamptz NOT NULL DEFAULT now()
);
REVOKE ALL ON public.alpha_starter_stripe_receipts FROM PUBLIC;
COMMIT;
