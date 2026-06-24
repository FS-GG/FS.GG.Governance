# Quickstart: Execute Selected Gates In `fsgg route` / `fsgg ship` (F052)

A validation/run guide for the host wiring that closes the evidence-reuse loop end-to-end: `fsgg route` and
`fsgg ship` now run each selected must-recompute gate through the F051 port, capture its evidence, persist the
grown store, and (in ship) let real pass/fail drive the verdict — while a `reusable` gate is reused, not
re-run. For the new public contracts see [contracts/](./contracts/); for semantics see
[data-model.md](./data-model.md) and [research.md](./research.md).

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- A POSIX shell (`/bin/sh`) for the real temp-script fixtures (present on the Linux CI shell).
- The merged thread on graph: F051 `GateExecution`, F050 `ExecutionRecord`, F049 `EvidenceCapture`,
  F047/F048 `EvidenceReuseStore`, F046 `FreshnessSensing`, F041 `CacheEligibility`, F032 `CommandRecord`,
  F030 `EvidenceReuse`, F024 `Ship`, F023 `Enforcement`, F018 `Gates`, F014 `Config`. No new third-party package.

## Build

```bash
# The new pure helper library (and its dependency graph)
dotnet build src/FS.GG.Governance.GateRun/FS.GG.Governance.GateRun.fsproj

# The edited host commands and document emitters
dotnet build src/FS.GG.Governance.RouteCommand/FS.GG.Governance.RouteCommand.fsproj
dotnet build src/FS.GG.Governance.ShipCommand/FS.GG.Governance.ShipCommand.fsproj
```

`GateRun` compile order is `Model.fsi → Model.fs → Plan.fsi → Plan.fs`, with `ProjectReference`s to
`GateExecution`, `CommandRecord`, `EvidenceReuse`, `Config`, and `Gates`.

## Exercise the pure helpers in FSI (the honest audience — Principle I)

The `scripts/prelude.fsx` F052 section drives the three new pure pieces with no process at all:

```fsharp
// ── F052: GateRun (pure host helpers) ──
#r "src/FS.GG.Governance.GateRun/bin/Debug/net10.0/FS.GG.Governance.GateRun.dll"
open FS.GG.Governance.GateRun

// (1) argv lex — a literal split, quotes/escapes honored, no shell features
Plan.lexCommandLine "dotnet test --no-build"          // Some (Executable "dotnet", [Argument "test"; Argument "--no-build"])
Plan.lexCommandLine "echo 'hello world'"              // Some (Executable "echo", [Argument "hello world"])
Plan.lexCommandLine "   "                             // None (degenerate)

// (2) commandFor — declared spec → GateCommand; None when the gate declares no command
Plan.commandFor "/repo" tooling gateWithCommand       // Some { WorkingDirectory="/repo"; Environment=empty; Timeout=declared; ... }
Plan.commandFor "/repo" tooling gateWithoutCommand    // None  (⇒ NotExecuted)

// (3) priorExitOf — recover the prior exit from a REAL reference (round-trips senseExecution → referenceOf)
let record = GateExecution.Interpreter.senseExecution fakePort someCommand
let ref'   = FS.GG.Governance.EvidenceCapture.EvidenceCapture.referenceOf record
Plan.priorExitOf ref'                                 // Some (ExitCode <the record's exit>)
Plan.priorExitOf (EvidenceReuse.Model.EvidenceRef "not-canonical")   // None (⇒ recompute, never reuse)
```

## Validate the closed loop (the two-run reuse demo)

The defining behavior — first run executes & grows the store, second run reuses without spawning — is exercised
by the command tests against a writable temp store and a fake port that **counts its calls**:

```text
Run 1 (empty store):
  • selected gate is `mustRecompute` → executed once (fake port call count: 1)
  • evidence captured → store grown → pruned + retained + persisted at <repo>/readiness/evidence-reuse.json
  • route.json / audit.json carry  "execution": { disposition:"executed", exitCode:0, passed:true }

Run 2 (same world, store from run 1):
  • freshness world matches the captured reference → cache marks the gate `reusable`
  • priorExitOf recovers the prior exit → gate is REUSED (fake port call count stays 1 — NO second spawn)
  • route.json / audit.json carry  "execution": { disposition:"reused", exitCode:0, passed:true }
```

## Validate the ship verdict (real pass/fail drives it)

```bash
dotnet test tests/FS.GG.Governance.ShipCommand.Tests/FS.GG.Governance.ShipCommand.Tests.fsproj
```

Expected, against deterministic temp-script gates through a fake port:

- A selected **blocking-maturity** gate whose command **exits non-zero** ⇒ stays a `Blocker` ⇒ ship `Verdict =
  Fail`, exit code `1` (SC-001).
- The **same** gate exiting **0** ⇒ relocated to `Passing` ⇒ not a blocker on account of its execution ⇒
  `Verdict = Pass`, exit code `0` (SC-002).
- A **missing executable** / **timed-out** gate ⇒ the F051 sentinel outcome, treated as a **failed** gate, no
  crash, timeout returns within a bounded time (SC-005).
- A gate with **no declared command** ⇒ not executed, keeps its current rollup treatment (SC-006).

## Validate route stays advisory

```bash
dotnet test tests/FS.GG.Governance.RouteCommand.Tests/FS.GG.Governance.RouteCommand.Tests.fsproj
```

Expected: each selected-gate entry in `route.json` carries its `execution` outcome and executed-vs-reused
disposition; the command **exits 0 regardless of any gate's exit code**; every other field of `route.json`
(selected gates, route trace, findings, cost rollup, cache section, schema version) is byte-identical to before
this wiring (SC-004, FR-009).

## Validate the full solution

```bash
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln
```

Expected: clean build; all projects green. The only document changes are the additive per-gate `execution`
embed and the `fsgg ship` verdict changes that follow directly from real gate results; `RouteJson`/`AuditJson`
own goldens stay byte-stable (the embed defaults to empty); no merged pure core is edited and no schema is
bumped (SC-009). Surface drift: a fresh `surface/FS.GG.Governance.GateRun.surface.txt` plus re-blessed
`RouteJson`/`AuditJson`/`RouteCommand`/`ShipCommand` baselines (generate/update via `BLESS_SURFACE`).
