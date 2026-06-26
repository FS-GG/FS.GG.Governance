# Quickstart — validating Kernel JSON consolidation

This feature is **proven by absence of change**: every machine-readable JSON output stays
byte-identical while the duplicated source shrinks. The validation below works for each of
the three slices independently.

## Prerequisites

- .NET SDK with `net10.0` (solution builds via `Directory.Build.props`).
- A clean working tree before each slice (so `git status` cleanly shows whether any golden
  fixture moved — a moved fixture is a failure signal, not an update target).

## The acceptance invariant (all slices)

```bash
# 1. Build the whole solution (warnings are errors here).
dotnet build FS.GG.Governance.sln

# 2. Run the full test suite.
dotnet test FS.GG.Governance.sln

# 3. THE GATE: no golden/snapshot fixture may have changed.
git status --porcelain -- '**/*.golden' '**/*.snapshot' 'tests/**/Fixtures/**'
#    → expected output: EMPTY. Any printed fixture path means behaviour drifted → revert.
```

A green suite **and** an empty fixture diff is the pass condition for every slice.

## Slice 1 — `writeToString` exported from `Kernel`

1. Add the `val writeToString` line to `src/FS.GG.Governance.Kernel/Json.fsi`
   (see `contracts/kernel-json-delta.md`).
2. For each `src` project with a local `writeToString` (the 12 `*Json` projections,
   `EvidenceReuseStore`, `RefreshCommand/Interpreter`, and `AttestationJson`'s `private`
   copy): ensure `Kernel` is on its reference graph (add a `ProjectReference` only where it
   is not already transitive), call `Json.writeToString`, and delete the local copy.
3. Regenerate the Kernel surface baseline and confirm only `writeToString` was added:
   ```bash
   dotnet test tests/FS.GG.Governance.Kernel.Tests   # SurfaceDriftTests guards surface/FS.GG.Governance.Kernel.surface.txt
   ```
4. Validate the acceptance invariant.

**Expected outcome**: exactly one definition of `writeToString` remains in `src`
(`Kernel`); ~13–14 copies removed; all goldens byte-identical.

```bash
# proof there is one definition left:
grep -rl "let writeToString\|let private writeToString" src --include='*.fs'
#   → only src/FS.GG.Governance.Kernel/Json.fs
```

## Slice 2 — `FS.GG.Governance.JsonTokens`

1. Create the project (`.fsproj` + `JsonTokens.fsi` + `JsonTokens.fs`) per
   `contracts/JsonTokens.fsi`; register it and its `*.Tests` in `FS.GG.Governance.sln`.
2. In each projection, replace the local token helpers with **module-qualified** calls
   (`JsonTokens.maturityToken …`) — qualified, not `open`-ed, to avoid ambiguity with
   `Enforcement.maturityToken`/`profileToken` where both are in scope (research D5). Delete
   the local copies, including `VerifyJson`'s `rr`-prefixed token block.
3. Add `surface/FS.GG.Governance.JsonTokens.surface.txt` + `SurfaceDriftTests.fs`.
4. Validate the acceptance invariant.

**Expected outcome**: each of the seven token helpers has exactly one definition in the JSON
layer; goldens byte-identical.

## Slice 3 — `FS.GG.Governance.JsonWriters`

1. Create the project (`.fsproj` referencing `JsonTokens` + the domain owners) per
   `contracts/JsonWriters.fsi`; register it and its `*.Tests`.
2. Replace the duplicated sub-object writers / map helpers in the projections with
   module-qualified calls; delete the local copies (`writeCause`/`writeCauseValue`,
   `verdictByGate`, `outcomeByGate`, `writeExecution`, `writeEnforcement`, nullable writers).
3. Add the surface baseline + `SurfaceDriftTests.fs`.
4. Validate the acceptance invariant.

**Expected outcome**: each shared writer/map helper has exactly one definition; goldens
byte-identical; net `src` reduction ~300 LOC across the three slices.

## What "done" looks like

- `dotnet build` + `dotnet test` green on `FS.GG.Governance.sln`.
- `git status` shows **no** changed golden/snapshot fixture.
- `grep` confirms one definition each of `writeToString` and the seven token helpers.
- Surface baselines updated for `Kernel` (+1 member) and the two new leaves (new files);
  projection surfaces unchanged.
- Test count unchanged (no tests lost in the move) — compare `dotnet test` totals before/after.
