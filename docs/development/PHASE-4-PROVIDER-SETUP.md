# Phase 4 provider setup and zero-cost development

User preference: use free development tooling now; use OpenAI for the final
system with a **$200 total budget**. Paid calls are disabled in the current
development environment. Cursor is a development IDE, not a supported runtime
provider API for this bot. No callable Cursor integration or free hosted AI
endpoint is configured. Local provider doubles test the integration at $0;
their outputs are explicitly fixture data, not model-generated content.

## Saved inactive configuration

The environment configuration draft contains these nonsecret suggestions:

| Variable | Suggested value |
| --- | --- |
| `Runtime__ControlledAi__Enabled` | `false` |
| `Runtime__ControlledAi__Provider` | `OpenAI` |
| `Runtime__ControlledAi__Protocol` | `OpenAI` |
| `Runtime__ControlledAi__TextEndpoint` | `https://api.openai.com/v1/chat/completions` |
| `Runtime__ControlledAi__ImageEndpoint` | `https://api.openai.com/v1/images/generations` |
| `Runtime__ControlledAi__TotalBudgetUsd` | `200` |

Saving this draft does not apply or publish it. Review/save in environment
settings and publish when appropriate. Even after publication it remains
inactive until credentials, models, budgets, and Enabled=true are supplied.
No secret was requested or added for the deferred paid activation.

## Before future live activation

Choose compatible text and PNG-generating image models and set
`Runtime__ControlledAi__TextModel` / `Runtime__ControlledAi__ImageModel`.
Supply `Runtime__ControlledAi__ApiToken` through the secure environment binding
for `api.openai.com`, or the supported `_FILE` secret path outside the app.
Do not put a key into this document, browser code, or tracked settings.
The proxy destination needs network access; a secure destination binding can
add it without replacing existing allowed domains.

Set `Runtime__ControlledAi__MaxCallsPerDay`, `DailyBudgetUsd`, and justified
`TextReservationUsd` / `ImageReservationUsd` using current provider pricing,
bounded input/output, and chosen models. A reservation is a conservative cost
bound, not an actual billing receipt. The $200 limit is lifetime reserved usage,
not $200 each day. Set an independent provider billing cap as well: this
application cannot guarantee external pricing or invoice amounts. Costs above
reservation stop further queueing and dispatch pending billing reconciliation.

Only then set Enabled=true for an explicitly authorized live test. The agent
has not done that now. Default zero budgets and empty credentials/models fail
closed. Runtime protocol/endpoints/model values are not accepted from callers.
No automatic provider retry occurs, including after timeouts or restarts.

## Gateway adapter contract

For a separately authorized HTTPS gateway, set Protocol=Gateway and configure
its text/image endpoints and model names. Requests use Bearer authentication
and `Idempotency-Key: <run UUID>`, with redirects disabled. JSON:

```json
{
  "kind": "TEXT or IMAGE",
  "model": "configured-model",
  "instructions": "versioned bounded worker instructions",
  "input": { "selectedContext": "role-specific stored input snapshot" },
  "requestId": "run-uuid",
  "maxOutputTokens": 1000,
  "width": 1024,
  "height": 1024,
  "responseFormat": "base64"
}
```

Response: `text` for TEXT or `imageBase64` for IMAGE; optional
`providerRequestId`, `inputTokens`, `outputTokens`, and `actualUsd`. Missing
actualUsd stays UNRECORDED. TEXT is nonempty plain text, at most 8000 characters;
images must meet Phase 3's PNG constraints. Default response cap is 1,450,000
bytes. OpenAI responses use chat `choices[].message.content` or image
`data[].b64_json`, with usage tokens when supplied. Tool-call replies are refused.
OpenAI API usage does not provide an authoritative USD receipt here, so actual
USD is not invented from token counts. Raw provider errors are never returned.

## Startup and recovery

Activate tools, start PostgreSQL, build Release, and apply migrations before
starting the API. The registered background worker restarts with the API; its
process is not permanent state. QUEUED runs require enabled valid configuration.
Expired RUNNING/CANCEL_REQUESTED deadlines become UNKNOWN even when calls are
disabled. Recovery does not reexecute a call. Inspect the run/event ledger and
provider billing before explicitly creating a fresh request.

The browser keeps only hashed request fingerprints and UUIDs in session storage
to preserve retry keys across reload. Database run records are the authority.
“Start a fresh AI request” creates a new intent and clears sharing consent; its
next submission requires a new reservation. Cancellation retains reservations.
An image draft is attached to Creative Academy only after an explicit human
action, and remains unreviewed. Candidate creation is idempotent and separately
durable; if linking the AI event is interrupted, retry the same attachment to
reconcile it without regenerating or charging again.

## Free browser validation

From `/workspace/Bliss_Bot_Chapel`, after a Release build:

```bash
bash scripts/phase4-free-browser-check.sh
```

This uses an isolated temporary PostgreSQL database, local HTTPS gateway double,
and installed Chromium/Playwright. A test-local CA signs localhost's leaf; TLS
verification remains enabled via SSL_CERT_FILE. No certificate callback disables
verification. The script shuts down only its own processes, removes the database
and private fixture keys, and retains logs outside the checkout. Ports 5084/9444
must be free. It makes no external model calls and uses no real credentials.
