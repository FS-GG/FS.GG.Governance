# Quickstart: Persist, Bound, And Prune The Evidence-Reuse Store

A runnable validation guide for the F047 pure write half. It proves the three operations
(`serialise`/`retain`/`prune`) end-to-end against the **real** F046 reader, with no on-disk persistence wired
into any host command (that is the deferred row). For the document shape and field mapping see
[data-model.md](./data-model.md); for the committed surface see
[contracts/EvidenceReuseStore.fsi](./contracts/EvidenceReuseStore.fsi); for the decisions see
[research.md](./research.md).

## Prerequisites

- .NET SDK (`net10.0`) — the repo standard from `Directory.Build.props`.
- The new project `src/FS.GG.Governance.EvidenceReuseStore` and test project
  `tests/FS.GG.Governance.EvidenceReuseStore.Tests` added to `FS.GG.Governance.sln`.
- No new packages — `System.Text.Json` is the net10.0 shared framework.

## 1. Build

```bash
dotnet build src/FS.GG.Governance.EvidenceReuseStore/FS.GG.Governance.EvidenceReuseStore.fsproj
```

Expected: compiles clean (`.fsi` before `.fs`; `TreatWarningsAsErrors=true`). The library references only
`EvidenceReuse` (F030), `FreshnessKey` (F029), and `Config` (F014) — every one already on F030's transitive
graph.

## 2. Exercise in FSI (Principle I)

The `scripts/prelude.fsx` F047 section loads the packed/public surface and exercises it. Sketch:

```fsharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.FreshnessSensing   // the REAL reader

// Build a real store via the genuine F030 record (Synthetic evidence refs — real refs need gate execution).
let inputs check : FreshnessInputs =
    { Check = CheckId check; Domain = DomainId "build"; Command = None
      Environment = EnvironmentClass.Local
      RuleHash = RuleHash "r1"; CoveredArtifacts = [ ArtifactHash "a1"; ArtifactHash "a2" ]
      CommandVersion = None; GeneratorVersion = GeneratorVersion "g1"
      Base = Revision "b0"; Head = Revision "h0" }

let store =
    EvidenceReuse.empty
    |> EvidenceReuse.record (inputs "fmt") (EvidenceRef "synthetic://fmt")     // SYNTHETIC: disclosed
    |> EvidenceReuse.record (inputs "lint") (EvidenceRef "synthetic://lint")

// SERIALISE → a single fsgg.evidence-reuse-store/v1 document.
let text = EvidenceReuseStore.serialise store
printfn "%s" text                                  // {"schemaVersion":"fsgg.evidence-reuse-store/v1","recorded":[...]}

// ROUND-TRIP through the REAL reader (writes a temp file because realStoreReader reads a path).
let path = System.IO.Path.GetTempFileName()
System.IO.File.WriteAllText(path, text)
match FreshnessSensing.realStoreReader path with
| Ok (Some loaded) -> printfn "round-trip equal: %b" (loaded = store)   // true (SC-001)
| other -> printfn "unexpected: %A" other

// DETERMINISM: same value → byte-identical text (SC-002).
printfn "byte-stable: %b" (EvidenceReuseStore.serialise store = text)

// EMPTY store → well-formed empty document, re-reads as empty (SC-003).
printfn "%s" (EvidenceReuseStore.serialise EvidenceReuse.empty)   // {"schemaVersion":"...","recorded":[]}

// RETAIN to a bound (newest-first); idempotent at/under bound (SC-004).
let bounded = EvidenceReuseStore.retain 1 store
printfn "bounded length: %d" (EvidenceReuse.entries bounded |> List.length)   // 1, newest kept

// PRUNE superseded worlds; record-built stores are already clean ⇒ unchanged (SC-005).
printfn "prune no-op: %b" (EvidenceReuseStore.prune store = store)
```

## 3. Run the tests

```bash
dotnet test tests/FS.GG.Governance.EvidenceReuseStore.Tests/FS.GG.Governance.EvidenceReuseStore.Tests.fsproj
```

| Test file | Proves | Success criterion |
|-----------|--------|-------------------|
| `RoundTripTests.fs` | `serialise` → real `realStoreReader` (temp file) yields a store **equal** to the input, over FsCheck-generated stores; empty store round-trips as empty. | SC-001, SC-003 |
| `DeterminismTests.fs` | Re-serialising the same store yields **byte-identical** output. | SC-002 |
| `RetentionTests.fs` | `retain` is within bound, keeps the newest entries newest-first, is idempotent at/under bound, and every survivor is one of the inputs. | SC-004 |
| `PruningTests.fs` | `prune` removes superseded-world entries, returns a newest-first subset, is a no-op on clean stores, and is verdict-preserving. | SC-005 |
| `SafetyTests.fs` | No operation turns a `mustRecompute` candidate into `reusable`: `decide` pre vs post is identical-or-stricter, over generated candidates × stores. | SC-006 |
| `TotalityTests.fs` | Every operation is pure and total over generated inputs with no filesystem/clock/network access; never throws. | SC-008 |
| `SurfaceDriftTests.fs` | The public surface equals `surface/FS.GG.Governance.EvidenceReuseStore.surface.txt`; the library depends only on the F030 graph + BCL + FSharp.Core (no host/adapter/CLI/sibling edge, no new package). | SC-007, Principle II |

Synthetic evidence references carry the `Synthetic` token in their test names and are listed in the PR
(Principle V) — real evidence references depend on gate execution (Assumptions).

## 4. Re-bless the surface baseline (intentional surface changes only)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.EvidenceReuseStore.Tests/FS.GG.Governance.EvidenceReuseStore.Tests.fsproj
```

This rewrites `surface/FS.GG.Governance.EvidenceReuseStore.surface.txt`. **No** existing baseline is re-blessed
— this row adds one new baseline and touches no merged F029–F046 surface or golden (SC-007).

## What this row does NOT do (deferred — Assumptions / Out of Scope)

- No file is written to disk; no atomic temp+rename, no store-path discovery, no `--store` flag, no wiring into
  `fsgg route` / `fsgg ship` (a later host row).
- No **real** evidence reference is produced (needs gate execution / output-digest capture).
- No wall-clock TTL/age expiry and no `RecordedEvidence` timestamp change.
- No schema-version bump and no change to the read-only reader's accepted shape.
