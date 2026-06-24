# Quickstart: Capture A Real Evidence Reference From An Executed Gate

A validation/run guide for the new pure library `FS.GG.Governance.EvidenceCapture`. It proves the row
end-to-end: a real (built) `CommandRecord` becomes a reproducible `EvidenceRef`, folds into the store, and the
captured world is then reusable. **No I/O** anywhere (SC-008). See
[`contracts/EvidenceCapture.fsi`](./contracts/EvidenceCapture.fsi) for the surface and
[`data-model.md`](./data-model.md) for the semantics.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The merged F030 `EvidenceReuse`, F032 `CommandRecord`, F029 `FreshnessKey`, and — for the persistence
  round-trip only — F047 `EvidenceReuseStore` + F046 `FreshnessSensing` build (they are already on `main`).

## Build

```bash
# Build just the new library and its tests once the projects exist:
dotnet build src/FS.GG.Governance.EvidenceCapture/FS.GG.Governance.EvidenceCapture.fsproj
dotnet build tests/FS.GG.Governance.EvidenceCapture.Tests/FS.GG.Governance.EvidenceCapture.Tests.fsproj
```

## Exercise in FSI (honest-audience walkthrough — `scripts/prelude.fsx`)

The prelude gains an F049 section that loads the packed/Debug DLLs and exercises the public surface exactly as a
caller would. Sketch:

```fsharp
#r "../src/FS.GG.Governance.CommandRecord/bin/Debug/net10.0/FS.GG.Governance.CommandRecord.dll"
#r "../src/FS.GG.Governance.EvidenceReuse/bin/Debug/net10.0/FS.GG.Governance.EvidenceReuse.dll"
#r "../src/FS.GG.Governance.EvidenceCapture/bin/Debug/net10.0/FS.GG.Governance.EvidenceCapture.dll"

open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceCapture

// An already-executed gate, as its reproducible facts + the one sensed duration (F032).
let env = { Added = []; Changed = []; Removed = [] }
let record =
    CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work") env (TimeoutLimit 30) (ExitCode 0)
        (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 123_456L)

// The same gate, identical in every reproducible fact, only SLOWER:
let slower = CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ]
                 (WorkingDirectory "/work") env (TimeoutLimit 30) (ExitCode 0)
                 (OutputDigest "sha-out") (OutputDigest "sha-err") NoCapturedOutput (SensedDuration 999_999L)

// US2 / SC-002: the sensed duration NEVER leaks into the reference.
printfn "[F49] duration-only difference => equal reference? %b"
    (EvidenceCapture.referenceOf record = EvidenceCapture.referenceOf slower)   // true

// US1 / SC-001: capture, then the captured world is reusable and serves the derived reference.
let inputs : FreshnessInputs = (* a resolved freshness world — see F029/F043 fixtures *) failwith "fill in"
let grown = EvidenceCapture.capture inputs record EvidenceReuse.empty
printfn "[F49] captured world reusable with derived ref? %b"
    (EvidenceReuse.decide inputs grown = Reuse(EvidenceCapture.referenceOf record))   // true

// US3 / SC-004: a DIFFERENT world is still Recompute — capture added no match for it.
// EvidenceReuse.decide otherWorld grown = Recompute _
```

(The `inputs` placeholder is filled from the F029/F043 freshness-world fixtures already used across the cache
thread.)

## Run the tests

```bash
dotnet test tests/FS.GG.Governance.EvidenceCapture.Tests/FS.GG.Governance.EvidenceCapture.Tests.fsproj
```

### What the tests assert (each maps to a success criterion)

| Test file | Asserts | Maps to |
|-----------|---------|---------|
| `ReferenceTests.fs` | Two records differing ONLY in `SensedDuration` → byte-identical `EvidenceRef`. | US2 / SC-002 |
| `ReferenceTests.fs` | Each single reproducible-fact perturbation (executable, an argument, argument order, working dir, env-delta set, timeout, exit code, each digest, each captured-output outcome) → a DIFFERENT reference. | US2 / SC-003 |
| `ReferenceTests.fs` | `referenceOf` total over empty digests, non-zero exit, all captured-output outcomes; same input → byte-identical reference (FsCheck). | FR-007 / SC-005 |
| `CaptureTests.fs` | `decide inputs (capture inputs record empty)` = `Reuse (referenceOf record)`. | US1 / SC-001 |
| `CaptureTests.fs` | Into a non-empty store: every prior entry preserved byte-for-byte; `decide` for every world other than the captured one is unchanged. | US3 / SC-004 |
| `CaptureTests.fs` | Re-capturing the same world with a new record → `decide` serves the most-recently-captured reference. | US3 (newest-first) |
| `PersistenceRoundTripTests.fs` | A `capture`-grown store → F047 `serialise` → F046 `realStoreReader` re-reads an EQUAL store (world + exact reference). | FR-010 / SC-007 |
| (all) | Every test runs with no filesystem/clock/process/network. | SC-008 |

### Additive guarantee (SC-006)

```bash
# Full build + test suite stays green; no existing artifact changes:
dotnet build FS.GG.Governance.sln
dotnet test  FS.GG.Governance.sln
git status   # only NEW files under src/, tests/, surface/, specs/049-*, scripts/prelude.fsx + CLAUDE.md pointer
```

The library adds zero third-party dependencies, bumps zero schema versions, and edits zero existing cores, host
commands, or golden baselines. The new public surface is captured in
`surface/FS.GG.Governance.EvidenceCapture.surface.txt` and guarded by the reflective surface-drift test pattern.
