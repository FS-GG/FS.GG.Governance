module FS.GG.Governance.ShipCommand.Tests.FailureTests

open Expecto
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// US4 + SC-004: each tool-failure category — not-a-repo / git-unavailable, missing-or-invalid catalog,
// unresolved --since rev, unrecognized lever, unwritable output after a valid rollup — yields a
// DISTINCT diagnostic and a category-mapped non-zero exit code, EACH distinct from the blocked verdict
// code 1, with NO partial artifact, and the interpreter NEVER throws.

let private firstMessage (model: Loop.Model) =
    model.Diagnostics |> List.map (fun d -> d.Message) |> String.concat " | "

[<Tests>]
let tests =
    testList
        "Failure"
        [ test "not-a-git-repo ⇒ InputUnavailable (exit 3), distinct diagnostic, no write (US4 AS1)" {
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts validCatalog gitNotRepo cap req) req

              Expect.equal model.Exit Loop.InputUnavailable "not-a-repo ⇒ InputUnavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit code 3"
              Expect.isEmpty cap.Writes "no artifact written on a sensing failure"
              Expect.stringContains (firstMessage model) "git sensing unavailable" "actionable git diagnostic"
          }

          test "git-unavailable ⇒ InputUnavailable (exit 3), no write (US4 AS1)" {
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts validCatalog gitUnavailable cap req) req
              Expect.equal model.Exit Loop.InputUnavailable "git unavailable ⇒ InputUnavailable"
              Expect.isEmpty cap.Writes "no artifact written"
          }

          test "missing required catalog ⇒ InputUnavailable (exit 3), distinct diagnostic, no write (US4 AS2)" {
              // ExplicitPaths bypasses git, so the ONLY failure surface is the catalog load.
              let req = requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts Map.empty gitEmpty cap req) req

              Expect.equal model.Exit Loop.InputUnavailable "missing catalog ⇒ InputUnavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit code 3"
              Expect.isEmpty cap.Writes "no artifact written on an invalid catalog"
              Expect.stringContains (firstMessage model) "catalog invalid" "catalog validation diagnostic"
          }

          test "invalid catalog content ⇒ InputUnavailable (exit 3), no write (US4 AS2)" {
              let req = requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts invalidCatalog gitEmpty cap req) req
              Expect.equal model.Exit Loop.InputUnavailable "invalid catalog ⇒ InputUnavailable"
              Expect.isEmpty cap.Writes "no artifact written"
          }

          test "unresolved --since revision ⇒ InputUnavailable (exit 3), distinct diagnostic, no write (US4 AS1)" {
              let req = requestFor (Loop.Since "nope") Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts validCatalog (gitUnknownRev "nope") cap req) req

              Expect.equal model.Exit Loop.InputUnavailable "unknown rev ⇒ InputUnavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit code 3"
              Expect.isEmpty cap.Writes "no artifact written"
              Expect.stringContains (firstMessage model) "git sensing unavailable" "ref-resolution diagnostic"
          }

          test "unrecognized --mode/--profile ⇒ UsageError (exit 2), no ports built, no artifact (US4 AS3)" {
              // Recognition happens in parse, BEFORE any port is built — so no artifact is reachable.
              match Loop.parse [ "ship"; "--mode"; "bogus" ] with
              | Error(Loop.UnrecognizedMode "bogus") -> ()
              | other -> failtestf "expected UnrecognizedMode, got %A" other

              match Loop.parse [ "ship"; "--profile"; "nope" ] with
              | Error(Loop.UnrecognizedProfile "nope") -> ()
              | other -> failtestf "expected UnrecognizedProfile, got %A" other

              Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage decision ⇒ exit 2"
          }

          test "unwritable output after a valid rollup ⇒ ToolError (exit 4), never Blocked, no partial artifact (US4 AS4)" {
              // A base-blocking change (would be Blocked) whose write FAILS must surface ToolError, not
              // a blocked verdict — the FR-009 distinction.
              let req = requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let cap = newCapture ()
              // F25 wiring (064): three artifacts are written; an unwritable output location fails all of them.
              let failing = Set.ofList [ req.AuditOut; req.CostBudgetOut; req.ProvenanceOut ]
              let model = Interpreter.run (fakePortsFailingWrites validCatalog gitEmpty cap failing req) req

              Expect.equal model.Exit Loop.ToolError "unwritable output ⇒ ToolError"
              Expect.notEqual model.Exit Loop.Blocked "a write failure is NEVER reported as a blocked verdict"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit code 4"
              Expect.isEmpty cap.Writes "the failing writer left no (partial) artifact"
              Expect.stringContains (firstMessage model) "failed to write" "write-failure diagnostic"
          }

          test "the four tool-failure codes are mutually distinct and none equals the blocked code 1 (FR-009, SC-004)" {
              let usage = Loop.exitCode Loop.UsageError'
              let input = Loop.exitCode Loop.InputUnavailable
              let tool = Loop.exitCode Loop.ToolError
              let blocked = Loop.exitCode Loop.Blocked

              let codes = [ usage; input; tool ]
              Expect.equal (List.distinct codes |> List.length) 3 "usage/input/tool codes are mutually distinct"
              for c in codes do
                  Expect.notEqual c blocked (sprintf "tool-failure code %d must differ from the blocked code %d" c blocked)
              Expect.equal blocked 1 "the blocked verdict code is 1"
          }

          test "the failure diagnostics are DISTINCT across categories (US4, SC-004)" {
              let diagOf files git scope failing =
                  let req = requestFor scope Loop.Text
                  let cap = newCapture ()
                  let model = Interpreter.run (fakePortsFailingWrites files git cap failing req) req
                  firstMessage model

              let notRepo = diagOf validCatalog gitNotRepo Loop.DefaultRange Set.empty
              let badCatalog = diagOf Map.empty gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Set.empty
              let unknownRev = diagOf validCatalog (gitUnknownRev "nope") (Loop.Since "nope") Set.empty

              let writeReq = requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let unwritable = diagOf validCatalog gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) (Set.ofList [ writeReq.AuditOut ])

              let distinct = [ notRepo; badCatalog; unknownRev; unwritable ] |> List.distinct
              Expect.equal distinct.Length 4 "all four diagnostics are distinct"
          }

          test "the interpreter NEVER throws for any failure input (FR-014, SC-004 totality)" {
              let runSafely files git scope =
                  let req = requestFor scope Loop.Text
                  Interpreter.run (fakePorts files git (newCapture ()) req) req |> ignore

              runSafely validCatalog gitNotRepo Loop.DefaultRange
              runSafely validCatalog gitUnavailable Loop.DefaultRange
              runSafely Map.empty gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ])
              runSafely invalidCatalog gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ])
              runSafely validCatalog (gitUnknownRev "nope") (Loop.Since "nope")
          } ]
