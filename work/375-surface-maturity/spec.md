---
schemaVersion: 1
workId: 375-surface-maturity
title: "Configured maturity in F# public-surface receipt"
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Configured maturity in F# public-surface receipt Specification

Prose status: specified

## User Value
Consumers can distinguish advisory and blocking F# public-surface policy from a persisted Governance receipt.

## Scope
- SB-001: Correct the versioned v1 producer semantics, preserve no-verdict input states, exercise production CLI fixtures, and document compatibility plus rollout order.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can consumers can distinguish advisory and blocking F# public-surface policy from a persisted Governance receipt.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Configured maturity in F# public-surface receipt is available, when the user exercises it, then they can consumers can distinguish advisory and blocking F# public-surface policy from a persisted Governance receipt.

## Functional Requirements
- FR-001: The producer derives the receipt maturity from typed effective configuration rather than a caller supplied or constant string. (Stories: US-001; Acceptance: AC-001)
- FR-002: A block-on-ship zero-signature executable emits an applicable deterministic v1 receipt with cardinality zero and maturity block-on-ship. (Stories: US-001; Acceptance: AC-001)
- FR-003: Populated, explicit non-applicable, internal, and malformed controls retain explicit outcomes and malformed input emits no clean verdict. (Stories: US-001; Acceptance: AC-001)
- FR-004: Production CLI fixtures pin deterministic JSON for zero, populated, non-applicable, internal, and malformed states. (Stories: US-001; Acceptance: AC-001)
- FR-005: Documentation states v1 semantics, package/tool version obligation, compatibility window, and SDD#833 rollout order. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 375-surface-maturity`.
