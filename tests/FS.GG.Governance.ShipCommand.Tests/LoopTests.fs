module FS.GG.Governance.ShipCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Route
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

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

          test "Loaded Valid rolls up + projects, emits exactly one WriteArtifact = F025(F024 rollup) (US1, SC-001)" {
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
              let result = Route.select registry report findings
              let expectedDecision = Ship.rollup result req.Mode req.Profile
              let expectedDoc = AuditJson.ofShipDecision expectedDecision None

              Expect.equal m2.Decision (Some expectedDecision) "Decision = Ship.rollup of the same inputs/levers"
              Expect.equal m2.AuditDoc (Some expectedDoc) "AuditDoc = AuditJson.ofShipDecision of the decision"
              Expect.equal m2.Phase Loop.Rolled "advanced to Rolled"

              Expect.equal
                  e2
                  [ Loop.WriteArtifact(Loop.AuditArtifact, req.AuditOut, expectedDoc) ]
                  "exactly one WriteArtifact effect with the request path and projected audit doc"
          }

          test "Wrote Ok then Emitted reach Done; ExitCodeBasis Blocked ⇒ Blocked exit (US1 AS1)" {
              // src change under gate/standard ⇒ block-on-ship gates effective-Blocking ⇒ Fail/Blocked.
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
              Expect.equal (Option.get m2.Decision).Verdict Fail "src change under gate/standard fails"

              let m3, e3 = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m2
              Expect.equal m3.Phase Loop.Persisted "write ack ⇒ Persisted"
              Expect.equal e3 [ Loop.EmitSummary(Loop.render m2 req.Format) ] "write ack ⇒ EmitSummary"

              let m4, e4 = Loop.update Loop.Emitted m3
              Expect.equal m4.Phase Loop.Done "Emitted ⇒ Done"
              Expect.equal m4.Exit Loop.Blocked "Blocked basis ⇒ Blocked exit"
              Expect.equal e4 [] "terminal: no further effects"
          }

          test "ExitCodeBasis Clean ⇒ Success exit (US1 AS2)" {
              // a routine path selects no gates and yields only an advisory finding ⇒ Pass/Clean.
              let snap = snapshotOf (gitWithChanges [ 'M', "notes.txt" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
              Expect.equal (Option.get m2.Decision).Verdict Pass "routine-only change passes"

              let m3, _ = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m2
              let m4, _ = Loop.update Loop.Emitted m3
              Expect.equal m4.Exit Loop.Success "Clean basis ⇒ Success exit"
          }

          test "exitCode total mapping 0/1/2/3/4 (US1)" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "Success ⇒ 0"
              Expect.equal (Loop.exitCode Loop.Blocked) 1 "Blocked ⇒ 1"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "UsageError' ⇒ 2"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "InputUnavailable ⇒ 3"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "ToolError ⇒ 4"
          }

          test "render Text states verdict + basis, partitions with base/effective severity + findings + path (US1 AS3)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
              let text = Loop.render m2 Loop.Text

              Expect.stringContains text "verdict fail" "states the verdict"
              Expect.stringContains text "exit-code basis: blocked" "states the exit-code basis"
              Expect.stringContains text "blockers:" "lists the blockers section"
              Expect.stringContains text "base blocking" "shows base severity"
              Expect.stringContains text "effective blocking" "shows effective severity"
              Expect.stringContains text req.AuditOut "reports the written path"
              // Deterministic for a fixed Model.
              Expect.equal text (Loop.render m2 Loop.Text) "Text render is pure"
          }

          test "render Json equals the audit document text verbatim and is pure (US3)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Json
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
              let m2, _ = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1

              Expect.equal (Loop.render m2 Loop.Json) (Option.get m2.AuditDoc) "Json render = the F025 audit doc verbatim"
              Expect.equal (Loop.render m2 Loop.Json) (Loop.render m2 Loop.Json) "Json render is pure"
          } ]
