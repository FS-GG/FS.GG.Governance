module FS.GG.Governance.RouteCommand.Tests.PersistenceTransitionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F048 US1/US2/US3 — the persistence DECISION as a pure `Loop.update` transition (SC-008, no filesystem):
// the emitted `PersistStore` effect content equals F047's `prune |> retain |> serialise`; the flag-off and
// degraded cases emit NO write; and `StorePersisted(Error _)` is non-fatal (no Exit/artifact change). The
// REAL atomic write + reader round-trip is proven in PersistenceEdgeTests.

let private okOrFail =
    function
    | Ok v -> v
    | Error e -> failtestf "unexpected Error: %A" e

// Drive init → Sensed → Loaded → FreshnessSensed to just BEFORE the StoreLoaded join, over real upstream
// inputs (real F016 snapshot, real F014 facts, real sensed facts from the faked sensor).
let private toBeforeStore (req: Loop.RunRequest) =
    let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
    let snap = snapshotOf git defaultOpts
    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
    let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
    let baseHead = baseHeadOfSnap (Some snap)
    let sensed = FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead |> okOrFail
    let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
    m3, baseHead

// The F047 pipeline the host must emit (data-model §2).
let private expectedContent (store: ReuseStore) : string =
    store
    |> EvidenceReuseStore.prune
    |> EvidenceReuseStore.retain EvidenceReuseStore.defaultRetentionBound
    |> EvidenceReuseStore.serialise

let private persistEffectsOf (effects: Loop.Effect list) =
    effects
    |> List.choose (function
        | Loop.PersistStore(p, c) -> Some(p, c)
        | _ -> None)

[<Tests>]
let tests =
    testList
        "PersistenceTransition"
        [ test "PersistStore=true + non-degraded store ⇒ emits exactly one PersistStore(StorePath, pipeline) (T012/T019)" {
              // A store within bound and free of strictly-superseded entries, so `retain (prune s) = s` and
              // `serialise s` is the expected content even after the full-pipeline derivation (T012 precond).
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt"; persistInputs "build" "h2", syntheticRef "bld" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, baseHead = toBeforeStore req
              // F052: StoreLoaded requests ExecuteGates; the writes + persist arrive on GatesExecuted.
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let _, e5 = runExecuteEffect fakeExecPort m4 e4

              // The persisted content is the F047 pipeline over the GROWN store (the executed gates captured).
              let grown = expectedGrownStore fakeExecPort fakeSensor validCatalog store m4.SelectedGates baseHead
              let persists = persistEffectsOf e5
              Expect.equal (List.length persists) 1 "exactly one PersistStore effect"
              Expect.equal persists [ req.StorePath, expectedContent grown ] "PersistStore(StorePath, F047 pipeline over the grown store)"

              // The two artifact writes are still emitted, unchanged.
              let writes = e5 |> List.filter (function Loop.WriteArtifact _ -> true | _ -> false)
              Expect.equal (List.length writes) 2 "the two WriteArtifact effects are unchanged"
          }

          test "PersistStore=false ⇒ NO PersistStore effect; only the two writes (T012, SC-006)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = requestFor Loop.DefaultRange Loop.Text // PersistStore = false
              let m3, _ = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let _, e5 = runExecuteEffect fakeExecPort m4 e4

              Expect.isEmpty (persistEffectsOf e5) "no PersistStore effect when the flag is off"
              Expect.equal
                  (e5 |> List.filter (function Loop.PersistStore _ -> false | _ -> true) |> List.length)
                  2
                  "exactly the two writes, nothing else"
          }

          test "content uses the FULL prune|>retain|>serialise pipeline, not bare serialise (T019/US2)" {
              // An over-bound + strictly-superseded store: bare `serialise store` would differ from the pipeline.
              let many =
                  [ for n in 1..(EvidenceReuseStore.defaultRetentionBound + 5) -> persistInputs "format" (sprintf "h%d" n), syntheticRef (sprintf "e%d" n) ]
              let store = storeOf many
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, baseHead = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let _, e5 = runExecuteEffect fakeExecPort m4 e4
              let grown = expectedGrownStore fakeExecPort fakeSensor validCatalog store m4.SelectedGates baseHead

              match persistEffectsOf e5 with
              | [ _, content ] ->
                  Expect.equal content (expectedContent grown) "content = prune|>retain|>serialise over the grown store"
                  Expect.notEqual content (EvidenceReuseStore.serialise grown) "and is NOT the un-bounded serialise"
              | other -> failtestf "expected one PersistStore, got %A" other
          }

          test "degraded store (malformed on load) ⇒ NO PersistStore, non-fatal don't-clobber note (T024/D6)" {
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, _ = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Error "synthetic malformed store")) m3
              // The degraded load still classifies + executes; the don't-clobber decision lands on GatesExecuted.
              let m5, e5 = runExecuteEffect fakeExecPort m4 e4

              Expect.isEmpty (persistEffectsOf e5) "a degraded load is never persisted (don't clobber)"
              Expect.isTrue m4.StoreDegraded "StoreDegraded is set"
              Expect.isTrue
                  (m5.CacheNotes |> List.exists (fun n -> n.Contains "left untouched"))
                  "a non-fatal don't-clobber note is appended"
          }

          test "StorePersisted(Error _) changes neither Exit nor the emitted route.json/gates.json (T024/FR-006)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, _ = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let m5, e5 = runExecuteEffect fakeExecPort m4 e4

              let exitBefore = m5.Exit

              // First the two write acks, then a FAILED store ack.
              let m6, _ = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m5
              let m7, _ = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m6
              let m8, e8 = Loop.update (Loop.StorePersisted(Error "no space left on device")) m7

              Expect.equal m8.Exit exitBefore "Exit unchanged by a failed store write"
              Expect.isTrue m8.PersistAcked "the ack is recorded"
              Expect.isTrue
                  (m8.CacheNotes |> List.exists (fun n -> n.Contains "store not persisted" && n.Contains "run unaffected"))
                  "a non-fatal note is appended"
              // No WriteArtifact effect is re-emitted by the store ack (artifacts are not rewritten).
              Expect.isEmpty (e8 |> List.filter (function Loop.WriteArtifact _ -> true | _ -> false)) "no artifact re-write"
              // The emitted artifacts are exactly the ones from the projection — the store ack did not alter them.
              Expect.equal (m5.RouteDoc) (m8.RouteDoc) "route.json content is unchanged by the store ack"
              ignore e5

              // And the summary IS emitted (gated until the store ack), so the run completes.
              Expect.equal (List.length e8) 1 "the store ack releases the summary"
              Expect.isTrue (e8 |> List.exists (function Loop.EmitSummary _ -> true | _ -> false)) "EmitSummary released"
          }

          test "with persistence on, the summary waits for the store ack, not the second write (T016/D10)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, _ = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let m4b, _ = runExecuteEffect fakeExecPort m4 e4
              let m5, e5 = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m4b
              let m6, e6 = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m5

              Expect.equal e5 [] "first write ack: no summary"
              Expect.equal e6 [] "second write ack: STILL no summary (awaiting the store ack)"

              let _, e7 = Loop.update (Loop.StorePersisted(Ok())) m6
              Expect.isTrue (e7 |> List.exists (function Loop.EmitSummary _ -> true | _ -> false)) "store Ok ack ⇒ summary"
          } ]
