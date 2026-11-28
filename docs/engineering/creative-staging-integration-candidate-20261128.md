# Creative staging integration candidate

Source baseline: 14e7c5be4629ccfbc7151a3627cec950266b7a3e (reviewed production main). Existing staging baseline: 433b144c62ed6160679ba89c8442bf6adbc127fb.

Candidate retains the exact Railway staging identity guard and opt-in fictional roundtrip wrapper, adapted to the current Dockerfile's /app/ops path. Current Railway healthcheck and fictional client are identical across baselines. Current .NET 10 image, financial UI build and creative artwork/status/video implementations retained.

Do not repoint staging yet. Registration, privacy, authentication, EF migration history, and other divergent staging-only source need reconciliation or explicit acceptance. No production transfer, STOP release, payment or dispatch. Existing staging and production branches unchanged.
