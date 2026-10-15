# AGM-LAUNCH-010 R20 — targeted runtime perl-base upgrade

PR199/R19 was externally merged at f79b04641382be866991201fbea27c01ee03a01b.
Its scan reported13 scanner-fixed perl-base matches (5 critical,5 high,3 medium)
at5.36.0-7+deb12u3, with5.36.0-7+deb12u4 listed as fixed. Preserve the R19 scan,
database/image hashes and all remaining findings; no prior risk acceptance exists.

Add perl-base explicitly to the existing apt install, so Debian's configured
repositories select the available supported candidate. Log apt candidate policy
and installed version during build; require dpkg's native Debian-version comparison
to meet the known fixed floor. Do not download arbitrary packages, remove media
features, upgrade all OS packages, change the base distribution or suppress CVEs.

Rerun the existing image build/runtime/audit CI. Query the actual installed Perl
version as the configured non-root user and enforce the same floor in runtime
checks. Exercise ffmpeg libx264 plus DejaVu drawtext against an entirely synthetic
160x90 frame sequence, with ffprobe confirming H264/geometry. This occurs offline
in the existing read-only/capability-dropped/no-new-privileges container with
bounded writable /tmp; no host media, credentials, customer data or external URL.

Preserve image/source/DB provenance, package versions and full before/after
findings. Compare the exact13 perl advisory IDs and all residual severities.
If database fingerprints differ, label the scans non-identical and do not claim
the total count difference is solely caused by this package change. Scan matches
are not confirmed exploitability. Other high/critical/unfixed findings still need
vendor/runtime applicability and supported remediation review; no exception or
launch authority is granted by this narrow change or by CI execution success.

Authenticated payment/browser/real OIDC/provider/hosted acceptance remains open.
Stripe UNVERIFIED / OWNER DECISION REQUIRED. No payment/schema/identity changes,
real processing, credentials changes, production, paid activation, image push,
merge or deployment. Submit the tested draft and task-ID evidence for review.
