---
schemaVersion: 1
workId: 375-surface-maturity
title: Configured maturity in F# public-surface receipt
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/375-surface-maturity/spec.md
sourceClarifications: work/375-surface-maturity/clarifications.md
sourceChecklist: work/375-surface-maturity/checklist.md
publicOrToolFacingImpact: true
---

# Configured maturity in F# public-surface receipt Plan

Prose status: planned

## Source Snapshot
- spec: work/375-surface-maturity/spec.md sha256:02479028745449640f205312c367bf5ec67c0a2ccbc662bf38a62b74ab161618 schemaVersion:1
- clarifications: work/375-surface-maturity/clarifications.md sha256:b93f64a0d51226041b6e89bc354156cd80e2f0e4d465579da42ee03fb467b763 schemaVersion:1
- checklist: work/375-surface-maturity/checklist.md sha256:7afbc87d6dbd22c7e88025d5d9525ccc413f667e2daddef5226b43c927d0d703 schemaVersion:1

## Plan Scope
- Work item 375-surface-maturity is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 0.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add a typed `Maturity` member to `FSharpSurfacePolicy.Facts`, parse only the closed Governance maturity vocabulary, default an omitted field to `Warn`, and use that typed fact to render receipt `maturity`.
- PD-002 [AC-001] [FR-002] complete: Preserve receipt schemaVersion 1 and derive the zero/one/many cardinality from compiled signatures selected by the configured glob; blocking maturity is a policy fact, not a caller argument or a cardinality inference.
- PD-003 [AC-001] [FR-003] complete: Reject malformed policy at the sensing edge so the receipt carries only input-state `fsharp.surface-malformed` evidence and no clean policy verdict; retain validated test-project non-applicability and internal-module controls.
- PD-004 [AC-001] [FR-004] complete: Execute the built `FS.GG.Governance.FSharpSurfaceCommand` against disposable SDK F# project fixtures and compare persisted receipt bytes with command JSON after normalizing the CLI line terminator.
- PD-005 [AC-001] [FR-005] complete: Document the 0.2.x producer compatibility correction, publication order, SDD#833 rollout dependency, and a consumer-side F# receipt pattern that has no maturity override.

## Contract Impact
- PC-001 [PD-001] receipt contract: `fsharp-public-surface/v1` retains its schema and field order, but `maturity` now means the effective typed configured policy. The Config and DesignChecks package surfaces grow additively and receive a compatible 0.2.0 producer version.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run typed policy and malformed-input tests, the built-command zero/populated/non-applicable/internal/malformed fixture, Config surface-drift baseline, Debug/Release solution gates, and deterministic SDD verification.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] compatibleCorrection: Existing v1 consumers remain schema-compatible; publish Config and DesignChecks 0.2.0 before SDD#833 consumes blocking maturity, and treat older hardcoded-warn producers as incapable of proving a blocking surface.

## Generated View Impact
- GV-001 [PD-001] workModel: Refresh the work model, verification, ship verdict, and generated Codex/Claude guidance after authored plan, task, and evidence facts change; no generated source implementation is edited manually.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 375-surface-maturity`.
