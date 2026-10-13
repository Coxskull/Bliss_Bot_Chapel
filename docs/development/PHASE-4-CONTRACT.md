# Phase 4 — Controlled AI workers

Two logical roles: Concierge (text suggestions for an authorized planning
session) and Production Artist (PNG output from an immutable production brief).
They share one provider abstraction and a bounded background runner. They have
no executable tools, database mutation privileges, matching/pricing authority,
approval authority, or delivery authority. Human-controlled .NET operations
retain all authority. Prompt contract version: bliss-workers-v1.

Text context: the selected workspace's latest confirmed Brand DNA, the current
human message, and at most one explicitly selected previous successful reply
from the same planning session. Image context: the saved production recipe only;
no reference images, private candidate images, or unrelated workspace data are
sent. Each submission requires explicit data-sharing consent. Stored input
snapshots are tenant-private. AI output is untrusted plain text or validated PNG.

Provider/model/endpoints are deployment configuration, not request fields.
Adapters support a documented JSON gateway contract and OpenAI text/image APIs.
HTTPS and disabled redirects protect credentials. Requests/response sizes,
text/output bounds, timeouts, daily call quota, and a daily USD reservation budget
are configured, together with a lifetime USD cap. Default Enabled=false and zero
budget prevent live execution. The user's $200 preference applies to future
OpenAI activation; current work is zero-cost local validation only.
Admins must supply an independently justified maximum USD reservation per call
and enforce the provider's own billing cap: a local ledger cannot guarantee a
provider's billing rate. Reported usage and actual USD are recorded separately;
missing actual cost remains UNRECORDED, not zero. Reserved amounts are retained
for failures, cancellations, timeouts, and uncertain outcomes.

Durable runs: QUEUED → RUNNING → SUCCEEDED_PENDING_REVIEW / FAILED / TIMED_OUT /
CANCELLED / UNKNOWN. Cancellation of a running call requests interruption and
does not assert that the provider stopped billing. Only one execution claim per
run is allowed. An expired RUNNING deadline after a crash becomes UNKNOWN and
is not reexecuted. Provider HTTP requests are never automatically retried.
Retry keys replay identical authorized inputs, conflict on changed input, and
never invoke the provider twice for the same run. New retries need explicit new
keys and new reservations. Events and usage reservations are durable. Quota
reservation uses a serializable PostgreSQL transaction and optimistic concurrency
for test/nonrelational providers. Budget and call limits apply across workspaces.

Text suggestions remain in the AI run ledger; the existing human-message API
does not accept fabricated PLANNER actors. Image output stays private in the
run ledger. A human may explicitly attach it as a new candidate using the Phase 3
API's version and review protections. Successful generation is not approval.

API: workspace-scoped AI readiness, list/detail runs, submit text/image runs,
cancel runs, authorized output image, and explicit image-to-candidate attachment.
Public `/ai-workspace.html` shows provider readiness, session/brief selection,
data-sharing consent, run history, cancellation, suggestions, and candidate
attachment. No provider secrets or raw provider errors are exposed.

Required validation: adapter contracts (test doubles), privacy scope, tenant and
CSRF boundaries, idempotency, concurrent reservations/claims, quota/budget refusal,
usage reporting, cancellation, timeout, crash recovery, durable outputs, candidate
attachment, fresh/upgrade migrations, real-browser flow, and regression suite.
An authorized live provider request remains required for full Phase 4 acceptance;
missing credentials/model selection/budget must be reported as a blocker rather
than claimed complete. No campaign activation or outreach is included.
