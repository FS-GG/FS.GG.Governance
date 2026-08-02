---
schemaVersion: 1
workId: 369-effect-boundary
title: F# functional-core/effect-edge governance gate
stage: checklist
changeTier: tier1
status: checklistReady
sourceSpec: work/369-effect-boundary/spec.md
sourceClarifications: work/369-effect-boundary/clarifications.md
publicOrToolFacingImpact: true
---

# F# functional-core/effect-edge governance gate Checklist

Prose status: checklistReady

## Source Specification
- work/369-effect-boundary/spec.md

## Source Clarifications
- work/369-effect-boundary/clarifications.md

## Source Snapshot
- spec: work/369-effect-boundary/spec.md sha256:edf03fedd62a5d801115fbbbb70c9ce6f29a27f2530c5351898efb5dc75b5f00 schemaVersion:1
- clarifications: work/369-effect-boundary/clarifications.md sha256:2a7f5cbcfe7bba28885079a99a10d47b8195935d7239cf24c4e47a70d3e9ccc5 schemaVersion:1

## Checklist Items
- CHK-001 [FR-001] [AC-001] blocking: Requirement FR-001 is testable and linked to acceptance coverage.
- CHK-002 [FR-002] [AC-001] blocking: Requirement FR-002 is testable and linked to acceptance coverage.
- CHK-003 [FR-003] [AC-001] blocking: Requirement FR-003 is testable and linked to acceptance coverage.
- CHK-004 [FR-004] [AC-001] blocking: Requirement FR-004 is testable and linked to acceptance coverage.
- CHK-005 [FR-005] [AC-001] blocking: The declaration grammar, symbol binding, and malformed-input behavior are exact and independently testable.
- CHK-006 [FR-006] [AC-001] blocking: Production Verify fixtures cover the good architecture and every required blocking control.
- CHK-007 [FR-007] [AC-001] blocking: Lexical controls distinguish executable calls from comments, literals, and identifiers while retaining exact call identity and location.
- CHK-008 [FR-008] [AC-001] blocking: Production Verify exercises the documented example, false-positive controls, and an actual call diagnostic through real sensing.

## Review Results
- CR-001 [CHK:CHK-001] [FR-001] [AC-001] pass: Requirement FR-001 is testable and linked to acceptance coverage.
- CR-002 [CHK:CHK-002] [FR-002] [AC-001] pass: Requirement FR-002 is testable and linked to acceptance coverage.
- CR-003 [CHK:CHK-003] [FR-003] [AC-001] pass: Requirement FR-003 is testable and linked to acceptance coverage.
- CR-004 [CHK:CHK-004] [FR-004] [AC-001] pass: Requirement FR-004 is testable and linked to acceptance coverage.
- CR-005 [CHK:CHK-005] [FR-005] [AC-001] pass: Grammar and symbol-boundary outcomes have concrete positive and negative examples.
- CR-006 [CHK:CHK-006] [FR-006] [AC-001] pass: Production-route coverage names each required control and expected severity.
- CR-007 [CHK:CHK-007] [FR-007] [AC-001] pass: Unit controls cover line/block comments, string/interpolated-string contents, identifier-shaped text, and an actual call with identity/location.
- CR-008 [CHK:CHK-008] [FR-008] [AC-001] pass: Production controls assert the exact documented comment passes and a real filesystem call blocks with diagnostic evidence.

## Accepted Deferrals
No accepted checklist deferrals recorded.

## Blocking Findings
No blocking findings recorded.

## Advisory Notes
No advisory notes recorded.

## Lifecycle Notes
- Specification requirements reviewed: 8.
- Clarification decisions reviewed: 2.
- Next lifecycle action: `fsgg-sdd plan --work 369-effect-boundary`.
