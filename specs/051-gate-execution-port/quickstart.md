# Quickstart: Gate-Execution Port (F051)

A validation/run guide for `FS.GG.Governance.GateExecution` — the first process-spawning edge. It proves the
feature end-to-end: run a real gate, assemble its `CommandRecord`, and close the chain into F049. For the
public contract see [contracts/](./contracts/); for the semantics see [data-model.md](./data-model.md).

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- A POSIX shell (`/bin/sh`) for the real temp-script fixtures — present on the Linux CI shell.
- The on-graph dependency F050 `FS.GG.Governance.ExecutionRecord` (brings F032 `CommandRecord` and F014
  `Config` transitively). No new third-party package.

## Build

```bash
# Build the new edge library (and its dependency graph)
dotnet build src/FS.GG.Governance.GateExecution/FS.GG.Governance.GateExecution.fsproj
```

Compile order is `Model.fsi → Model.fs → Interpreter.fsi → Interpreter.fs`. The `.fsproj` has a single
`ProjectReference` to `FS.GG.Governance.ExecutionRecord`.

## Exercise in FSI (the honest audience — Principle I)

The `scripts/prelude.fsx` F051 section demonstrates both sides of the port boundary. Shape:

```fsharp
// ── F051: GateExecution (IMPURE gate-execution edge) ──
#r "src/FS.GG.Governance.GateExecution/bin/Debug/net10.0/FS.GG.Governance.GateExecution.dll"
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.GateExecution

// (1) PURE GIVEN THE PORT — drive senseExecution with a deterministic FAKE port (no process at all)
let fakePort : Model.ExecutionPort =
    fun _command ->
        { Stdout = System.Text.Encoding.UTF8.GetBytes "hello\n"
          Stderr = [||]
          ExitCode = Model.ExitCode 0
          Duration = Model.SensedDuration 1_000_000L }

let cmd : Model.GateCommand =
    { Executable = Model.Executable "echo"
      Arguments = [ Model.Argument "hello" ]
      WorkingDirectory = Model.WorkingDirectory "/tmp"
      Environment = { Added = []; Changed = []; Removed = [] }
      Timeout = Config.Model.TimeoutLimit 30
      CapturedOutput = Model.NoCapturedOutput }

let record = Interpreter.senseExecution fakePort cmd
// record.Reproducible.StdoutDigest = ExecutionRecord.digestOf (UTF8 "hello\n"); StderrDigest = digest of [||]
CommandRecord.canonicalId record        // defined and reproducible

// (2) THE REAL EDGE — run an actual process through realPort (writes a temp script, runs /bin/sh)
let realRecord = Interpreter.senseExecution Interpreter.realPort realCmd   // realCmd points at the temp script
```

Expected: the fake-port record's two digests equal `ExecutionRecord.digestOf` of the supplied buffers, every
reproducible fact equals `cmd`, and `canonicalId` is defined. The real-edge record carries the script's real
captured bytes and exit code.

## Validation scenarios → tests

Run the suite:

```bash
dotnet test tests/FS.GG.Governance.GateExecution.Tests/FS.GG.Governance.GateExecution.Tests.fsproj
```

| Scenario (spec) | Test file | Asserts |
|-----------------|-----------|---------|
| US1 AC1/AC2 — real run → assembled record | `SenseTests.fs` | `StdoutDigest`/`StderrDigest = digestOf <captured>`, `ExitCode 0`, every reproducible fact carried verbatim (incl. the env delta's three classes); driven by a fake port AND a real temp-script fixture |
| US2 AC1 — non-zero exit recorded | `FailureTests.fs` | a script exiting `7` → `ExitCode 7` + captured output digested; recorded, not rejected |
| US2 AC2 — missing executable | `FailureTests.fs` | a non-existent executable → `startFailureExitCode` + a captured diagnostic; `senseExecution` does not throw |
| US2 AC3 — timeout bounded | `FailureTests.fs` | a script sleeping past a 1s `TimeoutLimit` → terminated within a bounded time, `timeoutExitCode`, partial output, elapsed duration; never hangs |
| US3 AC1 — identity stable across runs | `IdentityTests.fs` | two runs of a deterministic gate → byte-identical `canonicalId` despite differing `Duration` |
| US3 AC2 — identity sensitivity | `IdentityTests.fs` | perturbing one output byte / argument / working dir / env entry / exit code each changes `canonicalId` |
| US3 AC3 — duration-invariance | `IdentityTests.fs` | duration-only difference does not change `canonicalId` |
| US1 AC3 — close the loop | `CloseLoopTests.fs` | `senseExecution`'s record → F049 `referenceOf` (defined, reproducible) → `capture` makes the gate's world reusable |
| Principle II — surface hygiene | `SurfaceDriftTests.fs` | reflective surface equals the committed baseline; production assembly references only the ExecutionRecord graph + BCL + FSharp.Core (NOT EvidenceCapture/EvidenceReuse/FreshnessKey/host/adapters/CLI) |

The edge tests use **real `/bin/sh` temp-script fixtures** (mirroring the `Snapshot` tests' real `git`); the
pure-given-the-port tests use a **deterministic fake port** over real `byte[]`. No network, no governed
repository (SC-007). Output digests are derived from real captured bytes, never `Synthetic` literals.

## Surface baseline

Generate/refresh the reflective baseline after a deliberate surface change:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.GateExecution.Tests/FS.GG.Governance.GateExecution.Tests.fsproj
```

This writes `surface/FS.GG.Governance.GateExecution.surface.txt`. Run without the env var to assert no drift.

## Done when

- `dotnet build` of the new library succeeds with `TreatWarningsAsErrors`.
- All scenarios above pass, including the real-process edge tests and the close-the-loop round-trip.
- The surface-drift baseline is committed and the scope-hygiene assertion passes.
- Zero edits to any F029–F050 core, host command, golden baseline, or schema (additive guarantee, FR-011).
