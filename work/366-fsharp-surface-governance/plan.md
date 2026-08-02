---
schemaVersion: 1
workId: 366-fsharp-surface-governance
title: General F# gate for curated signature surfaces, explicit visibility, and API documentation
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/366-fsharp-surface-governance/spec.md
sourceClarifications: work/366-fsharp-surface-governance/clarifications.md
sourceChecklist: work/366-fsharp-surface-governance/checklist.md
publicOrToolFacingImpact: true
---

# General F# gate for curated signature surfaces, explicit visibility, and API documentation Plan

Prose status: planned

## Source Snapshot
- spec: work/366-fsharp-surface-governance/spec.md sha256:9d564d1d4c98d549d17d113805e0a8679d43623af50b08d66d7ddc91a2703970 schemaVersion:1
- clarifications: work/366-fsharp-surface-governance/clarifications.md sha256:07d5fcee1b8dbc797ad675c69e780e706d2ef087b28769266a629f3408436ea3 schemaVersion:1
- checklist: work/366-fsharp-surface-governance/checklist.md sha256:0c815e6a8b96e934c47ff07434779f0a61f149f485b6d2a5f8a342c558362c88 schemaVersion:1

## Plan Scope
- Work item 366-fsharp-surface-governance is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 2.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Extend typed project sensing with an ordered compile-item model, then classify each compiled non-test `.fs` module as paired, explicitly internal, entry point, generated, governed-exempt, or public-by-default.
- PD-002 [AC-002] [FR-002] complete: Add pure design checks that derive stable per-project and per-module violations for a missing signature, an unpaired or incorrectly ordered signature, and a source/signature contract failure; findings carry only a narrow remediation, never an inferred export list.
- PD-003 [AC-003] [FR-003] complete: Parse signature declarations sufficiently to identify exported declarations without XML-doc comments and report those contracts from the signature file rather than implementation text.
- PD-004 [AC-004] [FR-004] complete: Reuse the existing package-surface baseline path for package or tool-facing projects and compose its result with the new signature findings without imposing a baseline on executable-only products.
- PD-005 [AC-005] [FR-005] complete: Publish the check through the built-in reusable profile and rule catalog as advisory, with generated guidance stating the dated promotion checkpoint and focused Rogue3-shaped fixtures.

## Contract Impact
- PC-001 [PD-001] governed F# surface profile: Expose a typed, additive design-check request and result contract through curated `.fsi` surfaces; preserve existing adapter and package-baseline contracts.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run focused DesignChecks, Findings, Ship, and BuiltIn adapter tests plus a solution build; fixtures prove library and executable classification, signatures, explicit internal modules, entry points, generated files, XML docs, and baseline applicability.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] advisoryThenBlocking: Ship the profile in advisory mode and state that it becomes blocking after the 2026-10-01 migration checkpoint, subject to fixture-backed promotion review.

## Generated View Impact
- GV-001 [PD-001] workModel: Regenerate the SDD work model and readiness receipts after authored SDD artifacts and verification evidence change; generated views remain tool-owned.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 366-fsharp-surface-governance`.
