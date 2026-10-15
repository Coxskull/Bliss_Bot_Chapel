# AGM-A2A-011: local Mission Control / owner review preparation

Reuses the externally merged task ledger and SQL001-005. No new schema, hosted
migration, API registration, UI, executor or delivery channel. Read-only
LocalGatewayMissionReview is restricted to loopback gateway_test, authenticated
registered tenant/agent and the current distinct QA/Engineering role binding.
Task ID/correlation are exact scoped selectors, not authority grants.

One repeatable-read, read-only transaction obtains the task, tenant stop flag and
at most nine events (eight allowed, overflow refused). Exact tenant/task/run
indexes bound the reads. Contiguous turns and final immutable event must agree
with the mutable task snapshot; disagreement refuses the report. No inbox
payloads, credentials or tool arguments are exposed. Stopped history remains
readable but emits no ready owner-review request. Read success does not reserve
authority: another transaction may stop the tenant immediately afterward.

An active eight-turn APPROVAL_REQUESTED run with reported QA PASS and zero failed
tests derives AGM-OWNER-REVIEW-REQUEST/1: task/correlation/request-message ID,
reported correction commit, evidence SHA256 and reported counts. LOCAL_MOCK,
approvalGranted=false, executionAuthorized=false, deliveredToOwner=false.
This is an owner review-request contract, not owner acceptance, notification or
delivery. Owner identity/channel/decision and real independent QA are separate
future integration. No approval decision endpoint or automatic next action.

AGM-MISSION-STATUS/1 preserves the bounded event history and explicit false
independentlyVerified/approval/execution/delivery flags. SHA256 covers exact UTF8
artifact bytes; deterministic repeated snapshots aid evidence correlation.
Hash equality is not a signature, authenticity proof, fetched-artifact verification
or proof of truthful tests. Fixture commits/hashes/keys are synthetic. Privileged
database administrators can bypass triggers; this is not cryptographic history.

Acceptance: exact-head Release/focused gateway tests; actual disposable PostgreSQL
checks for scope and role refusal, read-only replay, exact hash, bounded history,
stopped-history/request suppression, mutable snapshot disagreement and existing
immutable event/atomicity/concurrency rules. No full financial acceptance claim.
Actual AI-to-AI/Codex pilot, HTTP/TLS/identity/owner UI/notification, independent
review and owner acceptance remain pending separately approved isolated scope.
No paid activation, production, payments, credentials changed, merge or deploy.
AGM-LAUNCH-010/002/005 and SEC001 stay active; $300 is planning, not spend authority.
