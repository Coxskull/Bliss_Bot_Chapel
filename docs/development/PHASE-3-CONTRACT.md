# Phase 3 — Academy production workspace

Confirmed Brand DNA is the authority for brand name, market, language, palette,
font, goals, and restrictions. The caller selects a production family and
provides a headline and CTA. Only an ACTIVE, physically available reference
with human-authored learn/do-not-copy instructions for that exact family is
eligible. Unsupported families do not borrow unrelated teachers. The existing
Academy deterministic brief builder is reused. No model is called.

Briefs are immutable snapshots linked to their confirmed Brand DNA revision.
They store production recipe, Quality DNA version, reference metadata snapshot,
reference file SHA-256, and recipe SHA-256. An existing brief remains readable
after brand or reference changes; a new brief requires a currently confirmed
latest brand revision. Private candidate PNG images are retained in the
database with hash, label, revision number, and production notes. Their content
is accessible only through authorized workspace routes, never public static files.
Candidate uploads are limited to 1 MiB and structurally valid, noninterlaced
8-bit grayscale/RGB/grayscale-alpha/RGBA PNGs up to 2048 by 2048 pixels.
Each candidate is a new revision of the brief, with expected-version checks.

Human reviewer/admin staff can append PASS, REVISE, or FAIL assessments to the
latest candidate. Every assessment requires rationale and independent PASS/FAIL
gates for customization, quality parity, originality, and geographic authenticity.
PASS requires all gates PASS; critical defects preclude PASS. Review records are
human observations, not automatic visual judgments. PASS is a local assessment,
not advertiser approval, Alpha campaign QA, campaign-ready status, or send authority.
Advertisers may submit candidates and read reviews but cannot record instructor
judgments. Reviewer-role access is allowed only for authorized workspace review
(existing tenant rules: chapel operator/admin staff, or a bound advertiser).

Authorization precedes resource lookup and idempotent replay. Reused operation
keys replay identical payloads and conflict on changed inputs. Revision numbers
are unique per brief/candidate. Writes and audit records commit atomically.
Review submissions require expected review version to prevent overwriting a
newer judgment. New candidates leave older reviews intact and start unreviewed.
EF refuses updates/deletes of all production records; no such API exists.

Routes under `/api/wedding-planner/workspaces/{workspaceId}/production`:
GET references; GET/POST briefs; GET brief history; POST candidates;
GET authorized candidate image; POST candidate reviews. The public production
page connects to an authorized workspace, displays confirmed direction, stores
and resumes briefs, uploads candidates, and displays review/version history.
Review controls are visible only for the review role; server policy is authority.

Tests must exercise DNA/reference provenance, unsupported/draft input refusal,
candidate/review histories, stale/retry conflicts, private assets, tenant isolation,
review authority, payload validation, concurrency, fresh/upgrade migrations,
browser save/resume, and the existing regression suite. No production deployment,
paid generation, matching/pricing changes, campaign activation, or outreach.
