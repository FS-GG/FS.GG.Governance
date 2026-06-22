module FS.GG.Governance.RouteCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route
open FS.GG.Governance.RouteJson
open FS.GG.Governance.GatesJson
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support


// US1 (the pure composition) + the F046 cache wiring: drive `Loop.update` with literal Msg values over
// real upstream-assembled inputs (a real RepoSnapshot from the F016 core, real TypedFacts from the F014
// core, real SensedFacts from the shared FreshnessSensing edge over the faked sensor). No I/O, no git, no
// clock (Principle IV).

// Drive init → Sensed → Loaded to the Selected phase, returning the model + the gates/baseHead context.
let private toSelected (git) (req: Loop.RunRequest) =
    let snap = snapshotOf git defaultOpts
    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
    let m2, e2 = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
    snap, m1, m2, e2

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "Sensed Ok sets Candidates + Snapshot and emits LoadCatalog (US1)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, e1 = Loop.update (Loop.Sensed(Ok snap)) m0

              Expect.equal m1.Candidates (Some(snap.Changed |> List.map (fun c -> c.Path))) "candidates = snapshot changed paths"
              Expect.equal m1.Snapshot (Some snap) "snapshot kept to derive base/head"
              Expect.equal m1.Phase Loop.Sensed' "advanced to Sensed'"
              Expect.equal e1 [ Loop.LoadCatalog req.Repo ] "emits LoadCatalog for the repo"
          }

          test "Loaded Valid selects gates and emits SenseFreshness + LoadStore (no write yet) (US1)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, _, m2, e2 = toSelected git req

              let selectedGates = (Option.get m2.Result).SelectedGates |> List.map (fun sg -> sg.Gate)
              Expect.equal m2.SelectedGates selectedGates "SelectedGates set from the route result"
              Expect.equal m2.Phase Loop.Selected "advanced to Selected (not Projected — the join waits)"
              Expect.equal
                  e2
                  [ Loop.SenseFreshness(selectedGates, baseHeadOfSnap (Some snap))
                    Loop.LoadStore req.StorePath ]
                  "emits SenseFreshness(selectedGates, baseHead) + LoadStore — and NO write"
          }

          test "the join fires only once BOTH Sensed and Store are Some, then emits two writes (US1, SC-001)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, _, m2, _ = toSelected git req

              let selectedGates = m2.SelectedGates
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed =
                  match FreshnessSensing.senseFreshness fakeSensor selectedGates baseHead with
                  | Ok s -> s
                  | Error e -> failtestf "fake sense failed: %s" e

              // FreshnessSensed first: the join does NOT fire (Store still None) — no effect, no write.
              let m3, e3 = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              Expect.equal e3 [] "join waits — only one input present"
              Expect.notEqual m3.Phase Loop.Projected "still pre-projection"

              // StoreLoaded second: both inputs present ⇒ the join builds the route doc + emits two writes.
              let m4, e4 = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3
              Expect.equal m4.Phase Loop.Projected "join fired ⇒ Projected"

              let result = Option.get m2.Result
              let expectedReport = expectedCacheReport selectedGates baseHead
              let gatesDoc = Option.get m2.GatesDoc
              let routeDoc = RouteJson.ofRouteResult result (Some expectedReport)

              Expect.equal m4.RouteDoc (Some routeDoc) "RouteDoc = ofRouteResult result (Some expectedReport)"
              Expect.equal
                  e4
                  [ Loop.WriteArtifact(Loop.GatesArtifact, req.GatesOut, gatesDoc)
                    Loop.WriteArtifact(Loop.RouteArtifact, req.RouteOut, routeDoc) ]
                  "two WriteArtifact effects with the gates + (cache-bearing) route doc"

              // SC-001: cacheEligibilityEvaluated:true and every selected gate's id appears in the cache section.
              Expect.stringContains routeDoc "\"cacheEligibilityEvaluated\":true" "the cache section is evaluated"
              for g in selectedGates do
                  Expect.stringContains routeDoc (gateIdValue g.Id) "each selected gate id appears in route.json"
          }

          test "empty selection ⇒ evaluated cache section with no per-gate verdicts, exit 0 (US1 sc.3)" {
              // A routine-only change selects no gates; the join still runs over an empty selection.
              let git = gitWithChanges [ 'M', "notes.txt" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap = snapshotOf git defaultOpts
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
              Expect.equal m2.SelectedGates [] "a routine change selects no gates"

              let sensed =
                  match FreshnessSensing.senseFreshness fakeSensor [] (baseHeadOfSnap (Some snap)) with
                  | Ok s -> s
                  | Error e -> failtestf "%s" e

              let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              let m4, _ = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3
              let routeDoc = Option.get m4.RouteDoc
              Expect.stringContains routeDoc "\"cacheEligibilityEvaluated\":true" "evaluated even with no gates"

              // Drive to Done and confirm exit 0.
              let m5, _ = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m4
              let m6, _ = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m5
              let m7, _ = Loop.update Loop.Emitted m6
              Expect.equal m7.Exit Loop.Success "route always exits 0"
          }

          test "both Wrote Ok then Emitted reach Done/Success, emitting the summary (US1)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, _, m2, _ = toSelected git req
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e
              let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              let m4, _ = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3

              let m5, e5 = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m4
              Expect.equal m5.Phase Loop.Persisted "first write ack ⇒ Persisted"
              Expect.equal e5 [] "no effect on the first write ack"

              let m6, e6 = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m5
              Expect.equal e6 [ Loop.EmitSummary(Loop.render m5 req.Format) ] "second write ack ⇒ EmitSummary"

              let m7, e7 = Loop.update Loop.Emitted m6
              Expect.equal m7.Phase Loop.Done "Emitted ⇒ Done"
              Expect.equal m7.Exit Loop.Success "Done ⇒ Success"
              Expect.equal e7 [] "terminal: no further effects"
          }

          test "the success summary lists each gate's cache outcome consistent with route.json (FR-015, C2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, _, m2, _ = toSelected git req
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e
              let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              let m4, _ = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3

              let summary = Loop.render m4 Loop.Text
              Expect.stringContains summary "cache-eligibility:" "summary carries the cache outcome header"
              Expect.stringContains summary "must recompute:" "summary lists the must-recompute block"
              // An absent store ⇒ every selected gate must-recompute; the summary names each gate.
              for g in m2.SelectedGates do
                  Expect.stringContains summary (gateIdValue g.Id) "summary names each selected gate's cache outcome"
          }

          test "render is byte-stable for a fixed Model in both formats (SC-002)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, _, m2, _ = toSelected git req
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e
              let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              let m4, _ = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3

              Expect.equal (Loop.render m4 Loop.Text) (Loop.render m4 Loop.Text) "Text render is pure"
              Expect.equal (Loop.render m4 Loop.Json) (Loop.render m4 Loop.Json) "Json render is pure"
          } ]
