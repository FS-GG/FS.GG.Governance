module FS.GG.Governance.ShipCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// US2 (the pure composition) + the F046 cache wiring: drive `Loop.update` with literal Msg values over
// real upstream-assembled inputs (a real RepoSnapshot from the F016 core, real TypedFacts from the F014
// core, real SensedFacts from the shared FreshnessSensing edge over the faked sensor). No I/O, no git, no
// clock (Principle IV).

// Drive init → Sensed → Loaded to the Selected phase, returning the snapshot + the post-select model + effects.
let private toSelected (git) (req: Loop.RunRequest) =
    let snap = snapshotOf git defaultOpts
    let m0, _ = Loop.init req
    let m1, _ = Loop.update (Loop.Sensed(Ok snap)) m0
    let m2, e2 = Loop.update (Loop.Loaded(Valid(factsOf validCatalog))) m1
    snap, m2, e2

// Feed the two cache senses (fake sensor + empty store), run the F052 execute step, and return the post-
// GatesExecuted model (Rolled phase, AuditDoc projected). The default fail exec port leaves the verdict as
// `Ship.rollup` decided (failing gates are not relocated).
let private toJoined (snap) (m2: Loop.Model) =
    let baseHead = baseHeadOfSnap (Some snap)
    let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e
    let m3, _ = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
    let m4raw, e4raw = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3
    runExecuteEffect fakeExecPort m4raw e4raw

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "Sensed Ok sets Candidates + Snapshot and emits LoadCatalog (US2)" {
              let snap = snapshotOf (gitWithChanges [ 'M', "src/Lib/Thing.fs" ]) defaultOpts
              let req = requestFor Loop.DefaultRange Loop.Text
              let m0, _ = Loop.init req
              let m1, e1 = Loop.update (Loop.Sensed(Ok snap)) m0

              Expect.equal m1.Candidates (Some(snap.Changed |> List.map (fun c -> c.Path))) "candidates = snapshot changed paths"
              Expect.equal m1.Snapshot (Some snap) "snapshot kept to derive base/head"
              Expect.equal m1.Phase Loop.Sensed' "advanced to Sensed'"
              Expect.equal e1 [ Loop.LoadCatalog req.Repo ] "emits LoadCatalog for the repo"
          }

          test "Loaded Valid rolls up the verdict and emits SenseFreshness + LoadStore (no write yet) (US2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2, e2 = toSelected git req

              let result =
                  let facts = factsOf validCatalog
                  let report = Routing.route facts (candidatesOf git defaultOpts)
                  let registry = Gates.buildRegistry facts
                  let findings = Findings.findUnknownGovernedPaths facts report
                  Route.select registry report findings

              let expectedDecision = Ship.rollup result req.Mode req.Profile
              Expect.equal m2.Decision (Some expectedDecision) "Decision = Ship.rollup of the same inputs/levers (verdict decided here)"
              Expect.equal m2.Phase Loop.Selected "advanced to Selected (not Rolled — the join waits)"

              let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
              Expect.equal
                  e2
                  [ Loop.SenseFreshness(selectedGates, baseHeadOfSnap (Some snap))
                    Loop.LoadStore req.StorePath ]
                  "emits SenseFreshness + LoadStore — and NO write"
          }

          test "the join fires once both senses arrive ⇒ auditDoc = ofShipDecision decision (Some report), one write (SC-002)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2, _ = toSelected git req
              let baseHead = baseHeadOfSnap (Some snap)
              let sensed = match FreshnessSensing.senseFreshness fakeSensor m2.SelectedGates baseHead with Ok s -> s | Error e -> failtestf "%s" e

              // FreshnessSensed first ⇒ the join waits (Store None) — no write.
              let m3, e3 = Loop.update (Loop.FreshnessSensed(Ok sensed)) m2
              Expect.equal e3 [] "join waits — only one input present"

              // StoreLoaded ⇒ F052 requests ExecuteGates (NOT the write yet).
              let m4raw, e4raw = Loop.update (Loop.StoreLoaded(Ok EvidenceReuse.empty)) m3

              match e4raw with
              | [ Loop.ExecuteGates _ ] -> ()
              | other -> failtestf "expected a single ExecuteGates effect, got %A" other

              // GatesExecuted ⇒ project the (relocated) audit doc + one write.
              let m4, e4 = runExecuteEffect fakeExecPort m4raw e4raw
              Expect.equal m4.Phase Loop.Rolled "GatesExecuted ⇒ Rolled"

              let decision = Option.get m2.Decision
              let expectedReport = expectedCacheReport m2.SelectedGates baseHead
              let outcomes = expectedOutcomes validCatalog m2.SelectedGates
              // Default fail exec ⇒ no passing gate ⇒ the relocation is identity ⇒ the decision is unchanged.
              let auditDoc = AuditJson.ofShipDecision decision (Some expectedReport) outcomes
              Expect.equal m4.AuditDoc (Some auditDoc) "AuditDoc = ofShipDecision decision (Some report) outcomes"
              Expect.equal e4 [ Loop.WriteArtifact(Loop.AuditArtifact, req.AuditOut, auditDoc) ] "exactly one WriteArtifact (cache+execution-bearing audit doc)"

              // SC-002: each kind:"gate" item carries a GateId-matched verdict.
              Expect.stringContains auditDoc "\"cacheEligibilityEvaluated\":true" "the cache section is evaluated"
              for g in m2.SelectedGates do
                  Expect.stringContains auditDoc (gateIdValue g.Id) "each selected gate id appears in audit.json"
          }

          test "Wrote Ok then Emitted reach Done; ExitCodeBasis Blocked ⇒ Blocked exit, UNCHANGED by cache (US2 AS1, SC-003)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2, _ = toSelected git req
              Expect.equal (Option.get m2.Decision).Verdict Fail "src change under gate/standard fails"
              let m4, _ = toJoined snap m2

              let m5, e5 = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m4
              Expect.equal m5.Phase Loop.Persisted "write ack ⇒ Persisted"

              // F27 wiring (063): EmitSummary now also carries the human payload + --plain flag (the mode is
              // decided at the edge). The carried text is still the rendered summary.
              match e5 with
              | [ Loop.EmitSummary(text, _, _) ] -> Expect.equal text (Loop.render m4 req.Format) "write ack ⇒ EmitSummary with the rendered text"
              | other -> failtestf "expected a single EmitSummary, got %A" other

              let m6, e6 = Loop.update Loop.Emitted m5
              Expect.equal m6.Phase Loop.Done "Emitted ⇒ Done"
              Expect.equal m6.Exit Loop.Blocked "Blocked basis ⇒ Blocked exit (cache never participates)"
              Expect.equal e6 [] "terminal: no further effects"
          }

          test "ExitCodeBasis Clean ⇒ Success exit; finding-only audit carries no cache verdict on any item (US2 AS2, E1)" {
              // A routine path selects no gates ⇒ only an advisory finding ⇒ Pass/Clean.
              let git = gitWithChanges [ 'M', "notes.txt" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2, _ = toSelected git req
              Expect.equal (Option.get m2.Decision).Verdict Pass "routine-only change passes"
              Expect.equal m2.SelectedGates [] "no gates selected"
              let m4, _ = toJoined snap m2

              let auditDoc = Option.get m4.AuditDoc
              Expect.stringContains auditDoc "\"cacheEligibilityEvaluated\":true" "evaluated even with no gate items"

              let m5, _ = Loop.update (Loop.Wrote(Loop.AuditArtifact, Ok())) m4
              let m6, _ = Loop.update Loop.Emitted m5
              Expect.equal m6.Exit Loop.Success "Clean basis ⇒ Success exit"
          }

          test "exitCode total mapping 0/1/2/3/4 (US2)" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "Success ⇒ 0"
              Expect.equal (Loop.exitCode Loop.Blocked) 1 "Blocked ⇒ 1"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "UsageError' ⇒ 2"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "InputUnavailable ⇒ 3"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "ToolError ⇒ 4"
          }

          test "render Text states verdict + basis + the cache summary (US2 AS3, FR-015 C2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let snap, m2, _ = toSelected git req
              let m4, _ = toJoined snap m2
              let text = Loop.render m4 Loop.Text

              // F27 wiring (063): the text is now the shared HumanText.ofShipDecision projection (NON-contractual
              // wording): Title "verdict: FAIL", exit status line, "Blockers" group, "Cache eligibility" section.
              Expect.stringContains text "verdict: FAIL" "states the verdict"
              Expect.stringContains text "exit status: blocked" "states the exit-code basis"
              Expect.stringContains text "Blockers" "lists the blockers section"
              Expect.stringContains text "Cache eligibility" "lists the cache outcome"
              for g in m2.SelectedGates do
                  Expect.stringContains text (gateIdValue g.Id) "summary names each selected gate's cache outcome"
              Expect.equal text (Loop.render m4 Loop.Text) "Text render is pure"
          }

          test "render Json equals the (cache-bearing) audit document verbatim and is pure (US2)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Json
              let snap, m2, _ = toSelected git req
              let m4, _ = toJoined snap m2

              Expect.equal (Loop.render m4 Loop.Json) (Option.get m4.AuditDoc) "Json render = the F025 audit doc verbatim"
              Expect.stringContains (Loop.render m4 Loop.Json) "\"cacheEligibilityEvaluated\":true" "the persisted doc carries the evaluated cache section"
              Expect.equal (Loop.render m4 Loop.Json) (Loop.render m4 Loop.Json) "Json render is pure"
          } ]
