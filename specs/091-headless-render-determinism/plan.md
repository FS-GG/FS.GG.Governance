# Implementation Plan: Headless Render-Width Test Determinism

**Branch**: `091-headless-render-determinism` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/091-headless-render-determinism/spec.md`

## Summary

The `WidthResilience` tests assert that `RichRender.emit` (Spectre.Console) emits lines fitting a
forced width across a matrix (200/80/40/20/10). They pass locally but fail in headless GitHub Actions,
so spec 089 excluded them from the publish `cli-tests` gate via `--filter "FullyQualifiedName!~WidthResilience"`
— a coverage hole tracked by issue #32.

Root cause (original hypothesis — **superseded**, see correction below): `RichRender` renders a `Table`
with `TableBorder.Rounded` (Unicode box-drawing). The test's `plainConsole` helper pins `Ansi`,
`ColorSystem`, `Out`, and `Profile.Width`, but **does not** pin the profile *capabilities* Spectre
infers from the host — `Unicode`, `Encoding`/`Legacy console` — which change how unbreakable tokens
(e.g. `src/**`-style globs, ~17–21 chars) and border glyphs are measured and wrapped.

> **CORRECTED ROOT CAUSE (#34/#37, 2026-06-29).** The glyph-measurement hypothesis above was wrong.
> A one-shot CI cell-vs-unit dump (run `28376202121`) proved the `Rounded` table renders an identical
> **20 display cells on both hosts** — measurement was never host-dependent. The real cause is ANSI
> suppression not holding: `AnsiConsole.Create` re-detects ANSI *after* `settings.Ansi <- AnsiSupport.No`,
> and under **`GITHUB_ACTIONS=true`** Spectre force-enables it. The two `console.Write(Markup …)` lines
> (title `[bold]`, exit-status `[dim]`) then leak SGR escapes (`ESC[1m … ESC[0m`) into the "plain"
> output. The escapes are invisible but inflate `String.Length` (`exit status: blocked` 20 → 28),
> tripping the per-line assertion at width 10/20 **on the GitHub Actions host only**. Reproduces locally
> with `GITHUB_ACTIONS=true dotnet test`. Fix: force `Profile.Capabilities.Ansi <- false` +
> `ColorSystem <- NoColors` *after* `Create` so `plainConsole` is genuinely ANSI-free everywhere.
> Lever 2 (boundary-aware folding assertion) was retained and is correct, but was not what greened CI.

Technical approach (both levers, per the spec's default):
1. **Pin capabilities for determinism** — extend `plainConsole` (or add a dedicated deterministic console
   builder) so the rendering profile is fully pinned: ANSI off, no color, fixed output encoding (UTF-8),
   Unicode capability fixed, legacy-console off, plus the existing forced width. Wrapping then depends
   only on inputs, not on the inferred host.
2. **Align the assertion with the real folding contract** — at a forced width narrower than the smallest
   unbreakable token, allow a line to extend to that token's boundary while still rejecting genuinely
   runaway/corrupted layout. This hardens US3 against future host/token drift even if pinning alone
   greens the matrix.
3. **Drop the exclusion** — remove the `FullyQualifiedName!~WidthResilience` filter from the publish
   `cli-tests` job so the full suite gates every publish.
4. **Close out** — close issue #32 and move its Coordination board item to **Done**.

## Technical Context

**Language/Version**: F# on .NET `net10.0`

**Primary Dependencies**: Spectre.Console `0.57.1` (rich render); Expecto + YoloDev.Expecto.TestSdk (test runner)

**Storage**: N/A

**Testing**: Expecto via `dotnet test` (`tests/FS.GG.Governance.Cli.Tests`)

**Target Platform**: Linux (GitHub Actions `ubuntu-latest`) and local developer hosts; headless determinism is the deliverable

**Project Type**: Single-project library + CLI; this feature touches test code and one CI workflow only

**Performance Goals**: N/A (test-determinism fix)

**Constraints**:
- Change confined to test code (`tests/FS.GG.Governance.Cli.Tests/`) and the publish workflow's test
  invocation (`.github/workflows/publish.yml`). No product/source change, no `.fsi`, no surface baselines.
- Must NOT green by removing/skipping the hard widths (10/20) or by host-conditional branching/`[<Ignore>]`.
- Published tool enforcement behavior (route/ship/verify verdicts) unaffected; no new published version.
- The other Spectre tests (RichRender / DegradeToPlain / Tui) must keep passing in CI.

**Scale/Scope**: ~2 files: `RenderSupport.fs` (console builder) + `WidthResilienceTests.fs` (assertion),
plus a one-line CI workflow edit; plus issue/board housekeeping.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Change classification** — **Tier 2 (internal change)**: no public API surface change, no new
  dependency, no contract change, no observable product-behavior change. (The spec floats "Tier 0";
  the constitution defines only Tier 1/Tier 2, so this is recorded as Tier 2.) Requires spec + tests;
  `.fsi` and surface baselines remain untouched. ✅
- **Principle I (Spec → FSI → Semantic tests → Implementation)** — No new public surface; the FSI sketch
  step is N/A. The deliverable *is* the semantic test discipline. ✅
- **Principle II (.fsi visibility)** — No `.fs`/`.fsi` product change; no access-modifier or surface-drift
  implications. ✅
- **Principle III (Idiomatic simplicity)** — Pinning console capabilities and a boundary-aware assertion
  are plain F#; no SRTP/reflection/custom-operator/type-provider use. ✅
- **Principle IV (Elmish/MVU boundary)** — No stateful/I/O workflow added; pure render + assert. ✅
- **Principle V (Test evidence)** — Real Spectre render against a real `ReportView` rolled by the genuine
  `Ship.rollup` (already the case in `RenderSupport.fs`); no synthetic evidence introduced. The fix makes
  the evidence honest cross-host rather than weakening it. ✅
- **Principle VI (Observability/safe failure)** — N/A (test code). ✅

**Result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/091-headless-render-determinism/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── width-resilience.md   # folding-invariant + publish-gate contract
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (repository root)

```text
tests/FS.GG.Governance.Cli.Tests/
├── RenderSupport.fs           # CHANGE: pin profile capabilities in the deterministic console builder
│                              #         (encoding/Unicode/legacy-console) alongside Ansi/Color/Width
├── WidthResilienceTests.fs    # CHANGE: assert the real folding contract (token-boundary tolerance)
│                              #         while keeping the 10/20 widths and rejecting runaway layout
└── (RichRenderTests / DegradeToPlainTests / TuiParityTests … unchanged, must stay green)

.github/workflows/
└── publish.yml                # CHANGE: drop `--filter "FullyQualifiedName!~WidthResilience"` from the
                               #         cli-tests job; update the surrounding comment to note the fix

src/FS.GG.Governance.HumanRender/
└── RichRender.fs / .fsi       # UNCHANGED — reference only (Table + TableBorder.Rounded is the renderer
                               #             under test; defaultWidth = 80)
```

**Structure Decision**: Single-project layout. The feature is intentionally narrow: two test files under
`tests/FS.GG.Governance.Cli.Tests/` and one CI workflow file. No `src/` product code changes — that
confinement is itself a requirement (FR-006) and the basis for the Tier 2 classification.

## Complexity Tracking

> No constitution violations — section intentionally empty.
