# Quickstart: Evidence-Reuse Decision Core

How to build, FSI-exercise, test, and re-bless the surface for `FS.GG.Governance.EvidenceReuse`. This is a
validation/run guide; the type and function details live in [data-model.md](./data-model.md) and
[contracts/](./contracts/).

## Prerequisites

- .NET SDK with `net10.0` (repo standard).
- The repo restores from the central feed (`Directory.Packages.props`); no new package is added by this
  feature.
- F029 (`FS.GG.Governance.FreshnessKey`) is merged and on the solution — this core references it.

## Build

```bash
dotnet build FS.GG.Governance.sln
```

Expected: the new `FS.GG.Governance.EvidenceReuse` library and `…EvidenceReuse.Tests` project compile clean
under `TreatWarningsAsErrors=true`, alongside the unchanged existing projects.

## Design-first FSI proof (Principle I)

Before writing `Model.fs` / `EvidenceReuse.fs`, draft the `.fsi` files and exercise the shape in
`scripts/prelude.fsx` (a new `// ── F030 …` section), then:

```bash
dotnet fsi scripts/prelude.fsx
```

The F030 section constructs a literal `FreshnessInputs` (reusing F029's worked example), records it under an
`EvidenceRef`, and shows: a matching candidate ⇒ `Reuse`; a one-field-changed candidate ⇒
`Recompute (InputsChanged [...])`; a different-gate candidate ⇒ `Recompute NoPriorEvidence`; and that
re-recording the same inputs refreshes (no duplicate). Expected outputs are inline comments.

## Test

```bash
dotnet test tests/FS.GG.Governance.EvidenceReuse.Tests/FS.GG.Governance.EvidenceReuse.Tests.fsproj
```

Expected: all suites green —

- **ReuseDecisionTests** — full match ⇒ `Reuse ref`; single-field change ⇒ `Recompute`, every F029 category
  (SC-001).
- **DeterminismTests** — `decide` twice byte-equal; covered-artifact reorder/dup leaves the decision
  unchanged (SC-002).
- **ExplanationTests** — every `Recompute` carries a located cause; `NoPriorEvidence` vs
  `InputsChanged [categories]` per the [decision semantics](./contracts/reuse-decision-semantics.md)
  (SC-003).
- **EmptyStoreTests** — empty store ⇒ `Recompute NoPriorEvidence` for any candidate (SC-004).
- **RecordTests** — record→reuse; refresh/de-dup most-recent-wins; independent entries; replay determinism;
  no mutation of the input store (SC-005).
- **PurityTests** — decisions/records identical across changed cwd/time/filesystem (SC-006).
- **SurfaceDriftTests** — public surface equals the committed baseline, and the assembly references only
  FreshnessKey/Config/BCL/FSharp.Core (SC-007).

## Re-bless the surface baseline (when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.EvidenceReuse.Tests/FS.GG.Governance.EvidenceReuse.Tests.fsproj
```

This rewrites `surface/FS.GG.Governance.EvidenceReuse.surface.txt`. Review the diff and commit it as part of
the Tier-1 change.

## What this feature does NOT do (out of scope — later Phase-11 rows)

- No persistence of the `ReuseStore` (no filesystem/database read or write).
- No eviction / expiry / size limit.
- No output-digest verification of reused evidence.
- No broad-route cost explanation, command-run records, provenance/attestation, ship verdict, or CLI.
