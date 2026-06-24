module FS.GG.Governance.VerifyCommand.Tests.FailureTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// T033 (Polish) — the five-way exit matrix (cli.md, SC-005): absent/invalid catalog ⇒ InputUnavailable (3);
// git-sensing unavailable ⇒ InputUnavailable (3); bad argv ⇒ UsageError' (2); unwritable --verify-out ⇒
// ToolError (4). Each distinct, no fabricated passing verdict, no partial artifact.

let private srcScope = Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]

[<Tests>]
let tests =
    testList
        "Failure matrix (Polish)"
        [ test "an invalid catalog ⇒ InputUnavailable (exit 3), no artifact, tagged diagnostic" {
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts invalidCatalog gitSrcChange cap) (requestFor srcScope Loop.Text)
              Expect.equal model.Exit Loop.InputUnavailable "invalid catalog ⇒ InputUnavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit 3"
              Expect.isNone (writtenVerify cap) "no artifact"
              Expect.isTrue (model.Diagnostics |> List.forall (fun d -> d.Category = Loop.InputUnavailable)) "tagged input-unavailable"
          }

          test "an absent catalog ⇒ InputUnavailable (exit 3)" {
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts Map.empty gitSrcChange cap) (requestFor srcScope Loop.Text)
              Expect.equal model.Exit Loop.InputUnavailable "absent catalog ⇒ InputUnavailable"
              Expect.isNone (writtenVerify cap) "no artifact"
          }

          test "git sensing unavailable ⇒ InputUnavailable (exit 3)" {
              let cap = newCapture ()
              // DefaultRange senses scope; a not-a-repo target fails sensing.
              let model = Interpreter.run (fakePorts validCatalog gitNotRepo cap) (requestFor Loop.DefaultRange Loop.Text)
              Expect.equal model.Exit Loop.InputUnavailable "git unavailable ⇒ InputUnavailable"
              Expect.isNone (writtenVerify cap) "no artifact"
          }

          test "bad argv ⇒ UsageError' (exit 2), decided before any port is built" {
              match Loop.parse [ "--nope" ] with
              | Error _ -> Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage ⇒ exit 2"
              | Ok _ -> failtest "expected a usage error"
          }

          test "an unwritable --verify-out ⇒ ToolError (exit 4), no partial artifact" {
              let cap = newCapture ()
              let req = requestFor srcScope Loop.Text
              let model = Interpreter.run (fakePortsFailingWrites validCatalog gitSrcChange cap (Set.ofList [ req.VerifyOut ])) req
              Expect.equal model.Exit Loop.ToolError "unwritable ⇒ ToolError"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit 4"
              Expect.isNone (writtenVerify cap) "no partial artifact"
          }

          test "the four failure exits are mutually distinct codes" {
              let codes =
                  [ Loop.Success; Loop.Blocked; Loop.UsageError'; Loop.InputUnavailable; Loop.ToolError ]
                  |> List.map Loop.exitCode

              Expect.equal codes [ 0; 1; 2; 3; 4 ] "five distinguishable exit codes" } ]
