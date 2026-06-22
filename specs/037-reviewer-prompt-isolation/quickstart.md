# Quickstart: Reviewer-Prompt Isolation — Governed-Artifact-as-Data Core

How to build, FSI-exercise, test, and re-bless the surface for `FS.GG.Governance.PromptIsolation`. This is a
validation/run guide; the type and function details live in [data-model.md](./data-model.md) and
[contracts/](./contracts/).

## Prerequisites

- .NET SDK with `net10.0` (repo standard).
- The repo restores from the central feed (`Directory.Packages.props`); no new package is added by this feature.
- F035 (`FS.GG.Governance.AgentReviewKey`) is merged and on the solution — this core references it for
  `QuestionText` (the instruction channel) and gets F029's `ArtifactHash` (the digest form) transitively through
  it.

## Build

```bash
dotnet build FS.GG.Governance.sln
```

Expected: the new `FS.GG.Governance.PromptIsolation` library and `…PromptIsolation.Tests` project compile clean
under `TreatWarningsAsErrors=true`, alongside the unchanged existing projects.

## Design-first FSI proof (Principle I)

Before writing `Model.fs` / `PromptIsolation.fs`, draft the `.fsi` files and exercise the shape in
`scripts/prelude.fsx` (a new `// ── F037 …` section), then:

```bash
dotnet fsi scripts/prelude.fsx
```

The F037 section assembles a literal `ReviewRequest` from a `QuestionText` instruction and a sequence of
`ArtifactPayload`s — a bounded excerpt whose content imitates an instruction, a digest-only artifact (reusing an
F029 `ArtifactHash`), and an empty excerpt — and shows: the captured excerpt is within its bound and marked
`Truncated`; an at-bound excerpt is `Whole`; the rendered prompt places the instruction-imitating content wholly
inside its length-prefixed `exc` payload (it never reaches `instr=…`); and re-rendering the same request is
byte-identical. Expected outputs are inline comments, matching the worked example in
[contracts/render-format.md](./contracts/render-format.md).

## Test

```bash
dotnet test tests/FS.GG.Governance.PromptIsolation.Tests/FS.GG.Governance.PromptIsolation.Tests.fsproj
```

Expected: all suites green —

- **ChannelSeparationTests** — `assemble i arts` puts `i` in the instruction channel and `arts` in the data
  channel; instruction-imitating artifact content stays in the data channel and never alters `Instructions`
  (SC-001).
- **BoundedCaptureTests** — at/under bound ⇒ whole + `Whole`; over bound ⇒ prefix + `Truncated`; never
  over-bound (property); boundary exactness; digest-only carries no bytes (SC-002).
- **RenderFenceTests** — content containing the fence markers / separators / tag characters / instruction-imitating
  text stays inside its length-prefixed segment; `render a = render b ⇒ a = b` (injectivity property) per the
  [render format](./contracts/render-format.md) (SC-003).
- **DeterminismTests** — `render` twice byte-equal; artifact order and duplicates are preserved (changing them
  changes the rendering) (SC-004).
- **PurityTests** — requests/renderings identical across changed cwd/time/filesystem; no model invoked, no bytes
  hashed, nothing persisted (SC-005).
- **EdgeCaseTests** — empty excerpt (`exc=w,0:`), zero bound, zero artifacts (`art=0;`), empty digest (`dig=0:`)
  each render to a distinct, unambiguous form (SC-002, SC-003).
- **SurfaceDriftTests** — public surface equals the committed baseline, and the assembly references only
  AgentReviewKey/FreshnessKey/Config/BCL/FSharp.Core (SC-006).

## Re-bless the surface baseline (when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.PromptIsolation.Tests/FS.GG.Governance.PromptIsolation.Tests.fsproj
```

This rewrites `surface/FS.GG.Governance.PromptIsolation.surface.txt`. Review the diff and commit it as part of the
Tier-1 change.

## What this feature does NOT do (out of scope — later Phase-12 rows)

- No invoking a model / running an actual review; no reading an artifact from disk and computing its digest
  (digests are supplied tokens).
- No verdict, cache key, or verdict store / lookup / invalidation (F035 / F036 own those).
- No review record / provenance of requests and response digests (row 4), no advisory-vs-blocking promotion (row
  5), no judge-vs-human calibration (row 6).
- No persistence and no CLI.
