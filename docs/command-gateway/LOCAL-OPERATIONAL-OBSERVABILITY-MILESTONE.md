# AGM-A2A-010 local operational observability

Unregistered, loopback-only and read-only. A registered tenant observer with an
explicit server policy reads one repeatable-read snapshot of synthetic capacity,
held/spent/available units, stop/epoch, bounded reservation/delivery/task counts
and the latest 20 control events. Queries are tenant-parameterized and supported
by fixture indexes in SQL006. No payloads, workers, credentials or cross-tenant
identifiers are returned.

The snapshot always has approvalGranted=false, executionAuthorized=false and
resumeAvailable=false. It cannot stop, resume, reconcile, claim, execute, notify
or approve. Synthetic units are not USD and do not authorize the $300 planning
ceiling. A snapshot can become stale immediately and is not an alerting service,
external abort, provider telemetry or production health certification.

Acceptance covers tenant isolation, registration/policy denial, missing control,
reserved/leased state, settled/delivered state, stop visibility, recent event
ordering, explicit false authority flags, wrong database refusal and absence of
state changes. No hosted migration, API/UI, actual agent/provider/Codex call,
paid activation, credential change, production, merge or deployment is included.
