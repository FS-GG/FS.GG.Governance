# Quickstart & Validation: `fsgg verify` Surface-Checks Host Wiring

A runnable validation guide proving the feature end-to-end. Scenarios map to the spec's Success Criteria and to
the `059` acceptance items being closed (T045). Details live in [contracts/](./contracts/verify-json-surfacechecks.md)
and [data-model.md](./data-model.md) — not duplicated here.

## Prerequisites

- .NET 10 SDK (the solution targets `net10.0`).
- Repo built clean: `dotnet build FS.GG.Governance.sln` (warnings-as-errors).

## Build & test

```bash
# Full solution gate (no regression anywhere)
dotnet build FS.GG.Governance.sln
dotnet test FS.GG.Governance.sln

# Just the verify host suite (fastest inner loop for this feature)
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests/FS.GG.Governance.VerifyCommand.Tests.fsproj
```

## Scenario 1 — No declared surfaces ⇒ byte-identical `verify.json` (SC-002)

1. A real temp repo with **no** declared product surfaces.
2. Run `fsgg verify` over it (via `Interpreter.run` in `SurfaceChecksE2ETests.fs`).
3. **Expected**: `verify.json` is byte-identical to the frozen pre-wiring golden under
   `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/` — no `surfaceChecks` field, schema version unchanged.
   Every other host golden (`route.json`/`ship.json`/`release.json`) is untouched (FR-009).

## Scenario 2 — Drifted package surface blocks (SC-001)

1. A real temp repo declaring a package surface whose baseline drifted (`Support.fs` builder).
2. Run `fsgg verify`.
3. **Expected**: `verify.json` gains a `surfaceChecks` entry `package.baseline-drift` carrying the drift detail
   and the declared `evidenceTag`; the run exits with the blocking exit code at `RunMode.Verify`
   (contracts C2/C3, acceptance 2).

## Scenario 3 — Advisory-only finding surfaces without escalating (SC-003)

1. A real temp repo whose only surface finding is advisory.
2. Run `fsgg verify`.
3. **Expected**: the entry appears with `"severity":"advisory"`; the exit code equals a clean run's exit code
   (contracts C3, acceptance 3).

## Scenario 4 — Determinism & read-only (SC-004, FR-012)

1. Re-run Scenario 2 over unchanged inputs; then rebuild the temp tree with the surfaces/files created in a
   different order. Also run a repo with a declared package surface that has **no committed baseline**.
2. **Expected**: `verify.json` is byte-identical across both runs (`Composition.run`'s stable sort; data-model
   §4). The no-baseline run reports `package.baseline-absent` (blocking) but **writes nothing** to the working
   tree and spawns no process (read-only package port), so two consecutive runs are byte-identical and the tree
   is unchanged (contract C4).

## Scenario 5 — Safe failure (FR-010)

1. A temp repo with a declared surface whose fact source is unreadable / escapes the product root.
2. Run `fsgg verify`.
3. **Expected**: a disclosed diagnostic / bounded sensor outcome (e.g. `PathEscapesBounds`), **not** a silent
   pass and **not** a crash; a genuine tool defect is distinguishable from missing/malformed input (research D6,
   Constitution VI).

## Constitution-gate checks (SC-005)

- `dotnet build FS.GG.Governance.sln` clean (warnings-as-errors) and the whole solution green.
- The `VerifyCommand` surface-drift baseline re-blessed for the additive `Loop`/`Interpreter` surface growth;
  `SurfaceDriftTests` green.
- `Directory.Packages.props` unchanged — **no new external dependency**.
- A `fsgg verify` run leaves the working tree unchanged (no `.baseline` written) and spawns no process — the
  package domain is wired through the read-only port (FR-012).
- `059` tasks **T045 / T048 / T052** marked complete (citing `067`); the roadmap note records the verify
  surface-checks wiring as closed.
