# Data Model

- `Handoff`: v2 envelope containing evidence graph, readiness, flat governed references, diagnostic
  projections, and `Fsgg.Schemas.GovernanceHandoffPerformanceEvidence` values.
- `PerformanceGateState`: `Passed | Failed | EnvironmentLimited | NotApplicable`.
- `PerformanceEvaluation`: evidence id, state, Governance-recomputed measurements, exact failures,
  and remediation.
- Performance gate id: `sdd-handoff:performance:<work-id>:<evidence-id>`.

The evaluation is deterministic and contains no clock or I/O. Thresholds and bindings come from the
typed intent; raw samples are the measurement authority.
