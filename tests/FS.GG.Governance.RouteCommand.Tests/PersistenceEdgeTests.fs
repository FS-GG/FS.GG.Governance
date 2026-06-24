module FS.GG.Governance.RouteCommand.Tests.PersistenceEdgeTests

open System.IO
open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F048 — the impure EDGE: drive the REAL `Interpreter.run` with `realPorts` against a real temp git repo,
// then re-read the persisted store through the REAL `FreshnessSensing.realStoreReader`. Genuine evidence
// (no `Synthetic` token on the round-trip — the store value is real; only the opaque evidence refs are
// disclosed-synthetic). Covers SC-001/002/003/005, FR-007, and persistence-off byte-identity (SC-006).

let private parseOrFail argv =
    match Loop.parse argv with
    | Ok r -> r
    | Error e -> failtestf "parse failed: %A" e

// F052: the gates now RUN. Override the real execution port with the deterministic all-pass fake so the
// store grows reproducibly (no real `dotnet` process) while git + filesystem stay real (the F048 edge proof).
let private runRoute (req: Loop.RunRequest) : Loop.Model =
    Interpreter.run { Interpreter.realPorts req.Repo with Execute = fakeExecPort } req

let private tmpFiles (dir: string) =
    Directory.GetFiles(dir, "*.tmp-*", SearchOption.AllDirectories)

