# Quickstart: Digest Captured Output And Assemble A Command Record From An Execution Outcome

A validation/run guide for the new pure library `FS.GG.Governance.ExecutionRecord`. It proves the row
end-to-end: raw captured stdout/stderr **bytes** become byte-stable `OutputDigest`s, those are assembled into a
complete F032 `CommandRecord`, and that record then derives a reproducible F049 `EvidenceRef` and a reusable
store entry. **No I/O** anywhere (SC-008) — hashing in-memory `byte[]` is pure computation. See
[`contracts/ExecutionRecord.fsi`](./contracts/ExecutionRecord.fsi) for the surface and
[`data-model.md`](./data-model.md) for the semantics.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- The merged F032 `CommandRecord` (and, transitively, F014 `Config`) build — already on `main`. For the
  close-the-loop walkthrough only: F049 `EvidenceCapture`, F030 `EvidenceReuse`, F029 `FreshnessKey` (also on
  `main`).

## Build

```bash
# Build just the new library and its tests once the projects exist:
dotnet build src/FS.GG.Governance.ExecutionRecord/FS.GG.Governance.ExecutionRecord.fsproj
dotnet build tests/FS.GG.Governance.ExecutionRecord.Tests/FS.GG.Governance.ExecutionRecord.Tests.fsproj
```

## Exercise in FSI (honest-audience walkthrough — `scripts/prelude.fsx`)

The prelude gains an F050 section that loads the packed/Debug DLLs and exercises the public surface exactly as a
caller would. Sketch:

```fsharp
#r "../src/FS.GG.Governance.CommandRecord/bin/Debug/net10.0/FS.GG.Governance.CommandRecord.dll"
#r "../src/FS.GG.Governance.EvidenceReuse/bin/Debug/net10.0/FS.GG.Governance.EvidenceReuse.dll"
#r "../src/FS.GG.Governance.EvidenceCapture/bin/Debug/net10.0/FS.GG.Governance.EvidenceCapture.dll"
#r "../src/FS.GG.Governance.ExecutionRecord/bin/Debug/net10.0/FS.GG.Governance.ExecutionRecord.dll"

open System.Text
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.ExecutionRecord

// US2 / SC-002 — content agreement and SC-003 — content sensitivity:
let outA = Encoding.UTF8.GetBytes "build succeeded\n"
let outB = Encoding.UTF8.GetBytes "build succeeded\n"   // same bytes
let outC = Encoding.UTF8.GetBytes "build succeeded!\n"  // one byte different
printfn "[F50] equal content => equal digest? %b"  (ExecutionRecord.digestOf outA = ExecutionRecord.digestOf outB) // true
printfn "[F50] one byte differs => digest differs? %b" (ExecutionRecord.digestOf outA <> ExecutionRecord.digestOf outC) // true

// FR-003 — totality over empty input, distinct from non-empty:
printfn "[F50] empty digest defined & distinct? %b"
    (ExecutionRecord.digestOf [||] <> ExecutionRecord.digestOf outA)  // true, never throws

// A captured execution outcome: the nine reproducible facts + duration + RAW output bytes.
let env = { Added = []; Changed = []; Removed = [] }
let mk dur stdout =
    ExecutionRecord.recordOf (Executable "gcc") [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work") env (TimeoutLimit 30) (ExitCode 0)
        stdout (Encoding.UTF8.GetBytes "") NoCapturedOutput (SensedDuration dur)

let record = mk 123_456L outA

// US3 / SC-007 — recordOf is build composed with digestOf on the two output positions:
let viaBuild =
    CommandRecord.build (Executable "gcc") [ Argument "-c"; Argument "main.c" ]
        (WorkingDirectory "/work") env (TimeoutLimit 30) (ExitCode 0)
        (ExecutionRecord.digestOf outA) (ExecutionRecord.digestOf (Encoding.UTF8.GetBytes ""))
        NoCapturedOutput (SensedDuration 123_456L)
printfn "[F50] recordOf = build ∘ digestOf? %b" (record = viaBuild)   // true

// US2 / SC-004 — duration-invariance of the identity (and the F049 reference):
let slower = mk 999_999L outA   // identical bytes & facts, only slower
printfn "[F50] duration-only diff => equal canonicalId? %b"
    (CommandRecord.canonicalId record = CommandRecord.canonicalId slower)               // true
printfn "[F50] duration-only diff => equal F049 reference? %b"
    (EvidenceCapture.referenceOf record = EvidenceCapture.referenceOf slower)           // true

// SC-003 — one output byte flips the identity and the reference:
let changed = mk 123_456L outC
printfn "[F50] one output byte => different reference? %b"
    (EvidenceCapture.referenceOf record <> EvidenceCapture.referenceOf changed)        // true

// US1 / SC-001 — close the loop: capture the assembled record, the world is reusable for the derived ref.
let inputs : FreshnessInputs = (* a resolved freshness world — see F029/F043 fixtures *) failwith "fill in"
let grown = EvidenceCapture.capture inputs record EvidenceReuse.empty
printfn "[F50] captured world reusable with derived ref? %b"
    (EvidenceReuse.decide inputs grown = Reuse(EvidenceCapture.referenceOf record))    // true
```

## Test (Expecto, no I/O — SC-008)

```bash
dotnet test tests/FS.GG.Governance.ExecutionRecord.Tests/FS.GG.Governance.ExecutionRecord.Tests.fsproj
```

The suite covers (see [plan.md](./plan.md) Project Structure for the file map):

- **DigestTests** — content agreement (SC-002); single-byte change/add/remove/reorder sensitivity (SC-003);
  empty-input totality and distinctness (FR-003); binary and large-input totality (Edge cases); determinism
  (SC-005); identical stdout/stderr bytes → equal digests (Edge case).
- **RecordTests** — `recordOf` equals `build` with `digestOf` on the two output positions (SC-007); digests in
  the correct position, never swapped (FR-005); arguments-in-order and env-delta three-class carriage (FR-005);
  a non-zero exit code assembles an ordinary record (US3 AC2); duration carried only in `Duration` (FR-005).
- **CloseLoopTests** — `canonicalId` and F049 `referenceOf` defined and reproducible (SC-001); `capture` makes
  the world reusable for the derived reference (US1 AC3); any single reproducible-fact perturbation (incl. one
  output byte) changes identity and reference (SC-003); duration-invariance of identity and reference (SC-004,
  FR-006).
- **SurfaceDriftTests** — the reflective surface baseline matches and scope hygiene holds (Principle II).

## Done When

- `dotnet build` of the full solution is green; every pre-existing artifact byte-identical (SC-007).
- `dotnet test` of the new suite is green; the surface baseline
  (`surface/FS.GG.Governance.ExecutionRecord.surface.txt`) matches.
- No new third-party `PackageReference`, no schema-version bump, no edit to any existing core/host/golden
  baseline (SC-007).
