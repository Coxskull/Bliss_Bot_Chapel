# Reviewer workspace assignment — least-privilege contract

**Status:** repository control and synthetic identity tests; **not** hosted identity-provider acceptance.

## Why

Review-only access added for Creative Academy instructor assessments and Bliss Chapel ALPHA_QA / INVENTORY_FIT decisions must not allow every user holding `bliss.reviewer` to read all advertisers' private artwork or approve it. The reviewer role alone never grants workspace membership.

## Verified identity claims

In authenticated/hosted mode, the **identity provider** must issue both:

- Role `bliss.reviewer` (or privileged `bliss.admin`), through the configured `Authentication:RoleClaimType`.
- One or more **issuer-assigned** workspace UUIDs under `Authentication:ReviewerWorkspaceClaimType` (default `bliss_review_workspace_id`). For multiple assignments use repeated token claims or an identity-provider UserInfo JSON array. A single UUID string is also accepted.

Only valid, nonempty UUID values count. Missing, malformed, wildcard and nonmatching values deny review-only evidence and decisions. The scoped authorization logic checks the **signed/authenticated user principal**, never the URL, body, client header, or an advertiser ID claim. `bliss.admin` retains the explicitly privileged administrative exception; an operator without admin/reviewer role is not entitled to instructor review.

### Routes protected by this extra scope

- `GET /api/wedding-planner/workspaces/{workspaceId}/production/review/briefs/{briefId}`
- `GET /api/wedding-planner/workspaces/{workspaceId}/production/review/briefs/{briefId}/candidates/{candidateId}/image` (private, no-store)
- `POST /api/wedding-planner/workspaces/{workspaceId}/production/briefs/{briefId}/candidates/{candidateId}/reviews`
- `POST /api/wedding-planner/workspaces/{workspaceId}/handoff/candidates/{candidateId}/approvals` **only for ALPHA_QA and INVENTORY_FIT**; advertiser decisions retain separate exact-tenant authority

Other Brand DNA, planner, candidate submission, campaign draft and handoff planning endpoints are unchanged. This does not grant campaign activation or delivery.

## Hosted deployment and review gate

1. Identity operator defines a controlled workflow to grant/revoke workspace assignments, authorized by the advertiser/Alpha privacy policy. Do **not** put assignments or test-only HTTP headers in frontend code.
2. Real OIDC issuer/token and UserInfo mapping must be tested with separate reviewer, admin, advertiser, viewer and wrong-workspace principals. Simulated `X-Test-Review-Workspaces` is strictly a fake test authentication handler and is **not** accepted by the live app.
3. Revocation of an assignment may not remove a workspace claim from an already-issued authentication cookie. Ensure sign-out/session invalidation or a suitably short cookie lifetime and document revocation delay. For immediate revocation, a server-side persistent assignment/revocation check is necessary before hosted acceptance.
4. Record permitted reviewer workspaces, access decisions, logs without private image bytes, and actual human artistic signoff separately. Customer image licensing and permission to share imagery with reviewers require explicit approval.
5. Obtain independent security/privacy review and approved migration/deployment. Synthetic CI success is not approval for production.

Until the issuer can supply controlled, correct assignments, this change intentionally **fails closed** for ordinary reviewer-role principals without a workspace assignment. The existing Development open fixture and admin exception remain.
