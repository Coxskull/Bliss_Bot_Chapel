# Railway ingress policy — approval required before activation

Railway does not publish stable proxy peer IPs or CIDRs. Observed 100.64.x.x peers
must not be pinned or treated as a guaranteed network contract. See the provider's
response: https://station.railway.com/questions/asp-net-core-10-secure-forwarded-heade-506a954b

The provider states that the public edge overwrites forwarding headers, but
same-project/environment private services bypass the edge and can supply arbitrary
headers. The proposed alternative therefore trusts **all services and TCP ingress
in the selected project/environment**, not just a sampled edge IP. Project/env IDs
are deployment checks, not cryptographic request-origin proof.

This code is disabled by default. Activation requires an explicit security decision
accepting that boundary, an inventory of every environment service and TCP proxy,
and tests that caller-supplied headers are overwritten at the actual public edge.
Adding an untrusted service or TCP ingress invalidates acceptance and requires review.
Do not enable it merely to satisfy a startup check.

Required configuration after approval:

- Runtime__RailwayIngress__Enabled=true
- Runtime__RailwayIngress__AllEnvironmentServicesTrusted=true
- Runtime__RailwayIngress__ExpectedProjectId: exact approved project UUID
- Runtime__RailwayIngress__ExpectedEnvironmentId: exact approved environment UUID
- Runtime__ForwardedForHeaderName=X-Real-IP
- No Runtime__KnownProxies entries with this mutually exclusive policy

RAILWAY_PROJECT_ID and RAILWAY_ENVIRONMENT_ID must match provider-injected server
configuration. A mismatch or missing acknowledgement refuses startup. Fixed-peer
validation remains the default when this profile is disabled. Authentication,
authorization, VerifyFull database TLS, secure cookies, backup declarations and
restricted-preview controls are unchanged. Configuration and CI are not hosted
acceptance, and the profile must remain disabled until approval and hosted testing.
