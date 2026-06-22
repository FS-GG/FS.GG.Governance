module FS.GG.Governance.ShipCommand.Tests.PersistenceEdgeTests

open System.IO
open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F048 (ship) — the impure EDGE: drive the REAL `Interpreter.run` with `realPorts` against a real temp git
// repo, then re-read the persisted store through the REAL `FreshnessSensing.realStoreReader`. Covers
// SC-001/002/003/005, FR-007, and the ship-distinguishing invariant: verdict / partition / enforcement /
// audit.json / exit code are byte-for-byte identical with vs without `--persist-store` (SC-004), and a
// store-write failure is non-fatal (exit governed solely by the verdict basis).

let private parseOrFail argv =
    match Loop.parse argv with
    | Ok r -> r
    | Error e -> failtestf "parse failed: %A" e

let private runShip (req: Loop.RunRequest) : Loop.Model =
    Interpreter.run (Interpreter.realPorts req.Repo) req

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
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runShip req |> ignore
                  Expect.equal (readStore req.StorePath) (Some store) "persisted store re-reads to the loaded store")
          }

          test "AC-2: an absent store ⇒ a well-formed empty v1 is written, re-read as the empty store" {
              withTempRepo (fun dir ->
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  Expect.isFalse (File.Exists req.StorePath) "no store on disk before the run"
                  runShip req |> ignore
                  Expect.isTrue (File.Exists req.StorePath) "an empty v1 store file is written"
                  Expect.equal (readStore req.StorePath) (Some EvidenceReuse.empty) "re-reads as EvidenceReuse.empty")
          }

          test "SC-002: two identical persistence runs write byte-identical store files" {
              withTempRepo (fun dir ->
                  let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise store)
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runShip req |> ignore
                  let bytes1 = File.ReadAllText req.StorePath
                  runShip req |> ignore
                  Expect.equal (File.ReadAllText req.StorePath) bytes1 "store file is byte-identical across runs")
          }

          test "FR-007: the write target is exactly --store; no default location used" {
              withTempRepo (fun dir ->
                  let custom = Path.Combine(dir, "custom", "my-store.json")
                  let defaultPath = Path.Combine(dir, "readiness", "evidence-reuse.json")
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store"; "--store"; custom ]
                  runShip req |> ignore
                  Expect.isTrue (File.Exists custom) "the custom store path is written"
                  Expect.isFalse (File.Exists defaultPath) "the default store path is NOT written")
          }

          test "SC-003: an over-bound store persists within defaultRetentionBound, newest-first, survivors ⊆ loaded" {
              withTempRepo (fun dir ->
                  let entries = [ for n in 1..(EvidenceReuseStore.defaultRetentionBound + 7) -> persistInputs "format" (sprintf "h%d" n), syntheticRef (sprintf "e%d" n) ]
                  let loaded = storeOf entries
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise loaded)
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runShip req |> ignore

                  let persistedEntries = readStore req.StorePath |> Option.defaultValue EvidenceReuse.empty |> EvidenceReuse.entries
                  let loadedEntries = EvidenceReuse.entries loaded
                  Expect.isLessThanOrEqual (List.length persistedEntries) EvidenceReuseStore.defaultRetentionBound "within bound"
                  Expect.equal persistedEntries (List.truncate EvidenceReuseStore.defaultRetentionBound loadedEntries) "newest-first head of the loaded store"
                  for e in persistedEntries do
                      Expect.contains loadedEntries e "every survivor is byte-for-byte a loaded entry")
          }

          test "SC-003: a strictly-superseded entry is pruned" {
              withTempRepo (fun dir ->
                  let newer = { Inputs = persistInputs "format" "h1"; Evidence = syntheticRef "new" }
                  let older = { Inputs = persistInputs "format" "h1"; Evidence = syntheticRef "old" }
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise (ReuseStore [ newer; older ]))
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  runShip req |> ignore
                  let persisted = readStore req.StorePath |> Option.defaultValue EvidenceReuse.empty
                  Expect.equal (EvidenceReuse.entries persisted) [ newer ] "the superseded (older) entry is pruned")
          }

          test "SC-004: verdict, exit, and audit.json are byte-for-byte identical with vs without --persist-store" {
              withTempRepo (fun dir ->
                  let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
                  writeFile dir "readiness/evidence-reuse.json" (EvidenceReuseStore.serialise store)

                  let reqOff = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1" ]
                  let modelOff = runShip reqOff
                  let auditOff = File.ReadAllText reqOff.AuditOut

                  let reqOn = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  let modelOn = runShip reqOn
                  let auditOn = File.ReadAllText reqOn.AuditOut

                  Expect.equal auditOn auditOff "audit.json is byte-identical (verdict decoupled from the store write)"
                  Expect.equal modelOn.Exit modelOff.Exit "exit code is unchanged by persistence"
                  Expect.equal modelOn.Decision modelOff.Decision "the ship verdict/partition/enforcement is unchanged")
          }

          test "SC-005: an unwritable store target is non-fatal — exit unchanged, audit.json intact, no partial file, a note" {
              withTempRepo (fun dir ->
                  writeFile dir "blocker" "x"
                  let badStore = Path.Combine(dir, "blocker", "evidence.json")

                  let reqBase = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1" ]
                  let modelBase = runShip reqBase
                  let auditBase = File.ReadAllText reqBase.AuditOut

                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store"; "--store"; badStore ]
                  let model = runShip req
                  Expect.equal model.Exit modelBase.Exit "exit code is unchanged by a failed store write"
                  Expect.equal (File.ReadAllText req.AuditOut) auditBase "audit.json is unchanged by the failed store write"
                  Expect.isFalse (File.Exists badStore) "no partial store file"
                  Expect.isEmpty (tmpFiles dir) "no leftover .tmp-* file"
                  Expect.isTrue (model.CacheNotes |> List.exists (fun n -> n.Contains "store not persisted")) "a non-fatal note records the failed persist")
          }

          test "Scenario 7: a malformed on-disk store is left UNTOUCHED with a don't-clobber note" {
              withTempRepo (fun dir ->
                  let garbage = "{ this is not valid json"
                  writeFile dir "readiness/evidence-reuse.json" garbage
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1"; "--persist-store" ]
                  let model = runShip req
                  Expect.equal (File.ReadAllText req.StorePath) garbage "the malformed file is NOT overwritten"
                  Expect.isTrue (model.CacheNotes |> List.exists (fun n -> n.Contains "left untouched")) "a don't-clobber note is present")
          }

          test "SC-006: with --persist-store ABSENT, no store file is written" {
              withTempRepo (fun dir ->
                  let req = parseOrFail [ "ship"; "--repo"; dir; "--since"; "HEAD~1" ]
                  Expect.isFalse req.PersistStore "the flag is off by default"
                  runShip req |> ignore
                  Expect.isFalse (File.Exists req.StorePath) "no store file is written when the flag is absent")
          } ]
