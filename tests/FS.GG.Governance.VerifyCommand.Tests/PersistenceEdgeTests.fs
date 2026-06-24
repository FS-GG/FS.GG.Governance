module FS.GG.Governance.VerifyCommand.Tests.PersistenceEdgeTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T029 (US3) — a failed/interrupted artifact write ⇒ `ToolError` (exit 4) and NO partial verify.json left
// behind (the writer is atomic temp+rename), DISTINCT from a Blocked verdict.

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]

[<Tests>]
let tests =
    testList
        "PersistenceEdge (US3)"
        [ test "a failing ArtifactWriter ⇒ ToolError (exit 4), no artifact recorded, distinct from Blocked" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Text Strict
              let ports = fakePortsFailingWrites validCatalog gitSrcChange cap (Set.ofList [ req.VerifyOut ])
              let model = Interpreter.run ports req

              Expect.equal model.Exit Loop.ToolError "write failure ⇒ ToolError"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit 4"
              Expect.notEqual model.Exit Loop.Blocked "ToolError is distinct from Blocked"
              Expect.isNone (writtenVerify cap) "no partial artifact recorded on a failed write"
          }

          test "a tool-error diagnostic is tagged and carries no fabricated passing verdict" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Text Standard
              let ports = fakePortsFailingWrites validCatalog gitSrcChange cap (Set.ofList [ req.VerifyOut ])
              let model = Interpreter.run ports req

              Expect.isNonEmpty model.Diagnostics "a diagnostic is recorded"
              Expect.isTrue (model.Diagnostics |> List.forall (fun d -> d.Category = Loop.ToolError)) "tagged ToolError" } ]
