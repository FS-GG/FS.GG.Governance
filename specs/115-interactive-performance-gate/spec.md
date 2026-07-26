# Feature Specification: Interactive Performance Evidence Gate

**Feature Branch**: `item/306-interactive-performance-gate`
**Created**: 2026-07-26
**Status**: Draft
**Input**: FS-GG/FS.GG.Governance#306; consumes `FS.GG.Contracts` 7.0.0 and
`governance-handoff` 2.0.0 published by FS-GG/FS.GG.SDD#700.

## Outcome

Governance independently verifies an active interactive performance declaration from raw,
auditable samples. A producer's `claimedBudgetPassed` value and precomputed measurements are
cross-checks only; neither can make failing samples pass.

## User Scenarios

### P1 — Raw samples decide the performance gate

Given an active 60 FPS intent and a `performance-evidence-v1` artifact, Governance validates the
intent/artifact binding and recomputes nearest-rank p95, p99, and maximum catch-up frames. A report
claiming success while exceeding 16.67 ms p95, 25 ms p99, or zero catch-up frames fails. Samples
inside the declared limits pass.

### P1 — Evidence cannot overclaim its environment

Evidence must bind the workload digest, workload class, target, capability, host, package versions,
measurement mode, policies, capture timestamp, currency token, and raw samples. Mixed bindings,
missing/malformed values, mismatched producer measurements, or probe/readback contamination fail.
When live compositor proof is declared, headless samples yield an explicit environment-limited
result rather than a pass.

### P2 — Non-interactive products remain unaffected

A handoff with no active performance evidence adds no performance gate. Stress/throughput workloads
remain distinct from normal-play budget verdicts.

## Requirements

### Change Classification

**Tier 1.** The handoff consumer advances from contract major 1 to 2, adopts Contracts 7.0.0, adds
public typed evaluation results, and changes observable gate projections.

### Functional Requirements

- **FR-001**: Parse only handoff contract major 2 and its object-shaped dependency edges, flat
  governed references, diagnostics, and typed performance evidence.
- **FR-002**: Validate every required intent and sample binding and reject malformed, missing,
  mixed, or mismatched data.
- **FR-002a**: Require canonical lowercase `sha256:<64 hexadecimal digits>` workload-definition
  digests, unique declared workload IDs, and equal duration/catch-up per-sample cardinality.
- **FR-003**: Recompute p95/p99 using nearest-rank over raw duration samples and maximum catch-up
  from raw catch-up samples; compare normal-play workloads to the intent thresholds.
- **FR-004**: Treat `claimedBudgetPassed` and producer measurements as non-authoritative and reject
  disagreement with recomputation.
- **FR-005**: The active 60 FPS default is p95 ≤ 16.67 ms, p99 ≤ 25 ms, catch-up = 0. A complete
  typed intent may explicitly select another positive target.
- **FR-006**: Live-compositor intent requires live-compositor mode and its required capability.
  Headless-only proof becomes `EnvironmentLimited`; readback-contaminated live proof fails.
- **FR-007**: Normal-play and stress-throughput workload classes must not be conflated. Only declared
  normal-play workloads determine the latency/catch-up verdict.
- **FR-008**: A stale diagnostic related to the evidence id invalidates that performance evidence.
- **FR-009**: Each active evidence item projects one deterministic, preselected typed gate. Failed,
  malformed, stale, or environment-limited results are `block-on-ship`; passed results are `warn`.
- **FR-010**: Gate descriptions and JSON projections carry exact failures and an actionable
  remediation pointer. Zero performance evidence produces no performance gate.

## Acceptance

- An M0-like artifact with `claimedBudgetPassed=true` but over-budget raw samples is rejected.
- An M5/M6-like artifact with valid bindings and within-budget samples passes after recomputation.
- A headless artifact cannot satisfy an intent requiring live compositor proof.
- No active performance evidence leaves non-interactive/headless products unchanged.

## Out of Scope

- Running benchmarks inside Governance; this feature consumes already captured raw evidence.
- Inventing host-specific freshness time windows absent from the producer contract.
- Changing the SDD-owned handoff or performance-artifact schemas.
