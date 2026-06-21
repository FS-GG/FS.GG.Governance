module FS.GG.Governance.RouteCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Route
open FS.GG.Governance.RouteJson
open FS.GG.Governance.GatesJson
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support


// US1 (the pure composition) + US3 (render purity): drive `Loop.update` with literal Msg values over
// real upstream-assembled inputs (a real RepoSnapshot from the F016 core, real TypedFacts from the
// F014 core) and assert the next Model + emitted Effects. No I/O, no git, no clock (Principle IV).

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "Sensed Ok sets Candidates from the snapshot and emits LoadCatalog (US1)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, e1 = Loop.update (Loop.Sensed(Ok snap)) m0

              Expect.equal m1.Candidates (Some(snap.Changed |> List.map (fun c -> c.Path))) "candidates = snapshot changed paths"
              Expect.equal m1.Phase Loop.Sensed' "advanced to Sensed'"
              Expect.equal e1 [ Loop.LoadCatalog req.Repo ] "emits LoadCatalog for the repo"
          }

          test "Loaded Valid runs the cores verbatim, projects both docs, emits two WriteArtifact (US1, SC-001)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let facts = factsOf validCatalog
              let m2, e2 = Loop.update (Loop.Loaded(Valid facts)) m1

              // Re-derive directly from the real cores and assert the composition carried them verbatim (FR-004).
              let candidates = m1.Candidates |> Option.defaultValue []
              let report = Routing.route facts candidates
              let registry = Gates.buildRegistry facts
              let findings = Findings.findUnknownGovernedPaths facts report
              let expected = Route.select registry report findings
              let gatesDoc = GatesJson.ofGateRegistry registry
              let routeDoc = RouteJson.ofRouteResult expected

              Expect.equal m2.Result (Some expected) "Result = Route.select of the same inputs"
              Expect.equal m2.GatesDoc (Some gatesDoc) "GatesDoc = GatesJson projection"
              Expect.equal m2.RouteDoc (Some routeDoc) "RouteDoc = RouteJson projection"
              Expect.equal m2.Phase Loop.Projected "advanced to Projected"

              Expect.equal
                  e2
                  [ Loop.WriteArtifact(Loop.GatesArtifact, req.GatesOut, gatesDoc)
                    Loop.WriteArtifact(Loop.RouteArtifact, req.RouteOut, routeDoc) ]
                  "exactly two WriteArtifact effects with the request paths and projected doc strings"
          }

          test "both Wrote Ok then Emitted reach Done/Success, emitting the summary (US1)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
              let m3, e3 = Loop.update (Loop.Wrote(Loop.GatesArtifact, Ok())) m2
              Expect.equal m3.Phase Loop.Persisted "first write ack ⇒ Persisted"
              Expect.equal e3 [] "no effect on the first write ack"

              let m4, e4 = Loop.update (Loop.Wrote(Loop.RouteArtifact, Ok())) m3
              Expect.equal e4 [ Loop.EmitSummary(Loop.render m3 req.Format) ] "second write ack ⇒ EmitSummary"

              let m5, e5 = Loop.update Loop.Emitted m4
              Expect.equal m5.Phase Loop.Done "Emitted ⇒ Done"
              Expect.equal m5.Exit Loop.Success "Done ⇒ Success"
              Expect.equal e5 [] "terminal: no further effects"
          }

          test "render is byte-stable for a fixed Model in both formats (US3, SC-002)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1

              Expect.equal (Loop.render m2 Loop.Text) (Loop.render m2 Loop.Text) "Text render is pure"
              Expect.equal (Loop.render m2 Loop.Json) (Loop.render m2 Loop.Json) "Json render is pure"
          } ]
