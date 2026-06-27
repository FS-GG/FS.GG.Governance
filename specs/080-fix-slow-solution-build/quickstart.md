# Quickstart: Validate the Faster Solution Build

This guide reproduces the slowness, applies the fix, and checks every Success Criterion.
See [contracts/build-command.md](./contracts/build-command.md) for the command contract
and [research.md](./research.md) for the underlying measurements.

## Prerequisites

- .NET SDK 10 (`dotnet --version` → `10.0.x`).
- A warm NuGet cache: `dotnet restore FS.GG.Governance.sln` once.
- A multi-core machine (numbers below are from 24 cores / 64 GB).

## 1. Reproduce the failure (baseline)

Unbounded full-solution build — expected to **thrash and not finish** (cancel after a few
minutes):

```bash
dotnet build FS.GG.Governance.sln -c Debug --no-restore /t:Rebuild
```

Expected: a flood of `MSB6003 (Resource temporarily unavailable)` / `MSB6006 (exited with
code 134)` / `MSB4166 (Child node exited prematurely)`; the build does not complete within
10 minutes. This is the baseline the fix replaces.

## 2. Confirm the bounded build is fast (SC-001, SC-006)

```bash
dotnet build FS.GG.Governance.sln -c Debug --no-restore /t:Rebuild -m:6
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, **all 162 projects** emitted, in
**~33 s** (≫ 4× faster than the unbounded baseline; well under 5 min). Once `build.fsx`
lands, the same is reached with `dotnet fsi build.fsx`.

## 3. No-op incremental (SC-002, FR-005)

Immediately re-run without changes:

```bash
dotnet build FS.GG.Governance.sln -c Debug --no-restore -m:6
```

Expected: completes in **< 30 s** (measured ~3.3 s) — only up-to-date checks, nothing
recompiles.

## 4. Same project set, equivalent outputs (SC-004, FR-002, FR-003)

```bash
# project count is unchanged (162)
find . -name '*.fsproj' | wc -l
# every project produced its assembly
grep -c ' -> ' <build-log>
```

Expected: 162 projects, 162 emitted assemblies; no project dropped from the `.sln`.

## 5. Correctness signal intact (FR-009, C-5)

Introduce a deliberate compile error in one project, run the bounded build, and confirm it
**fails** with the usual `FS####`/MSBuild error and a non-zero exit code; revert.

## 6. Full test suite as a delivery gate (SC-003, FR-004)

```bash
dotnet fsi build.fsx test          # wraps: dotnet test FS.GG.Governance.sln -m:<N>
```

Expected: every test project executes and a final pass/fail summary prints, with **zero**
test projects hand-excluded. The SDD `fs-gg-fullstack` template-generation integration
test stays **opt-in** (default run shows its named skip; set `FSGG_REAL_EVIDENCE=1` to
exercise it) so it does not block the suite (FR-010).

## 7. Measurability (NFR-001, C-9)

The `dotnet fsi build.fsx` wrapper prints the detected core count, the chosen `-m:<N>`,
and elapsed time on every run — capture these as the demonstrable before/after record.

## Pass criteria summary

Measured on the implementer's machine (24 logical cores, ~61 GB RAM, SDK 10.0.301, warm
cache) on 2026-06-27 via `dotnet fsi build.fsx` (chosen `-m:6`):

| Check | Target | Step | Result |
|---|---|---|---|
| Clean build | < 5 min and ≥ 4× baseline | 2 | ✅ **33 s** (`Build succeeded`, 162 assemblies, 0 `MSB6003`/`MSB6006`); ≥ 18× the >10-min unbounded baseline |
| No-op incremental | < 30 s | 3 | ✅ **3.4 s** (nothing recompiled) |
| Project set | 162, unchanged | 4 | ✅ 162 `.fsproj`; `.sln` byte-identical to `main` |
| Outputs | functionally equivalent | 4 | ✅ same 162 assemblies; only scheduling changed |
| Compile error | still fails, same detail | 5 | ✅ injected error → `Build FAILED`, `FS####`, exit 1; reverted green |
| Full suite | runs to completion, 0 excluded | 6 | ✅ **65 s** build+test; 81 runs, 2287 passed, 0 failed, 1 named skip (the opt-in worked-example real build), 0 excluded |
| Observability | core count + N + elapsed printed | 7 | ✅ wrapper prints `24 logical cores -> -m:6` + elapsed ms each run |