[<Tests>]
let tests =
    testList
        "PersistenceEdge"
        [ test "SC-001: a seeded non-empty v1 store round-trips losslessly through the real reader" {
              withTempRepo (fun dir ->
                  let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt"; persistInputs "build" "h2", syntheticRef "bld" ]
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise store)

                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  let model = runRoute req
                  Expect.equal model.Exit Loop.Success "run exits Success"

                  // F052: route now executes the selected command-gates and GROWS the store; the persisted
                  // store is the F047 pipeline over the seed + the captured evidence (still re-reads losslessly).
                  Expect.equal (readStore req.StorePath) (Some(expectedPersistedRepo dir store)) "persisted store = pipeline over the grown store")
          }

          test "AC-2: an absent store ⇒ a well-formed empty v1 is written, re-read as the empty store" {
              withTempRepo (fun dir ->
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  Expect.isFalse (File.Exists req.StorePath) "no store on disk before the run"

                  let model = runRoute req
                  Expect.equal model.Exit Loop.Success "run exits Success"
                  Expect.isTrue (File.Exists req.StorePath) "a v1 store file is written (parent dir created)"
                  // F052: from an absent store, the run captures the executed gates ⇒ a populated grown store.
                  Expect.equal (readStore req.StorePath) (Some(expectedPersistedRepo dir EvidenceReuse.empty)) "re-reads as the grown store")
          }

          test "SC-002: two identical persistence runs write byte-identical store files" {
              withTempRepo (fun dir ->
                  let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise store)
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]

                  runRoute req |> ignore
                  let bytes1 = File.ReadAllText req.StorePath
                  runRoute req |> ignore
                  let bytes2 = File.ReadAllText req.StorePath
                  Expect.equal bytes2 bytes1 "store file is byte-identical across runs")
          }

          test "FR-007: the write target is exactly --store (a custom non-default path); no default location used" {
              withTempRepo (fun dir ->
                  let custom = Path.Combine(dir, "custom", "my-store.json")
                  let defaultPath = Path.Combine(dir, "readiness", "evidence-reuse.json")
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store"; "--store"; custom ]
                  Expect.equal req.StorePath custom "StorePath is the custom --store value"

                  runRoute req |> ignore
                  Expect.isTrue (File.Exists custom) "the custom store path is written"
                  Expect.isFalse (File.Exists defaultPath) "the default store path is NOT written")
          }

          test "SC-003: an over-bound store persists within defaultRetentionBound, newest-first, survivors ⊆ loaded" {
              withTempRepo (fun dir ->
                  let entries = [ for n in 1..(EvidenceReuseStore.defaultRetentionBound + 7) -> persistInputs "format" (sprintf "h%d" n), syntheticRef (sprintf "e%d" n) ]
                  let loaded = storeOf entries
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise loaded)
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runRoute req |> ignore

                  let persisted = readStore req.StorePath |> Option.defaultValue EvidenceReuse.empty
                  let persistedEntries = EvidenceReuse.entries persisted
                  // F052: the grown store (seed + captured) is pruned/retained to the bound, newest-first.
                  Expect.isLessThanOrEqual (List.length persistedEntries) EvidenceReuseStore.defaultRetentionBound "within bound"
                  Expect.equal persisted (expectedPersistedRepo dir loaded) "persisted = F047 pipeline over the grown store")
          }

          test "SC-003: a strictly-superseded entry is pruned; an already-clean store persists value-equal" {
              withTempRepo (fun dir ->
                  // Hand-built raw store with a strictly-superseded duplicate (same inputs, older evidence) the
                  // reader reads back verbatim; prune must drop the older one.
                  let newer = { Inputs = persistInputs "format" "h1"; Evidence = syntheticRef "new" }
                  let older = { Inputs = persistInputs "format" "h1"; Evidence = syntheticRef "old" }
                  let raw = ReuseStore [ newer; older ]
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise raw)
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runRoute req |> ignore

                  let persisted = readStore req.StorePath |> Option.defaultValue EvidenceReuse.empty
                  // The superseded (older) duplicate is pruned; the grown store is the F047 pipeline result.
                  Expect.isFalse (EvidenceReuse.entries persisted |> List.contains older) "the superseded (older) entry is pruned"
                  Expect.equal persisted (expectedPersistedRepo dir raw) "persisted = F047 pipeline over the grown store")
          }

          test "SC-004: route.json per-gate cache verdicts are byte-identical with vs without --persist-store" {
              withTempRepo (fun dir ->
                  let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise store)

                  let reqOff = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1" ]
                  runRoute reqOff |> ignore
                  let routeOff = File.ReadAllText reqOff.RouteOut

                  let reqOn = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runRoute reqOn |> ignore
                  let routeOn = File.ReadAllText reqOn.RouteOut
                  Expect.equal routeOn routeOff "route.json verdicts are decoupled from the store write")
          }

          test "SC-005: an unwritable store target is non-fatal — exit unchanged, route.json intact, no partial file, a note" {
              withTempRepo (fun dir ->
                  // Place a FILE where the store's parent dir would be, so writeAtomic's CreateDirectory throws.
                  writeFile dir "blocker" "x"
                  let badStore = Path.Combine(dir, "blocker", "evidence.json")

                  // Baseline route.json from a clean (no-persist) run.
                  let reqBase = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1" ]
                  runRoute reqBase |> ignore
                  let routeBase = File.ReadAllText reqBase.RouteOut

                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store"; "--store"; badStore ]
                  let model = runRoute req
                  Expect.equal model.Exit Loop.Success "a store-write failure does not change the exit code"
                  Expect.equal (File.ReadAllText req.RouteOut) routeBase "route.json is unchanged by the failed store write"
                  Expect.isFalse (File.Exists badStore) "no partial store file"
                  Expect.isEmpty (tmpFiles dir) "no leftover .tmp-* file"
                  Expect.isTrue
                      (model.CacheNotes |> List.exists (fun n -> n.Contains "store not persisted"))
                      "a non-fatal note records the failed persist")
          }

          test "Scenario 7: a malformed on-disk store is left UNTOUCHED with a don't-clobber note" {
              withTempRepo (fun dir ->
                  let garbage = "{ this is not valid json"
                  writeFile dir "readiness/evidence-reuse.json" garbage
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  let model = runRoute req
                  Expect.equal model.Exit Loop.Success "run still succeeds"
                  Expect.equal (File.ReadAllText req.StorePath) garbage "the malformed file is NOT overwritten"
                  Expect.isTrue
                      (model.CacheNotes |> List.exists (fun n -> n.Contains "left untouched"))
                      "a don't-clobber note is present")
          }

          test "SC-006: with --persist-store ABSENT, no store file is written" {
              withTempRepo (fun dir ->
                  let req = parseOrFail [ "route"; "--repo"; dir; "--since"; "HEAD~1" ]
                  Expect.isFalse req.PersistStore "the flag is off by default"
                  runRoute req |> ignore
                  Expect.isFalse (File.Exists req.StorePath) "no store file is written when the flag is absent")
          } ]
