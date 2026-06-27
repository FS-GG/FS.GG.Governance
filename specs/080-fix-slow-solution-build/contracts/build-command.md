# Contract: Documented Build / Test Command

The user-facing "interface" of this feature is the **standard documented command** a
contributor or CI runs to build and test the whole solution. This contract freezes its
shape so docs, CI, and the guard test agree.

## The command

Build the whole solution (US1):

```bash
dotnet fsi build.fsx               # default verb = build
# or, equivalently, the explicit form it wraps:
dotnet build FS.GG.Governance.sln -m:<N>
```

Run the whole test suite as the delivery gate (US2):

```bash
dotnet fsi build.fsx test
# wraps:
dotnet test FS.GG.Governance.sln -m:<N>
```

Where `<N>` is the **bounded, hardware-derived** node count the wrapper computes (never
unlimited — research D2/D3/D4).

## Guarantees (what the contract promises)

| ID | Guarantee | Tied to |
|---|---|---|
| C-1 | The command builds **all 162 projects** of `FS.GG.Governance.sln`; no project is excluded to gain speed. | FR-002, SC-004 |
| C-2 | On an 8+ core machine with warm cache, a clean build completes in **< 5 min** and **≥ 4×** faster than the unbounded baseline. | SC-001, SC-006 |
| C-3 | A no-op incremental rebuild completes in **< 30 s**. | SC-002, FR-005 |
| C-4 | The full **test** suite runs to completion (every test project executes, final summary printed) with **zero** hand-excluded projects, within a single CI job budget (target < 20 min build+test). | SC-003, FR-004 |
| C-5 | A real compile error still **fails** the command with the same MSBuild diagnostic form/detail. | FR-009 |
| C-6 | Build outputs are **functionally equivalent** to the pre-fix build (same assemblies/behavior). | FR-003 |
| C-7 | Achievable with a **normal checkout** and **no per-developer machine tuning**; the bound is **checked in** and applies uniformly to every contributor and CI (no per-machine env var / local override). | FR-007, FR-008 |
| C-8 | Performance **scales with cores**; the build is not effectively single-threaded. | FR-006 |
| C-9 | The command prints the detected core count, the chosen `<N>`, and elapsed time. | NFR-001 |
| C-10 | The exit code equals the underlying `dotnet` exit code. | FR-009 |

## Non-guarantees (explicit scope edges)

- Does **not** speed up the internals of the SDD `fs-gg-fullstack` template-generation
  integration test; that test stays **opt-in/isolated** (`FSGG_REAL_EVIDENCE` / `CI`) so
  it does not block the suite (FR-010, research D5).
- Does **not** change `Directory.Build.props`/`Directory.Packages.props`, the `.sln`
  membership, any `.fsi`, or any compiled output (Tier 2).
- The absolute time targets assume an 8+ core machine and a warm package cache;
  constrained runners are proportionally slower but still benefit (spec Assumptions).

## Compatibility / migration

The plain `dotnet build FS.GG.Governance.sln` (no `-m`) keeps working but remains slow and
may still thrash — it is superseded as *the documented command* by the wrapper. Docs
(`README.md`, `docs/`) are updated in the same change to point at the wrapper. CI, when it
exists, calls the wrapper. No API or package consumer is affected.
