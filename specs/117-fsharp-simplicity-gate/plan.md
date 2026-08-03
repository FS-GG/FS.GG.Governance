# Implementation Plan: F# Simplicity Architecture Gate

**Branch**: `item/368-fsharp-simplicity-gate` | **Date**: 2026-08-03 | **Spec**: `spec.md`

## Summary

Add a focused `FS.GG.Governance.CodeChecks` library that compiles source text through the SDK's
FSharp.Compiler.Service, derives entity/member/symbol-use and syntax-range facts, and evaluates a
typed simplicity policy. Keep repository I/O outside the library; produce deterministic findings,
structured bound justifications, opt-in thresholds, explicit primitive declarations, and guidance.

## Technical Context

**Language/Version**: F# / .NET 10

**Primary Dependencies**: SDK-bundled FSharp.Compiler.Service, FSharp.Core

**Storage**: N/A; request/response values only

**Testing**: Expecto semantic tests through the public `.fsi` API, plus packed-library smoke evidence

**Target Platform**: .NET 10 hosts supported by the repository SDK

**Project Type**: Packable library

**Performance Goals**: Deterministic analysis of fixture-scale changes; thresholds are review triggers,
not performance guarantees

**Constraints**: No filesystem/network/clock in the public analyzer; no regex-only findings; parse/type
errors fail closed; no implicit universal thresholds

**Scale/Scope**: Changed F# source documents supplied by a caller

## Constitution Check

- **I / Tier 1**: spec and plan precede `.fsi`, semantic tests, and implementation; public surface and
  baseline are included.
- **II**: all public visibility resides in `.fsi`; no top-level visibility modifiers in `.fs`.
- **III**: plain records/DUs/functions; reflection is used only by FSharp.Compiler.Service internally,
  not as the scanner's detection mechanism.
- **IV**: N/A. Analysis is a single deterministic request/response computation with no owned workflow
  state or external I/O.
- **V**: planted sources are disclosed synthetic fixtures; the packed public library smoke is the real
  production-route evidence available for a source-analysis library.
- **VI**: compiler failures are typed diagnostics and never collapse to a clean report.

Post-design re-check: PASS. The SDK compiler service remains isolated in the new adapter-layer project;
the existing kernel and findings/ship packages gain no dependency.

## Project Structure

```text
src/FS.GG.Governance.CodeChecks/
  Model.fsi / Model.fs
  CodeChecks.fsi / CodeChecks.fs
tests/FS.GG.Governance.CodeChecks.Tests/
  ArchitectureTests.fs / Main.fs
surface/FS.GG.Governance.CodeChecks.surface.txt
docs/reference/fsharp-simplicity-gate.md
specs/117-fsharp-simplicity-gate/
```

**Structure Decision**: one public analysis library and one semantic test project, integrated into the
solution. Existing generic finding/ship packages stay unchanged because their closed vocabularies model
a different routing pipeline.

## Complexity Tracking

No constitution violation. Compiler-service use is the issue-mandated semantic source and is isolated
from the pure governance kernel.
