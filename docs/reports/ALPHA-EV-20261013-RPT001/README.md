# Client delivery evidence

Report ID: **ALPHA-EV-20261013-RPT001**. Work covered: 12 October 2026; issued: 13 October 2026 (Asia/Taipei).

The PDF consolidates six development phases across Bliss Bot, Creative Academy and Wedding Planner. Its evidence register E01–E13 links to retained logs, phase contracts, recovery manifests and actual local UI captures.

The final retained regression run passed 673 tests with no failures or skips; the Release build had zero warnings/errors. These are retained execution results, not a new test run performed while compiling this report. Hosted acceptance and live AI validation remain pending. Screenshots use fictional local development data.

`change-inventory.csv` records 100 development files relative to base commit `2a9a7782014ef5110220af52360ad0e8f7303c7e`, excluding this report package. The containing Git commit identifies the published source and evidence together.

Verify the package from this directory using `sha256sum -c SHA256SUMS.txt`. Render the PDF using `python3 build_report.py` with Python, ReportLab and DejaVu fonts installed; regenerate checksums after intentional changes. Hashes establish file integrity, not external certification.

Credentials, private keys, database dumps and broad runtime logs are excluded.
