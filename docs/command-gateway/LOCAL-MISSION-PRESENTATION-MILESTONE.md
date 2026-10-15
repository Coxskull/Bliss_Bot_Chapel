# AGM-A2A-011: local Mission Control presentation

This milestone renders the merged local Mission Control packet as a static,
read-only HTML report. It remains an unregistered fixture component: no API
route, hosted UI, owner notification, approval endpoint or execution action.

The renderer requires trusted authenticated tenant/agent scope, unchanged
distinct Executive QA and Engineering role binding, exact task/correlation,
bounded packet size and exact SHA256. It independently parses the artifact and
requires AGM-MISSION-STATUS/1, LOCAL_MOCK and explicit false values for
independent verification, approval, execution and delivery.

History must contain one through eight unique messages in the fixed workflow,
with contiguous turns, exact message/last-message linkage, configured actor role
attribution and final immutable event equal to the snapshot. A ready review
request is recomputed only for an active eight-turn APPROVAL_REQUESTED state with
reported QA PASS, zero reported failures and bounded commit/evidence identities.
Stopped runs display their history and suppress the ready review request.

The static document uses HTML encoding and CSP default-src none. It labels commit,
evidence and test counts as reported and states that checksum correlation is not
a signature or independent artifact verification. It always displays approval,
execution and owner delivery as false. There are no links, forms, scripts,
network requests or automatic actions. A snapshot can become stale immediately;
operators must refresh from the scoped reader before acting elsewhere.

Acceptance uses disposable loopback gateway_test after SQL001-005 and verifies
valid output, tampered bytes, trusted-scope mismatch, role/actor attribution,
stopped history and no authority mutation, alongside all prior gateway tests.
No actual AI-to-AI/Codex pilot, real identity/TLS, provider call, owner delivery,
production, paid activation, credential change, merge or deployment is claimed.
