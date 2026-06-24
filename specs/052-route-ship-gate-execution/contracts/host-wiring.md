# Host-seam contract deltas (F052)

The host wiring extends existing, already-curated `.fsi` surfaces. This document specifies the **deltas** to
those surfaces (the additive fields/parameters/cases) so they can be drafted before any `.fs` body changes
(Principle I). Each delta updates that module's reflective surface baseline (Principle II / Change
Classification). Nothing here edits a frozen merged core.

## `FS.GG.Governance.RouteCommand` / `FS.GG.Governance.ShipCommand` — `Interpreter.fsi`

The injected-ports record gains the F051 execution port (the only seam through which a command touches a gate
process — D4):

```fsharp
type Ports =
    { Files: Loader.FileReader
      Git: FS.GG.Governance.Snapshot.Ports
      Freshness: FreshnessSensing.FreshnessSensor
      Store: FreshnessSensing.StoreReader
      Write: ArtifactWriter
      Out: OutputSink
      Execute: FS.GG.Governance.GateExecution.Model.ExecutionPort }   // NEW (D4)
```

`realPorts` wires `Execute = FS.GG.Governance.GateExecution.Interpreter.realPort`. Tests inject a deterministic
fake `ExecutionPort` (D8). No other `Interpreter.fsi` symbol changes; `run` / `step` / `realPorts` keep their
signatures (they thread the larger `Ports` unchanged).

## `FS.GG.Governance.RouteCommand` / `FS.GG.Governance.ShipCommand` — `Loop.fsi`

The pure loop gains one `Effect` case and one `Msg` case (mirroring the existing `SenseFreshness` /
`FreshnessSensed` and `LoadStore` / `StoreLoaded` pairs — D4):

```fsharp
type Effect =
    | ...                                                  // existing cases unchanged
    | ExecuteGates of (GateId * GateCommand) list          // NEW — the must-recompute command-gates to run

type Msg =
    | ...                                                  // existing cases unchanged
    | GatesExecuted of (GateId * CommandRecord) list       // NEW — the assembled records, in request order
```

`init` / `update` / `render` / `parse` / `exitCode` keep their signatures. `update` requests `ExecuteGates`
after cache eligibility (D5) and, on `GatesExecuted`, folds F049 `capture` into the store, builds the per-gate
`GateOutcome`s, projects the documents with the execution embed, and emits the persist-grown-store effect.
`fsgg route`'s `exitCode` is unchanged (always `0` — FR-008).

## `FS.GG.Governance.RouteJson` — `RouteJson.fsi`

`ofRouteResult` gains a new **optional** trailing parameter carrying the per-gate outcomes (D6). Default-empty
⇒ byte-identical output to today (preserves the F045-era golden; FR-009):

```fsharp
// before: val ofRouteResult: result: RouteResult -> cache: CacheEligibilityReport option -> string
val ofRouteResult:
    result: RouteResult ->
    cache: CacheEligibilityReport option ->
    execution: (GateId * GateOutcome) list ->        // NEW (empty ⇒ no `execution` embed, output unchanged)
        string
```

## `FS.GG.Governance.AuditJson` — `AuditJson.fsi`

`ofShipDecision` gains the same optional per-gate outcomes parameter (D6):

```fsharp
// before: val ofShipDecision: decision: ShipDecision -> cache: CacheEligibilityReport option -> string
val ofShipDecision:
    decision: ShipDecision ->
    cache: CacheEligibilityReport option ->
    execution: (GateId * GateOutcome) list ->        // NEW (empty ⇒ no `execution` embed, output unchanged)
        string
```

Each selected-gate entry gains, beside the F045 `cacheEligibility` object, matched by `GateId`:

```json
"execution": { "disposition": "executed" | "reused" | "notExecuted", "exitCode": <int>, "passed": <bool> }
```

`exitCode` / `passed` are omitted for `notExecuted`.

## `FS.GG.Governance.ShipCommand` — the verdict relocation (ship only, D3)

A pure helper, declared in `ShipCommand`'s curated `.fsi` (it depends on `Ship.Model` / `Enforcement`, so it
lives here, NOT in `GateRun` — D7), applies real pass/fail to the verbatim `Ship.rollup` decision:

```fsharp
/// Relocate every PASSING command-gate (id in `passedGateIds`) out of `Blockers`/`Warnings` into `Passing`,
/// then recompute `Verdict` / `ExitCodeBasis` from the remaining blockers (Ship's own rule, re-applied). A
/// failing or no-command gate is left exactly where `Ship.rollup` placed it; findings are never moved. This
/// is the only verdict change this row introduces (FR-006, FR-009), and it can only CLEAR blockers a passing
/// gate would otherwise raise — never create one.
val applyExecution: passedGateIds: Set<GateId> -> decision: ShipDecision -> ShipDecision
```

`Ship.rollup`, `Enforcement.deriveEffectiveSeverity`, and every `Ship`/`Enforcement` type are used **verbatim**
and unedited (FR-017); `applyExecution` constructs only the already-public `ShipDecision` / `EnforcedItem` /
`Verdict` / `ExitCodeBasis` values.
