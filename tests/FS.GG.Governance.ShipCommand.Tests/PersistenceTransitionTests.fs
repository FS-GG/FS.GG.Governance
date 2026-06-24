module FS.GG.Governance.ShipCommand.Tests.PersistenceTransitionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F048 US1/US2/US3 (ship) — the persistence DECISION as a pure `Loop.update` transition (SC-008): the emitted
// `PersistStore` content equals F047's `prune |> retain |> serialise`; flag-off / degraded emit NO write; and
// `StorePersisted(Error _)` is non-fatal — ship's exit stays governed SOLELY by `ExitCodeBasis`, never
// `ToolError`/`Blocked` (the ship-distinguishing invariant).

let private okOrFail =
    function
    | Ok v -> v
    | Error e -> failtestf "unexpected Error: %A" e

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

// Drive a full flow to Done, returning the terminal exit, for a given persistence flag + store ack.
let private exitFor (persist: bool) (storeAck: Result<unit, string> option) =
    let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = persist }
    let m3, _ = toBeforeStore req
    let m4raw, e4 = Loop.update (Loop.StoreLoaded(Ok(storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]))) m3
    let m4, _ = runExecuteEffect fakeExecPort m4raw e4
    let m5, _ = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m4

    let m6 =
        match storeAck with
        | Some ack ->
            let m, _ = Loop.update (Loop.StorePersisted ack) m5
            m
        | None -> m5

    let m7, _ = Loop.update Loop.Emitted m6
    m7.Exit

[<Tests>]
let tests =
    testList
        "PersistenceTransition"
        [ test "PersistStore=true + non-degraded ⇒ exactly one PersistStore(StorePath, pipeline) + the audit write (T014/T021)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt"; persistInputs "build" "h2", syntheticRef "bld" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, baseHead = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let _, e5 = runExecuteEffect fakeExecPort m4 e4
              let grown = expectedGrownStoreAt "." fakeExecPort fakeSensor validCatalog store m4.SelectedGates baseHead

              Expect.equal (persistEffectsOf e5) [ req.StorePath, expectedContent grown ] "PersistStore(StorePath, F047 pipeline over the grown store)"
              Expect.equal
                  (e5 |> List.filter (function Loop.WriteArtifact _ -> true | _ -> false) |> List.length)
                  1
                  "the single audit write is unchanged"
          }

          test "PersistStore=false ⇒ NO PersistStore effect; only the audit write (T014, SC-006)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let m3, _ = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let _, e5 = runExecuteEffect fakeExecPort m4 e4
              Expect.isEmpty (persistEffectsOf e5) "no PersistStore effect when the flag is off"
          }

          test "content uses the FULL prune|>retain|>serialise pipeline, not bare serialise (T021/US2)" {
              let many =
                  [ for n in 1..(EvidenceReuseStore.defaultRetentionBound + 5) -> persistInputs "format" (sprintf "h%d" n), syntheticRef (sprintf "e%d" n) ]
              let store = storeOf many
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, baseHead = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let _, e5 = runExecuteEffect fakeExecPort m4 e4
              let grown = expectedGrownStoreAt "." fakeExecPort fakeSensor validCatalog store m4.SelectedGates baseHead

              match persistEffectsOf e5 with
              | [ _, content ] ->
                  Expect.equal content (expectedContent grown) "content = prune|>retain|>serialise over the grown store"
                  Expect.notEqual content (EvidenceReuseStore.serialise grown) "and NOT the un-bounded serialise"
              | other -> failtestf "expected one PersistStore, got %A" other
          }

          test "degraded store ⇒ NO PersistStore, non-fatal don't-clobber note (T026/D6)" {
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, _ = toBeforeStore req
              let m4, e4 = Loop.update (Loop.StoreLoaded(Error "synthetic malformed store")) m3
              let m5, e5 = runExecuteEffect fakeExecPort m4 e4
              Expect.isEmpty (persistEffectsOf e5) "a degraded load is never persisted"
              Expect.isTrue m4.StoreDegraded "StoreDegraded is set"
              Expect.isTrue (m5.CacheNotes |> List.exists (fun n -> n.Contains "left untouched")) "don't-clobber note present"
          }

          test "ship exit is governed SOLELY by ExitCodeBasis — a StorePersisted(Error) never makes it ToolError (T026)" {
              let exitOff = exitFor false None
              let exitOkAck = exitFor true (Some(Ok()))
              let exitErrAck = exitFor true (Some(Error "no space left on device"))

              // The verdict basis governs the exit (here `Blocked` — a real blocking verdict); the store ack
              // must not perturb it. A failed store write must NEVER turn into the `ToolError` exit.
              Expect.notEqual exitErrAck Loop.ToolError "a failed store write never becomes ToolError"
              Expect.equal exitErrAck exitOff "exit with a failed store write == the no-persist exit (basis-governed)"
              Expect.equal exitOkAck exitOff "exit with a successful store write == the no-persist exit"
          }

          test "StorePersisted(Error _) leaves the audit doc unchanged and appends a non-fatal note (T026/FR-006)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, _ = toBeforeStore req
              let m4raw, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let m4, _ = runExecuteEffect fakeExecPort m4raw e4
              let m5, _ = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m4
              let m6, e6 = Loop.update (Loop.StorePersisted(Error "no space left on device")) m5

              Expect.equal m6.AuditDoc m4.AuditDoc "audit.json content is unchanged by the store ack"
              Expect.isTrue
                  (m6.CacheNotes |> List.exists (fun n -> n.Contains "store not persisted" && n.Contains "run unaffected"))
                  "a non-fatal note is appended"
              Expect.isTrue (e6 |> List.exists (function Loop.EmitSummary _ -> true | _ -> false)) "the store ack releases the summary"
          }

          test "with persistence on, the summary waits for the store ack, not the audit write (D10)" {
              let store = storeOf [ persistInputs "format" "h1", syntheticRef "fmt" ]
              let req = { requestFor Loop.DefaultRange Loop.Text with PersistStore = true }
              let m3, _ = toBeforeStore req
              let m4raw, e4 = Loop.update (Loop.StoreLoaded(Ok store)) m3
              let m4, _ = runExecuteEffect fakeExecPort m4raw e4
              let m5, e5 = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m4
              Expect.equal e5 [] "audit write ack: no summary yet (awaiting the store ack)"
              let _, e6 = Loop.update (Loop.StorePersisted(Ok())) m5
              Expect.isTrue (e6 |> List.exists (function Loop.EmitSummary _ -> true | _ -> false)) "store Ok ack ⇒ summary"
          } ]
