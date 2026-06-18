# ADR 0001 — Structured logging: none in the kernel or the pure loop; observability via values

**Status**: Accepted · **Date**: 2026-06-18 · **Feature**: F08 (`008-effects-interpreter`)

**Resolves**: constitution `TODO(STRUCTURED_LOGGING)`; roadmap §5 ("pick a structured-logging
approach … record the choice in an ADR before F08, the first feature that does real IO").

## Context

F08 is the first feature that performs real I/O (sensing artifacts, dispatching agent reviews,
recording verdicts). Constitution Principle VI requires operationally significant events to emit
structured diagnostics with actionable context, and to distinguish a tool defect from missing or
malformed input. The constitution also requires dependencies to be minimized and the first useful
product (the kernel) to stay BCL-only, and Principle IV requires `update` to be **pure** — it
cannot log.

## Decision

**Add no structured-logging library** for F08. Observability is delivered as **values**:

- The pure `Loop.update` reifies every operationally significant event as data on the `Model`:
  `Failures` (`ArtifactUnavailable` / `ReviewDispatchFailed` / `ReviewStoreUnavailable` — each a
  fact about absent/bad input, kept distinct from a tool defect) and `Disclosures` (logged
  bypass/override justifications).
- The edge `Interpreter` emits the F06 outputs (JSON explanation, JSON contract, rendered route)
  through the injected `OutputSink` port.

A host that wants conventional logging wires it **behind the `OutputSink` and around `run`**, at
the host/CLI boundary (F12) — never in the kernel or the pure `Loop`. If a concrete logging
library is ever adopted, it is added there, in a follow-up ADR.

## Consequences

- **Zero new dependency** is preserved (FR-017, SC-009); the kernel's BCL-only hygiene (V12) and
  the new Host hygiene check (V14: Host → BCL/FSharp.Core/Kernel only) both hold.
- Principle IV is honoured: `update` stays pure (it produces observability values, it does not
  perform logging I/O).
- Principle VI is satisfied: every significant event is an inspectable, testable value;
  absent/bad input (`Failure`) is structurally distinct from a tool defect (which surfaces as a
  test failure, never a `Failure`).
- The logging-library decision is **deferred to the host (F12)**, where process lifecycle and the
  output contract actually live — recorded as a tracked follow-up, not an open TODO.

## Alternatives considered

- **Adopt `Microsoft.Extensions.Logging` or Serilog now.** Rejected: a dependency the pure loop
  cannot use and the edge does not yet need; it would pull a package into the effects shell before
  the host that owns logging exists. The `OutputSink` + value-based failure reporting cover F08.
