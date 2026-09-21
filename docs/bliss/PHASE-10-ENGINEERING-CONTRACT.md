# Phase 10 Engineering Contract — Operator Observability

## Objective

Give operators a same-origin view of process health, request correlation, and
recent throttle or access events without exposing secrets or changing matching
behavior.

## Required behavior

1. Assign every response a `X-Request-Id` GUID.
2. Echo a caller-supplied `X-Request-Id` only when it is a valid GUID.
3. Publish `GET /api/runtime/status` with process status, database readiness,
   rate-limit policy, last request id, and a bounded in-memory event log.
4. Record rate-limit, unauthorized, forbidden, and unhealthy-readiness events.
5. Never persist request bodies, tokens, passwords, or identity-provider secrets.
6. Provide `POST /api/runtime/throttle-check` as a no-persistence write-quota probe,
   protected by the existing operator write policy and write rate limiter.
7. Surface a Status workspace that displays health, correlation, throttle policy,
   and recent events, including 429 toasts that include Retry-After and request id.
8. Keep health probes anonymous and the status document behind the same access
   policy as the rest of the API.

## Explicit exclusions

- log shipping, APM vendors, or metrics backends
- durable operational-event storage
- identity-provider credential display
- changes to matching, review, placement, or Alpha Auto boundaries

## Acceptance criteria

- generated and valid incoming request ids appear on responses;
- invalid incoming request ids are replaced;
- exceeding the write quota records a `RateLimited` event on `/api/runtime/status`;
- the frontend Status view renders health, request id, throttle policy, and events;
- existing authentication, CSRF, health, and business tests continue to pass.
