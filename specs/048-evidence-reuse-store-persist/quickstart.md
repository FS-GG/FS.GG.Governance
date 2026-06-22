# Quickstart: Persist The Evidence-Reuse Store To Disk From The Host Commands

A runnable validation guide proving the row end-to-end. See [data-model.md](./data-model.md) for the persisted
derivation and MVU transitions, [contracts/](./contracts/) for the committed `.fsi` deltas, and
[research.md](./research.md) for the decisions. Implementation details belong in `tasks.md`, not here.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The F047 core `FS.GG.Governance.EvidenceReuseStore` is built and on the solution (it is).
- From repo root: `dotnet build FS.GG.Governance.sln` succeeds before changes.

## Build

```bash
# After wiring (adds the two ProjectReferences + the MVU cases):
dotnet build src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj
dotnet build src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj
```

A `TreatWarningsAsErrors` build that succeeds confirms the new `.fsi` cases and the EvidenceReuseStore reference
compile cleanly.

## Exercise in FSI (Principle I — honest audience)

`scripts/prelude.fsx` gains a persistence section. The shape to exercise:

```fsharp
// 1. Build a real loaded store via the genuine F030 record (real FreshnessInputs, opaque synthetic refs).
let loaded = EvidenceReuse.empty |> EvidenceReuse.record inputsA evidenceA |> EvidenceReuse.record inputsB evidenceB

// 2. The persisted document is exactly the F047 pipeline the host emits (decision lives in `update`):
let content =
    loaded
    |> EvidenceReuseStore.prune
    |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
    |> EvidenceReuseStore.serialise

// 3. Round-trip through the REAL reader (write to a temp file, read back):
System.IO.File.WriteAllText(tmp, content)
let reread = FreshnessSensing.realStoreReader tmp     // Ok (Some store)
// reread store equals (prune >> retain) loaded — lossless w.r.t. survivors.
```

## Validate (acceptance ↔ success criteria)

Run the focused command tests:

```bash
dotnet test tests/FS.GG.Governance.RouteCommand.Tests/FS.GG.Governance.RouteCommand.Tests.fsproj
dotnet test tests/FS.GG.Governance.ShipCommand.Tests/FS.GG.Governance.ShipCommand.Tests.fsproj
```

### Scenario 1 — Durable lossless round-trip (US1 / SC-001)
Point a command at a temp repo whose `readiness/evidence-reuse.json` holds a well-formed non-empty `v1` document;
run with `--persist-store`. Assert the post-run file re-reads through `FreshnessSensing.realStoreReader` to the
store the command persisted, opaque evidence refs and freshness inputs preserved verbatim.

### Scenario 2 — Absent file persists an empty `v1` (US1 AC-2 / Edge)
Delete the store path; run with `--persist-store`. Assert a well-formed empty `v1` document is written (parent
dirs created), distinct on disk from an absent file, re-reading as `EvidenceReuse.empty`.

### Scenario 3 — Deterministic write (US1 AC-3 / SC-002)
Run twice with identical inputs and identical loaded store; assert the two persisted files are byte-identical.

### Scenario 4 — Bounded + pruned, removal-only (US2 / SC-003)
Seed a store that exceeds `defaultRetentionBound` and contains a strictly-superseded entry; run with
`--persist-store`. Assert the persisted file is within bound, newest-first, contains no superseded entry, and
every surviving entry is byte-for-byte one of the loaded entries.

### Scenario 5 — Verdicts decoupled; ship verdict/exit unchanged (US3 AC-1 / SC-004)
Run each command with and without `--persist-store` over the same inputs. Assert the per-gate cache verdicts in
`route.json` / `audit.json` are identical, and ship's verdict, partition, enforcement fields, and exit code are
byte-for-byte identical.

### Scenario 6 — Induced write failure is non-fatal (US3 AC-2 / SC-005)
Make the store path unwritable (e.g. a read-only directory). Run with `--persist-store`. Assert: exit code
unchanged (route 0; ship governed solely by its verdict basis), `route.json` / `audit.json` unchanged, **no**
partial/`.tmp-*` store file left, and a non-fatal note appears in the summary.

### Scenario 7 — Don't clobber a malformed store (Edge / D6)
Write garbage to the store path; run with `--persist-store`. Assert the malformed file is **not** overwritten
and a non-fatal note explains it was left untouched; the run's verdicts are computed from the degraded-to-empty
store exactly as F046 already does.

### Scenario 8 — Persistence off is byte-identical (US3 AC-3 / SC-006)
Run both commands **without** `--persist-store`. Assert no store file is written and every emitted artifact and
existing golden baseline is byte-identical to the pre-row baseline.

### Pure-transition checks (SC-008, no filesystem)
Drive `Loop.update` directly: assert the emitted `PersistStore` effect's `content` equals the F047 pipeline
output; assert no `PersistStore` effect when the flag is off or the store degraded; assert
`StorePersisted(Error _)` changes neither `Exit` nor the emitted artifacts.

## Surface / hygiene

```bash
dotnet test   # the existing reflective surface-drift tests must pass with the two updated baselines
```

Assert `surface/FS.GG.Governance.RouteCommand.surface.txt` and `…ShipCommand.surface.txt` reflect exactly the
three new public cases/field each, and that no F029–F047 baseline and no route/audit golden changed (SC-007).
