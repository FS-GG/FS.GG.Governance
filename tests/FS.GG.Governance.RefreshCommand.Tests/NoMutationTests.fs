module FS.GG.Governance.RefreshCommand.Tests.NoMutationTests

open Expecto
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// US2 no-mutation guard (SC-003/FR-013): a `--dry-run` over a stale repo leaves the working tree
// byte-for-byte identical — no view, no lock, no artifact. Plus a belt-and-braces guard with faulting write
// ports that would explode (⇒ exit 4) if the dry-run ever tried to write.

[<Tests>]
let tests =
    testList
        "NoMutation"
        [ test "--dry-run over a stale repo leaves the tree byte-for-byte identical" {
              withTempRepo refreshYmlOneView (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                  let before = snapshotTree repo
                  runReal repo { requestFor repo with DryRun = true } |> ignore
                  Expect.equal (snapshotTree repo) before "dry-run wrote nothing")
          }

          test "--dry-run over an all-current repo leaves the tree identical (exit 0)" {
              withTempRepo refreshYmlOneView (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                  runReal repo (requestFor repo) |> ignore // seed current
                  let before = snapshotTree repo
                  let m = runReal repo { requestFor repo with DryRun = true }
                  Expect.equal m.Exit NothingToRefresh "exit 0"
                  Expect.equal (snapshotTree repo) before "dry-run wrote nothing")
          }

          test "faulting write/generate ports are never reached in --dry-run (exit 5, not 4)" {
              withTempRepo refreshYmlOneView (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                  let exploding =
                      { Interpreter.realPorts repo with
                          Generate = fun _ -> failwith "Generate must not run in dry-run"
                          WriteProv = fun _ _ -> failwith "WriteProv must not run in dry-run"
                          Write = fun _ _ -> failwith "Write must not run in dry-run" }

                  let m = Interpreter.run exploding { requestFor repo with DryRun = true }
                  // If any write port had been reached the run would have become ToolError (exit 4).
                  Expect.equal m.Exit ViewsRegenerated "dry-run previewed without touching any write port")
          } ]
