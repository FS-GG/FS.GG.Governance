module FS.GG.Governance.RouteCommand.Tests.FailureTests

open Expecto
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// US4 + SC-004: each failure category — not-a-repo / git-unavailable, missing-or-invalid catalog,
// unresolved --since rev, unwritable output after a valid route — yields a DISTINCT diagnostic and a
// category-mapped non-zero exit code, with NO partial artifact, and the interpreter NEVER throws.

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

          test "unresolved --since revision ⇒ InputUnavailable (exit 3), distinct diagnostic, no write (US4 AS3)" {
              let req = requestFor (Loop.Since "nope") Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts validCatalog (gitUnknownRev "nope") cap req) req

              Expect.equal model.Exit Loop.InputUnavailable "unknown rev ⇒ InputUnavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit code 3"
              Expect.isEmpty cap.Writes "no artifact written"
              Expect.stringContains (firstMessage model) "git sensing unavailable" "ref-resolution diagnostic"
          }

          test "unwritable output after a valid route ⇒ ToolError (exit 4), no partial artifact (US4 AS4)" {
              let req = requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let cap = newCapture ()
              let failing = Set.ofList [ req.GatesOut; req.RouteOut ]
              let model = Interpreter.run (fakePortsFailingWrites validCatalog gitEmpty cap failing req) req

              Expect.equal model.Exit Loop.ToolError "unwritable output ⇒ ToolError"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit code 4"
              Expect.isEmpty cap.Writes "the failing writer left no (partial) artifact"
              Expect.stringContains (firstMessage model) "failed to write" "write-failure diagnostic"
          }

          test "the four failure categories produce DISTINCT diagnostics (US4, SC-004)" {
              let diagOf files git scope failing =
                  let req = requestFor scope Loop.Text
                  let cap = newCapture ()
                  let model = Interpreter.run (fakePortsFailingWrites files git cap failing req) req
                  firstMessage model

              let notRepo = diagOf validCatalog gitNotRepo Loop.DefaultRange Set.empty
              let badCatalog = diagOf Map.empty gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Set.empty
              let unknownRev = diagOf validCatalog (gitUnknownRev "nope") (Loop.Since "nope") Set.empty

              let writeReq = requestFor (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) Loop.Text
              let unwritable = diagOf validCatalog gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ]) (Set.ofList [ writeReq.GatesOut; writeReq.RouteOut ])

              let distinct = [ notRepo; badCatalog; unknownRev; unwritable ] |> List.distinct
              Expect.equal distinct.Length 4 "all four diagnostics are distinct"
          }

          // ── usage errors → exit 2, no artifact (the Program/parse boundary) ──

          test "every usage error maps to exit 2 (US4, no artifact reachable before ports are built)" {
              let usages =
                  [ Loop.parse [ "route"; "--paths"; "a"; "--since"; "X" ]
                    Loop.parse [ "route"; "--paths" ]
                    Loop.parse [ "route"; "--bogus" ]
                    Loop.parse [ "route"; "--repo" ] ]

              for r in usages do
                  match r with
                  | Error _ -> ()
                  | Ok req -> failtestf "expected a usage Error, got Ok %A" req

              Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage decision ⇒ exit 2"
          }

          test "the interpreter NEVER throws for any failure input (FR-013, SC-004 totality)" {
              let runSafely files git scope =
                  let req = requestFor scope Loop.Text
                  Interpreter.run (fakePorts files git (newCapture ()) req) req |> ignore

              // None of these may raise — every failure is a Msg/Diagnostic/ExitDecision, not an exception.
              runSafely validCatalog gitNotRepo Loop.DefaultRange
              runSafely validCatalog gitUnavailable Loop.DefaultRange
              runSafely Map.empty gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ])
              runSafely invalidCatalog gitEmpty (Loop.ExplicitPaths [ gp "src/Lib/Thing.fs" ])
              runSafely validCatalog (gitUnknownRev "nope") (Loop.Since "nope")
          } ]
