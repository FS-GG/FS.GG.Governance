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
- spec: work/369-effect-boundary/spec.md sha256:e879df7bb2ae8c275b1ab673d812cd588db0289ae5f12c31b5c9326c84c24c8e schemaVersion:1
- clarifications: work/369-effect-boundary/clarifications.md sha256:01fd36a08292bd9fc0f9f3aa71a147ab375f88a77aaa1d7c183e8fee14de1f07 schemaVersion:1
- checklist: work/369-effect-boundary/checklist.md sha256:337c0009b7e93edb1fe52dd7fe4c372e777894039ef265ed8251cea407478879 schemaVersion:1

## Plan Scope
- Work item 369-effect-boundary is planned from the current specification, clarification, and checklist facts.
- Requirement count: 10.
- Clarification decision count: 3.
- Checklist result count: 10.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add a signature-backed DesignChecks fact model and pure evaluator; applicability comes from `IsStatefulWorkflow`, never a literal F# type or function name.
- PD-002 [AC-001] [FR-002] complete: Keep source sensing at the edge, classify known effect symbols conservatively, and emit deterministic category/symbol diagnostics for direct effects and hidden continuation forms.
- PD-003 [AC-001] [FR-003] complete: Require declared edge interpreter delivery contracts to carry success and failure messages plus retry and idempotency semantics for repeatable effects.
- PD-004 [AC-001] [FR-004] complete: Make parsers, validators, and thin adapters explicitly non-applicable and fail closed for malformed exemptions; cover both in executable tests and a practical documentation example.
- PD-005 [AC-001] [FR-005] complete: Parse a closed exact-token grammar, bind a declaration only to its immediately following same-named `let`, stop at the next declaration at the same or lower indentation, and resolve an optional edge symbol from real compiled source.
- PD-006 [AC-001] [FR-006] complete: Drive required controls through `Interpreter.realPorts`: clean functional-core/edge, local direct I/O, missing symbol, malformed/unknown option, parser/thin adapter, callback, delivery completeness, and exemption validity.
- PD-007 [AC-001] [FR-007] complete: Mask comments and F# literal forms without changing source offsets, match only token-bounded executable call forms, and expose typed effect-call facts containing category, symbol, line, and column.
- PD-008 [AC-001] [FR-008] complete: Add unit and production controls for the documented pure-line comment, block comments, ordinary/interpolated strings, identifier-only text, and an actual filesystem call with diagnostic identity/location.
- PD-009 [AC-001] [FR-009] complete: Split lexical scanning into code and interpolated-literal states; mask delimiters/literal segments, return to code scanning inside single-brace holes, and balance nested expression braces without changing source offsets.
- PD-010 [AC-001] [FR-010] complete: Add exact focused and real Verify-route regression controls for `$"{File.WriteAllText(path, model)}"`, asserting non-empty typed call facts and the production diagnostic identity/location.

## Contract Impact
- PC-001 [PD-001] public API: `FSharpEffectBoundary.fsi` is the reusable contract: typed effect categories, delivery facts, exemption facts, project sensor, and pure evaluator.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run focused DesignChecks tests proving direct I/O, process, callback, exception, retry/idempotency, pure parser, thin adapter, and expired exemption behavior; build Debug and Release.
- VO-002 [PD-005] [PD-006] semanticTest: Run focused production Verify tests proving exact parsing and real symbol-local bodies, then require exact-head full Debug and Release CI.
- VO-003 [PD-007] [PD-008] semanticTest: Capture fresh TRX evidence for lexical call sensing and production Verify controls, re-bless the intentional public effect-call surface, and require exact-head full Debug and Release CI.
- VO-004 [PD-009] [PD-010] semanticTest: Capture fresh focused and production-route reports proving executable interpolation holes retain `File.WriteAllText` identity and cannot produce an empty direct-effect result; require full branch gates before review.

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
