module FS.GG.Governance.EvidenceCommand.Tests.InterpreterTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceCommand
open FS.GG.Governance.EvidenceCommand.Tests.Support

// US1 — the edge interpreter executes each effect against injected ports and threads results back, with no real
// git/filesystem reached (the SenseReport/Write/Out are captured fakes).

[<Tests>]
let tests =
    testList
        "Interpreter"
        [ test "run over a faked well-formed report writes the versioned document and exits Success" {
              let r = report [ reportNode "a" Real Real (Some Fresh) "speckit" ] []
              let ports, cap = fakePorts (Ok r)
              let model = Interpreter.run ports (requestWith "out.json" Loop.Json)

              Expect.equal model.Exit Loop.Success "operational success"
              Expect.equal (List.length cap.Writes) 1 "exactly one artifact written"

              let path, content = List.head cap.Writes
              Expect.equal path "out.json" "written to the requested out path"

              let root = JsonDocument.Parse(content).RootElement
              Expect.equal (root.GetProperty("schemaVersion").GetString()) "fsgg.evidence/v1" "versioned document"
          }

          test "step SenseReport reifies the port result as Reported" {
              let r = report [] []
              let ports, _ = fakePorts (Ok r)

              match Interpreter.step ports (Loop.SenseReport ".") with
              | Loop.Reported(Ok _) -> ()
              | other -> failtestf "expected Reported (Ok _), got %A" other
          }

          test "step EmitSummary writes to the Out sink and returns Emitted" {
              let ports, cap = fakePorts (Ok(report [] []))

              match Interpreter.step ports (Loop.EmitSummary "hello") with
              | Loop.Emitted -> Expect.equal cap.Out [ "hello" ] "the line was emitted"
              | other -> failtestf "expected Emitted, got %A" other
          }

          test "step WriteArtifact reifies a port failure as Wrote(Error _) (never throws)" {
              let ports, _ = fakePortsFailingWrite (Ok(report [] []))

              match Interpreter.step ports (Loop.WriteArtifact("p", "c")) with
              | Loop.Wrote(Error _) -> ()
              | other -> failtestf "expected Wrote (Error _), got %A" other
          } ]

// F13 (#49): once the pipeline has decided (Phase = Done), every further reified Msg must be inert — matching
// the guard Route/Ship/Verify document. Before the fix, a post-Done `Wrote(Ok())` re-mutated Phase to
// Persisted and re-scheduled an EmitSummary effect.
[<Tests>]
let doneInertness =
    testList
        "DoneInertness-F13"
        [ test "update is inert once Phase = Done — no mutation, no effects" {
              let req = requestWith "out.json" Loop.Json
              let model0, _ = Loop.init req
              let doneModel = { model0 with Phase = Loop.Done; Exit = Loop.Success }

              Expect.equal (Loop.update (Loop.Wrote(Ok())) doneModel) (doneModel, []) "Wrote(Ok) after Done is inert"
              Expect.equal (Loop.update (Loop.Reported(Ok(report [] []))) doneModel) (doneModel, []) "Reported after Done is inert"
          } ]
