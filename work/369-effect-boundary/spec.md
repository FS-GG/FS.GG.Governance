---
schemaVersion: 1
workId: 369-effect-boundary
title: "F# functional-core/effect-edge governance gate"
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# F# functional-core/effect-edge governance gate Specification

Prose status: specified

## User Value
Teams can mechanically enforce functional-core/imperative-shell semantics without relying on names such as Model, Msg, Effect, or update.

## Scope
- SB-001: Reusable F# governance gate and CLI receipt for declared stateful I/O workflow boundaries.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can teams can mechanically enforce functional-core/imperative-shell semantics without relying on names such as Model, Msg, Effect, or update.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given F# functional-core/effect-edge governance gate is available, when the user exercises it, then they can teams can mechanically enforce functional-core/imperative-shell semantics without relying on names such as Model, Msg, Effect, or update.

## Functional Requirements
- FR-001: The evaluator classifies filesystem, process, environment, clock/randomness, network, UI/host, persistence, and mutable global effects from typed facts and produces deterministic symbol-bound diagnostics. (Stories: US-001; Acceptance: AC-001)
- FR-002: A declared transition containing direct effects fails, as do callback-hidden state or exception-driven continuations for stateful workflows. (Stories: US-001; Acceptance: AC-001)
- FR-003: Edge interpreters declare effect results, retry policy, and idempotency and return success or failure messages to the pure transition. (Stories: US-001; Acceptance: AC-001)
- FR-004: Pure parsers/validators and explicitly declared thin adapters are non-applicable; exemptions require a symbol, owner, rationale, and unexpired review date. (Stories: US-001; Acceptance: AC-001)
- FR-005: Each source declaration uses an exact `key=value` grammar, binds the immediately following compiled `let` with the same symbol, and fails closed for missing symbols, unknown or duplicate options, malformed values, and unresolved edge interpreters. (Stories: US-001; Acceptance: AC-001)
- FR-006: Production Verify sensing proves both directions: a pure declared transition plus explicit edge/delivery contract passes, while symbol-local direct I/O, callbacks, malformed policy, incomplete delivery, and expired exemptions block. (Stories: US-001; Acceptance: AC-001)
- FR-007: Effect sensing recognizes executable call syntax rather than raw substrings, ignores comments, literal contents, and identifier-shaped text, and retains each matched call identity plus its one-based line and column. (Stories: US-001; Acceptance: AC-001)
- FR-008: Production Verify controls execute the documented pure-comment example and lexical false-positive fixtures as passing cases, and an actual `File.WriteAllText` call as a blocking case whose diagnostic carries call identity and location. (Stories: US-001; Acceptance: AC-001)
- FR-009: Interpolated-string literal text remains non-executable, while executable expressions inside interpolation holes are scanned as code with nested brace and literal handling; an effect call in a hole retains call identity/location and cannot yield empty direct-effect facts. (Stories: US-001; Acceptance: AC-001)
- FR-010: Focused sensor and production Verify controls prove the exact `$"{File.WriteAllText(path, model)}"` bypass is blocking while static interpolation text remains clean. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 369-effect-boundary`.
