---
schemaVersion: 1
workId: 369-effect-boundary
title: F# functional-core/effect-edge governance gate
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/369-effect-boundary/spec.md
sourceClarifications: work/369-effect-boundary/clarifications.md
sourceChecklist: work/369-effect-boundary/checklist.md
publicOrToolFacingImpact: true
---

# F# functional-core/effect-edge governance gate Plan

Prose status: planned

## Source Snapshot
- spec: work/369-effect-boundary/spec.md sha256:f731900d63f56ac730f7d379189b9e183608edc045a35933335129046c93118a schemaVersion:1
- clarifications: work/369-effect-boundary/clarifications.md sha256:5a1233cb365d4c482d58ea11ca1e174c50020e3a950a99c43c3d3e2846d03527 schemaVersion:1
- checklist: work/369-effect-boundary/checklist.md sha256:9ea583eca19d1da9e832fd6f7e00a11fbc886d372b413b9ad22d322aaecee87d schemaVersion:1

## Plan Scope
- Work item 369-effect-boundary is planned from the current specification, clarification, and checklist facts.
- Requirement count: 4.
- Clarification decision count: 0.
- Checklist result count: 4.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add a signature-backed DesignChecks fact model and pure evaluator; applicability comes from `IsStatefulWorkflow`, never a literal F# type or function name.
- PD-002 [AC-001] [FR-002] complete: Keep source sensing at the edge, classify known effect symbols conservatively, and emit deterministic category/symbol diagnostics for direct effects and hidden continuation forms.
- PD-003 [AC-001] [FR-003] complete: Require declared edge interpreter delivery contracts to carry success and failure messages plus retry and idempotency semantics for repeatable effects.
- PD-004 [AC-001] [FR-004] complete: Make parsers, validators, and thin adapters explicitly non-applicable and fail closed for malformed exemptions; cover both in executable tests and a practical documentation example.

## Contract Impact
- PC-001 [PD-001] public API: `FSharpEffectBoundary.fsi` is the reusable contract: typed effect categories, delivery facts, exemption facts, project sensor, and pure evaluator.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run focused DesignChecks tests proving direct I/O, process, callback, exception, retry/idempotency, pure parser, thin adapter, and expired exemption behavior; build Debug and Release.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additiveContract: This is an additive governance API with no automatic source-name inference, so existing modules remain unaffected until a caller declares a stateful boundary.

## Generated View Impact
- GV-001 [PD-001] workModel: Commit refreshed work-model, analysis, evidence, verify, and ship views so the SDD lifecycle remains reviewable and exact-source current.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 369-effect-boundary`.
