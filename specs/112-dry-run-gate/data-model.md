# Phase 1 Data Model: Dry-run / simulated governance gate

Types the feature adds or reshapes. Reused types are referenced, not redefined.

## Reshaped (Tier 1 surface move)

### `ShipCommand.Loop.RunRequest` — gains one field

```fsharp
type RunRequest =
    { Repo: string
      Scope: ScopeSelector
      Mode: RunMode
      Profile: Profile
      Format: OutputFormat
      AuditOut: string
      StorePath: string
      PersistStore: bool
      ExplicitPlain: bool
      CostBudgetOut: string
      ProvenanceOut: string
      DryRun: bool }        // NEW — when true: no execution, no writes, simulated output
```

The hidden `ParseAcc` gains a matching `DryRun: bool` (default `false` in `emptyAcc`), filled in
the final `Ok { … }` block. No other request field changes.

## New pure types (`ShipCommand.Simulate` — new module + `.fsi`)

### `SignalClass` — per-gate sufficiency classification

```fsharp
type SignalClass =
    | RequiredSatisfied     // policy required a signal; the handoff carries it
    | RequiredAbsent        // policy required a signal; the handoff does NOT carry it (would-be notEvaluated)
    | NotRequired           // policy did not require this gate for the chosen profile
```

### `GateSufficiency` — one classified gate

```fsharp
type GateSufficiency =
    { GateId: GateId
      Class: SignalClass }
```

### `Sufficiency` — the breakdown (US2)

```fsharp
type Sufficiency =
    { Gates: GateSufficiency list
      RequiredAbsentCount: int          // > 0 ⇒ handoff is insufficient; absence is surfaced, not hidden
      AllNotEvaluated: bool }           // true ⇒ the FS.GG.Audio failure mode (nothing was evaluable)
```

### `SimulatedResult` — the whole dry-run result

```fsharp
type SimulatedResult =
    { Decision: ShipDecision           // reused, via Ship.rollup — Pass/Fail + blockers/warnings/passing
      Sufficiency: Sufficiency
      HandoffDiagnostics: Diagnostic list }   // SddHandoff diagnostics (malformed / version-mismatch / stale)
```

**Construction** (pure, total): `Simulate.assemble` takes the routed `RouteResult`, the consumed
handoff (`ConsumeResult`), `RunMode`, and `Profile`; it reuses `Ship.rollup route mode profile`
for `Decision`, classifies each selected gate for `Sufficiency`, and carries the handoff
`Diagnostics`. No I/O.

## New pure projection types (`ShipCommand.SimulateProjection` — new module + `.fsi`)

Two total functions, mirroring the reused `AuditJson`/`HumanText` split:

```fsharp
val schemaVersion: string                       // = "fsgg.audit.dryrun/v1"  (distinct from fsgg.audit/v2)
val toJson: result: SimulatedResult -> string   // simulated:true + verdict/blockers/warnings/passing + sufficiency
val toText: result: SimulatedResult -> string   // HumanText projection + a "SIMULATED (dry-run)" banner
```

The JSON document (contract detail in [contracts/cli-dry-run.md](./contracts/cli-dry-run.md))
carries `simulated: true` and the distinct `schemaVersion`, so it is recognizable to audit readers
yet impossible to consume as a real gate result. `toText`/`toJson` are deterministic (byte-stable).

## Reused unchanged (no surface move)

- `Ship.rollup: RouteResult -> RunMode -> Profile -> ShipDecision`, and `ShipDecision`,
  `Verdict`, `EnforcedItem*`, `ExitCodeBasis` — the verdict core.
- `AuditJson.ofShipDecision` / `HumanText.ofShipDecision` — the **real** projections, unchanged
  (real `audit.json` stays byte-identical; simulated output does not route through them).
- `SddHandoff.Reader.parse` / `Consumer.consume` / `ConsumeResult` / `Diagnostic` — handoff
  ingestion and its diagnostics.
- `GateRun.Model.GateDisposition.NotExecuted` / `GateOutcome` — the truthful "did not run" state
  used for all gates in a dry-run.
- `Route.Model.RouteResult` / `SelectedGate` — routing output feeding both `rollup` and the
  sufficiency classifier.

## Validation / invariants

- **No-writes invariant**: a dry-run emits zero `WriteArtifact`/`PersistStore` effects
  (asserted via the test `Capture` having empty `Writes`).
- **Real-audit invariant**: `AuditJson.ofShipDecision` output is byte-identical to pre-feature
  (pinned test); `schemaVersion` `fsgg.audit/v2` never appears in simulated output.
- **Absence-visible invariant**: `Sufficiency.AllNotEvaluated = true` ⇒ the text/JSON output shows
  the all-absent state explicitly, never an empty-blockers Pass (FR-011).
- **Determinism**: `toJson`/`toText` are byte-stable across repeated runs on identical inputs.
- **Safe-failure**: a `Malformed`/`VersionMismatch` handoff surfaces in `HandoffDiagnostics` and
  suppresses a bare Pass (FR-008).
