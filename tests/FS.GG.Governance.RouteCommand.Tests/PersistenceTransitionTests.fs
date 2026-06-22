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
    m3

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
              let m3 = toBeforeStore req
              let _, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3

              let persists = persistEffectsOf e4
              Expect.equal (List.length persists) 1 "exactly one PersistStore effect"
              Expect.equal persists [ req.StorePath, expectedContent store ] "PersistStore(StorePath, F047 pipeline content)"

              // The two artifact writes are still emitted, unchanged.
              let writes = e4 |> List.filter (function Loop.WriteArtifact _ -> true | _ -> false)
              Expect.equal (List.length writes) 2 "the two WriteArtifact effects are unchanged"
          }

          test "PersistStore=false ⇒ NO PersistStore effect; only the two writes (T012, SC-006)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = requestFor Loop.DefaultRange Loop.Text // PersistStore = false
              let m3 = toBeforeStore req
              let _, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3

              Expect.isEmpty (persistEffectsOf e4) "no PersistStore effect when the flag is off"
              Expect.equal
                  (e4 |> List.filter (function Loop.PersistStore _ -> false | _ -> true) |> List.length)
                  2
                  "exactly the two writes, nothing else"
          }

          test "content uses the FULL prune|>retain|>serialise pipeline, not bare serialise (T019/US2)" {
              // An over-bound + strictly-superseded store: bare `serialise store` would differ from the pipeline.
              let many =
                  [ for n in 1..(EvidenceReuseStore.defaultRetentionBound + 5) -> persistInputs "format" (sprintf "h%d" n), syntheticRef (sprintf "e%d" n) ]
              let store = storeOf many
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3 = toBeforeStore req
              let _, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3

              match persistEffectsOf e4 with
              | [ _, content ] ->
                  Expect.equal content (expectedContent store) "content = prune|>retain|>serialise"
                  Expect.notEqual content (EvidenceReuseStore.serialise store) "and is NOT the un-bounded serialise"
              | other -> failtestf "expected one PersistStore, got %A" other
          }

          test "degraded store (malformed on load) ⇒ NO PersistStore, non-fatal don't-clobber note (T024/D6)" {
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3 = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Error "synthetic malformed store")) m3

              Expect.isEmpty (persistEffectsOf e4) "a degraded load is never persisted (don't clobber)"
              Expect.isTrue m4.StoreDegraded "StoreDegraded is set"
              Expect.isTrue
                  (m4.CacheNotes |> List.exists (fun n -> n.Contains "left untouched"))
                  "a non-fatal don't-clobber note is appended"
          }

          test "StorePersisted(Error _) changes neither Exit nor the emitted route.json/gates.json (T024/FR-006)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3 = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3

              // Capture the artifacts the run emitted (from the write effects) and the pre-ack exit.
              let writesBefore = e4 |> List.choose (function Loop.WriteArtifact(k, p, c) -> Some(k, p, c) | _ -> None)
              let exitBefore = m4.Exit

              // First the two write acks, then a FAILED store ack.
              let m5, _ = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m4
              let m6, _ = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m5
              let m7, e7 = Loop.update (Loop.StorePersisted(Error "no space left on device")) m6

              Expect.equal m7.Exit exitBefore "Exit unchanged by a failed store write"
              Expect.isTrue m7.PersistAcked "the ack is recorded"
              Expect.isTrue
                  (m7.CacheNotes |> List.exists (fun n -> n.Contains "store not persisted" && n.Contains "run unaffected"))
                  "a non-fatal note is appended"
              // No WriteArtifact effect is re-emitted by the store ack (artifacts are not rewritten).
              Expect.isEmpty (e7 |> List.filter (function Loop.WriteArtifact _ -> true | _ -> false)) "no artifact re-write"
              // The emitted artifacts are exactly the ones from the join — the store ack did not alter them.
              Expect.equal (m4.RouteDoc) (m7.RouteDoc) "route.json content is unchanged by the store ack"
              ignore writesBefore

              // And the summary IS emitted (gated until the store ack), so the run completes.
              Expect.equal (List.length e7) 1 "the store ack releases the summary"
              Expect.isTrue (e7 |> List.exists (function Loop.EmitSummary _ -> true | _ -> false)) "EmitSummary released"
          }

          test "with persistence on, the summary waits for the store ack, not the second write (T016/D10)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3 = toBeforeStore req
              let m4, _ = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let m5, e5 = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m4
              let m6, e6 = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m5

              Expect.equal e5 [] "first write ack: no summary"
              Expect.equal e6 [] "second write ack: STILL no summary (awaiting the store ack)"

              let _, e7 = Loop.update (Loop.StorePersisted(Ok())) m6
              Expect.isTrue (e7 |> List.exists (function Loop.EmitSummary _ -> true | _ -> false)) "store Ok ack ⇒ summary"
          } ]
