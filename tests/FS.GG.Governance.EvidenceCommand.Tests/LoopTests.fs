module FS.GG.Governance.EvidenceCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceCommand
open FS.GG.Governance.EvidenceCommand.Tests.Support

// US1/US3 — the pure transition: `update` requests I/O via `Effect` data and performs none itself; the
// report → document mapping runs `Kernel.Evidence.build`/`effective` and recovers a graph failure by name.

let private modelFor (out: string) =
    Loop.init (requestWith out Loop.Json) |> fst

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "init requests the sense effect and nothing else" {
              let _model, effects = Loop.init (requestWith "o.json" Loop.Json)
              Expect.equal effects [ Loop.SenseReport "." ] "init emits a single SenseReport"
          }

          test "Reported(Ok report) emits the write effect carrying the projected document; no I/O in update" {
              let r = report [ reportNode "a" Real Real (Some Freshness.Fresh) "speckit" ] []
              let model, effects = Loop.update (Loop.Reported(Ok r)) (modelFor "out.json")

              let expectedDoc = EvidenceJson.ofReport (Loop.toDocument r)
              Expect.equal effects [ Loop.WriteArtifact("out.json", expectedDoc) ] "single write effect with the projected bytes"
              Expect.equal model.Phase Loop.Projected "phase advanced to Projected"
              Expect.equal model.Doc (Some expectedDoc) "document rendered and held in the model"
          }

          test "toDocument runs the taint closure: a Real node resting on Synthetic becomes effective AutoSynthetic (SC-004)" {
              // a depends on b; b is Synthetic ⇒ a's effective state is AutoSynthetic while declared stays Real.
              let r =
                  report
                      [ reportNode "a" Real Real (Some Freshness.Fresh) "speckit"
                        reportNode "b" Synthetic Synthetic None "speckit" ]
                      [ "a", "b" ]

              match (Loop.toDocument r).Content with
              | WellFormed(nodes, _) ->
                  let a = nodes |> List.find (fun n -> n.Id = "a")
                  Expect.equal a.Declared Real "declared kept Real"
                  Expect.equal a.Effective AutoSynthetic "effective demoted by the closure"
              | Malformed e -> failtestf "expected WellFormed, got %A" e
          }

          test "toDocument recovers a graph failure by name instead of a partial map (FR-004, D3)" {
              // A self-cycle is rejected by Evidence.build; the bridge would swallow it — toDocument surfaces it.
              let r = report [ reportNode "a" Real Real None "speckit" ] [ "a", "a" ]

              match (Loop.toDocument r).Content with
              | Malformed(Cycle _) -> ()
              | other -> failtestf "expected Malformed (Cycle _), got %A" other
          }

          test "a bare Stale with no resolved cause maps to Unknown — never a guessed cause (INV-6/D4)" {
              let r = report [ reportNode "a" Real Real (Some Freshness.Stale) "speckit" ] []

              match (Loop.toDocument r).Content with
              | WellFormed(nodes, _) ->
                  let a = nodes |> List.find (fun n -> n.Id = "a")
                  Expect.equal a.Freshness NodeFreshness.Unknown "bare Stale ⇒ Unknown, no fabricated cause"
              | Malformed e -> failtestf "expected WellFormed, got %A" e
          }

          test "Wrote(Ok ()) emits the summary; Emitted finishes Done/Success" {
              let r = report [ reportNode "a" Real Real (Some Freshness.Fresh) "speckit" ] []
              let m1, _ = Loop.update (Loop.Reported(Ok r)) (modelFor "out.json")
              let m2, effects = Loop.update (Loop.Wrote(Ok())) m1

              match effects with
              | [ Loop.EmitSummary _ ] -> Expect.equal m2.Phase Loop.Persisted "persisted"
              | other -> failtestf "expected a single EmitSummary, got %A" other

              let m3, fin = Loop.update Loop.Emitted m2
              Expect.equal m3.Phase Loop.Done "done"
              Expect.equal m3.Exit Loop.Success "success"
              Expect.isEmpty fin "no further effects"
          } ]
