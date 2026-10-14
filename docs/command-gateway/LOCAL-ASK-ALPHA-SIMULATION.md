# AGM-A2A-006 local Ask Alpha customer simulation

Reuses the existing fixed ConversationLaboratory and production Ask Alpha reply
logic. A trusted local policy must grant the exact current synthetic scenario ID
set and a bounded maximum. Missing, extra or unbounded scenarios are refused.
There is no arbitrary prompt, customer list, contact route, network call, delivery
adapter, write to a prospect or mutable production state.

The report contains scenario metadata, signals, pass/findings and a deterministic
SHA256 over the exact local evidence fields. It always records LOCAL_SYNTHETIC,
NOT_SENT, customerContacted=false, outreachAuthorized=false,
commitmentAuthorized=false and humanReviewRequired=true. The hash is evidence
correlation, not a signature, independent QA or owner acceptance.

Acceptance requires all fixed pricing, identity, suppressed-route, preview,
economics, repeated explanation and interest scenarios to pass against current
Ask Alpha behavior. This is scripted local simulation, not a real customer,
autonomous agent exchange, campaign test, outreach approval or conversion proof.
No provider/model call, paid activation, credential change, production, merge or
deployment is included.
