# Implementation Plan: Package F# Surface Producer

**Branch**: `item/396-package-fsharp-surface-producer` | **Date**: 2026-08-12 | **Spec**: `spec.md`

## Summary

Turn the existing deterministic producer executable into the supported `fsgg-fsharp-surface` global tool. Receipt evaluation stays in the established DesignChecks/Config closure; only its consumer entry route changes.

## Technical Context

**Language/Version**: F# / .NET 10
**Packaging**: NuGet global tool at a patch version.
**Testing**: Existing real-project command tests expanded to pack/install/run evidence, package-content inspection, stdout/file equality, and malformed mutation.

## Constitution Check

I/II: no new library API. III: retain the thin command edge. IV: command I/O remains at the edge. V: real temporary SDK projects and a packed tool. VI: malformed inputs continue to fail closed.

## Project Structure

```text
src/FS.GG.Governance.FSharpSurfaceCommand/
tests/FS.GG.Governance.DesignChecks.Tests/
docs/governance-design/fsharp-public-surface.md
specs/118-package-fsharp-surface/
```
