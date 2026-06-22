# Quickstart: Agent-Reviewed Verdict Store & Invalidation Decision Core

How to build, FSI-exercise, test, and re-bless the surface for `FS.GG.Governance.VerdictReuse`. This is a
validation/run guide; the type and function details live in [data-model.md](./data-model.md) and
[contracts/](./contracts/).

## Prerequisites

- .NET SDK with `net10.0` (repo standard).
- The repo restores from the central feed (`Directory.Packages.props`); no new package is added by this feature.
- F035 (`FS.GG.Governance.AgentReviewKey`) is merged and on the solution — this core references it (and gets
  F029's `RuleHash`/`ArtifactHash` transitively through it).

## Build

```bash
dotnet build FS.GG.Governance.sln
```

Expected: the new `FS.GG.Governance.VerdictReuse` library and `…VerdictReuse.Tests` project compile clean under
`TreatWarningsAsErrors=true`, alongside the unchanged existing projects.

## Design-first FSI proof (Principle I)

Before writing `Model.fs` / `VerdictReuse.fs`, draft the `.fsi` files and exercise the shape in
`scripts/prelude.fsx` (a new `// ── F036 …` section), then:

```bash
dotnet fsi scripts/prelude.fsx
```

The F036 section constructs a literal `AgentReviewInputs` (reusing F035's worked example), records it under a
`VerdictRef`, and shows: a matching request ⇒ `Valid`; a one-input-changed request (e.g. a model-version bump) ⇒
`Invalidated (InputsChanged [ModelVersionInput])` with `inputGroup` ⇒ `JudgeIdentity`; a prompt-hash / question
change ⇒ `PromptIdentity`; a different-check request ⇒ `Invalidated NoCachedVerdict`; and that re-recording the
same inputs refreshes (no duplicate). Expected outputs are inline comments.

## Test

```bash
dotnet test tests/FS.GG.Governance.VerdictReuse.Tests/FS.GG.Governance.VerdictReuse.Tests.fsproj
```

Expected: all suites green —

- **LookupDecisionTests** — full match ⇒ `Valid ref`; single-input change ⇒ `Invalidated`, every F035 input
  (SC-001).
- **ExplanationTests** — every `Invalidated` carries a located cause; `NoCachedVerdict` vs
  `InputsChanged [inputs]` per the [decision semantics](./contracts/lookup-decision-semantics.md); a judge change
  attributes (via `inputGroup`) to `JudgeIdentity`, a prompt change to `PromptIdentity` (SC-002, SC-003).
- **EmptyStoreTests** — empty store ⇒ `Invalidated NoCachedVerdict` for any request (SC-001/SC-003).
- **RecordTests** — record→lookup; refresh/de-dup most-recent-wins; independent entries; replay determinism; no
  mutation of the input store (SC-005).
- **DeterminismTests** — `lookup` twice byte-equal; reviewed-artifact reorder/dup leaves the decision unchanged
  (SC-004).
- **PurityTests** — decisions/records identical across changed cwd/time/filesystem (SC-006).
- **SurfaceDriftTests** — public surface equals the committed baseline, and the assembly references only
  AgentReviewKey/FreshnessKey/Config/BCL/FSharp.Core (SC-007).

## Re-bless the surface baseline (when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.VerdictReuse.Tests/FS.GG.Governance.VerdictReuse.Tests.fsproj
```

This rewrites `surface/FS.GG.Governance.VerdictReuse.surface.txt`. Review the diff and commit it as part of the
Tier-1 change.

## What this feature does NOT do (out of scope — later Phase-12 rows)

- No persistence of the `VerdictStore` (no filesystem/database read or write).
- No eviction / expiry / size limit.
- No invoking a model / running an actual review / minting or dereferencing a `VerdictRef`.
- No separation of governed artifact content from reviewer instructions (Phase 12 row 3), no recording of review
  requests / response digests (row 4), no advisory-vs-blocking promotion (row 5), no judge-vs-human calibration
  (row 6), and no CLI.
