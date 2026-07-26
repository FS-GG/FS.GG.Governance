# Quickstart

Produce a `governance-handoff` 2.x document with an active performance intent and raw
`performance-evidence-v1` sample sets, then run the normal Governance verify/ship flow. The route's
selected gates include `sdd-handoff:performance:*`.

- `warn` means Governance independently recomputed a pass.
- `block-on-ship` names every invalid binding or exceeded threshold and points to the artifact that
  must be recaptured.
- `environment-limited` means the current artifact cannot prove the declared live compositor
  requirement; run the protected capable-runner lane.

No `performanceEvidence` entries means no performance gate.
