# Phase 1 Data Model: Headless Render-Width Test Determinism

This feature adds no product domain types. The "entities" below are the test-side concepts the design
operates on. They live entirely in `tests/FS.GG.Governance.Cli.Tests/` and the publish workflow.

## Entity: Width matrix case

The set of forced console widths the rich-render surface is exercised against.

| Field | Value | Notes |
|-------|-------|-------|
| widths | `[200; 80; 40; 20; 10]` | Unchanged. The 10/20 cases are the ones that fail today and MUST stay (FR-004, SC-004). |
| safe-default case | `RichRender.defaultWidth` (= 80) | Asserted `> 0`; unchanged (edge case: unknown/unset width). |

**Validation rules**: matrix is fixed (no host-conditional add/remove); no `[<Ignore>]`/skip on any case.

## Entity: Deterministic test console

The `IAnsiConsole` + `StringWriter` pair the width suite renders into. Built by the (extended)
`RenderSupport` helper.

| Capability | Pinned value | Already pinned today? | Wrap-affecting? |
|------------|--------------|-----------------------|-----------------|
| Ansi | `AnsiSupport.No` | Yes | indirectly (escape counting) |
| ColorSystem | `ColorSystemSupport.NoColors` | Yes | indirectly |
| Output sink | `AnsiConsoleOutput(StringWriter)` | Yes | yes (captures emitted text) |
| Output encoding | UTF-8 (fixed via the writer) | **No — to add** | yes |
| Profile.Width | the matrix width | Yes | yes |
| Capabilities.Unicode | fixed (`true`) | **No — to add** | yes (Rounded border + token measurement) |
| Capabilities.Legacy | `false` | **No — to add** | yes |

**Validation rules**: the same pinned values are used locally and in CI (no environment branching). The
console is a pure function of `width` → identical render on every host (FR-001, FR-002).

**State transitions**: none (constructed per test case, written once, read once).

## Entity: Folding-invariant assertion

The per-line check applied to the captured output.

| Field | Definition |
|-------|------------|
| `longestToken` | length of the longest unbreakable token in the rendered content (derived from the same `ReportView`/inputs the renderer receives), accounting for border/indent contribution |
| `bound` | `max(width, longestToken)` |
| per-line assert | `line.Length <= bound` |
| non-triviality | output length `> 0` retained; at fit-widths (200/80/40) `bound = width` so the check is identical to today |

**Validation rules** (FR-003, FR-007):
- MUST reject genuinely runaway/corrupted layout (a line exceeding `bound`).
- MUST NOT weaken to "non-empty only".
- MUST NOT loosen any unrelated assertion in the suite.

## Entity: Publish `cli-tests` gate invocation

The CI step that runs the suite.

| Field | Before | After |
|-------|--------|-------|
| `dotnet test` filter | `--filter "FullyQualifiedName!~WidthResilience"` | *(removed)* |
| suite coverage | all EXCEPT WidthResilience | full suite incl. WidthResilience |
| locked restore / enforcement-smoke / publish ordering | unchanged | unchanged |

**Validation rules**: removing the filter is the sole workflow delta (FR-005); a deliberately-broken
width assertion MUST turn the gate red (SC-005).
