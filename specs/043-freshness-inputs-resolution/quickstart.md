# Quickstart: Per-Gate Freshness-Inputs Resolution Core

**Feature**: `043-freshness-inputs-resolution`

A validation/run guide for `FS.GG.Governance.FreshnessResolution`. Types and laws live in
[data-model.md](./data-model.md) and [contracts/](./contracts/); this file shows how to build, FSI-exercise,
test, and re-bless the surface. No implementation bodies here.

## Prerequisites

- .NET SDK `net10.0` (repo standard; `Nullable=enable`, `TreatWarningsAsErrors=true` from
  `Directory.Build.props`).
- The F041 `CacheEligibility` core (and its transitive cores) already build — this row's only project
  reference.

## Build

```bash
dotnet build src/FS.GG.Governance.FreshnessResolution/FS.GG.Governance.FreshnessResolution.fsproj
```

## FSI proof (design-first, Principle I)

The public surface is drafted in `scripts/prelude.fsx` (a new F043 section) and exercised before any `.fs` body
exists. The proof loads the packed/compiled library and the F041 core, then shows the three headline paths:

1. **Resolve** a fully-sensed gate and feed the result straight into F041:

   ```fsharp
   let report = FreshnessResolution.resolve [gate] sensed
   let cands  = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
   let elig   = CacheEligibility.evaluate cands store      // accepted without adaptation (FR-010)
   ```

2. **Unresolved** — drop a sensed fact and show the gate names exactly the gap, with `candidate = None`.
3. **Determinism** — `resolve` the same gates in two orders and show value-equal reports.

Run it:

```bash
dotnet fsi scripts/prelude.fsx
```

## Test (Expecto + FsCheck, over the PUBLIC surface)

```bash
dotnet test tests/FS.GG.Governance.FreshnessResolution.Tests/FS.GG.Governance.FreshnessResolution.Tests.fsproj
```

Tests build inputs from **real** upstream values — real F018 `Gate`s (via `Gates.buildRegistry` over real F014
facts, or hand-built `Gate` records), real F029 newtypes, and real F041 `CacheEligibility.evaluate` to prove the
candidate is accepted (Principle V — no mocks, no clock read, no hand-built oracle). Coverage maps to the
spec's user stories:

| Test file | Concern | Stories / Criteria |
|---|---|---|
| `ResolveTests.fs` | carry: identity fields verbatim, sensed fields verbatim, cost dropped | US1, SC-001 |
| `UnresolvedTests.fs` | no fabrication; names exactly the missing facts; no-hide (all gaps, never truncated) | US2, SC-002 |
| `CommandAbsenceTests.fs` | `Command = None` ⇒ resolved with absent command + version, never unresolved | US1 edge, SC-003 |
| `DeterminismTests.fs` | order-independent, byte-identical reports; no I/O | US3, SC-005 |
| `CompletenessTests.fs` | one entry per gate, attributed, ordered, duplicates preserved | US3, SC-006 |
| `TotalityTests.fs` | well-formed report across {0,1,many} gates × {all,partial,none} sensed; never throws | US3, SC-004 |
| `CandidateBridgeTests.fs` | `candidate` of resolved accepted by F041; `candidate` of unresolved = `None` | SC-007, FR-004 |
| `SensedEmptyTests.fs` | sensed-empty covered set resolves; unsensed is unresolved (never conflated) | Edge, FR-003 |
| `SurfaceDriftTests.fs` | the committed surface baseline + F041-only scope guard | Principle II, SC-008 |

Determinism, order-independence, totality, and no-hide are FsCheck properties; the worked examples in
[contracts/freshness-resolution-outcome.md](./contracts/freshness-resolution-outcome.md) are pinned.

## Re-bless the surface baseline (Tier 1)

After an intentional public-surface change:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.FreshnessResolution.Tests/FS.GG.Governance.FreshnessResolution.Tests.fsproj
```

This regenerates `surface/FS.GG.Governance.FreshnessResolution.surface.txt`. Review the diff before committing.

## Expected outcome

- `dotnet build` and `dotnet test` pass; existing builds/tests are unchanged (additive, SC-008).
- A resolved gate's `candidate` flows into F041 with no adaptation; an unresolved gate yields no candidate.
- Reports are byte-identical for value-equal inputs regardless of input order, cwd, clock, or filesystem.
