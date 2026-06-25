module FS.GG.Governance.RefreshCommand.Tests.FailureTests

open Expecto
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// Every failure class is distinguishable in diagnostic AND exit code; never a fabricated current; never a
// partial view (FR-010/FR-013/FR-016/SC-005).

let private hasDiag (m: Loop.Model) (substr: string) =
    m.Diagnostics |> List.exists (fun d -> d.Message.Contains substr)

[<Tests>]
let tests =
    testList
        "Failure"
        [ test "an absent manifest ⇒ InputUnavailable (exit 3), nothing regenerated" {
              withTempDir (fun repo ->
                  let m = runReal repo (requestFor repo)
                  Expect.equal m.Exit InputUnavailable "absent refresh.yml ⇒ exit 3"
                  Expect.isTrue (hasDiag m "refresh.yml") "diagnostic names the file")
          }

          test "a malformed manifest ⇒ InputUnavailable (exit 3)" {
              withTempRepo "views: [\n  - oops" (fun _ -> ()) (fun repo ->
                  let m = runReal repo (requestFor repo)
                  Expect.equal m.Exit InputUnavailable "malformed refresh.yml ⇒ exit 3")
          }

          test "a stale view whose source is absent ⇒ StaleUnresolved (exit 1), never fabricated current" {
              withTempRepo refreshYmlMissingSource (fun _ -> ()) (fun repo ->
                  let m = runReal repo (requestFor repo)
                  Expect.equal m.Exit StaleUnresolved' "unresolved ⇒ exit 1"
                  Expect.isFalse (fileExists repo "out.txt") "nothing fabricated"

                  match m.Decision |> Option.map (fun d -> (d.Views |> List.head).Status) with
                  | Some(StaleUnresolved reason) -> Expect.stringContains reason "absent.txt" "reason names the offending source"
                  | other -> failtestf "expected StaleUnresolved, got %A" other)
          }

          test "refresh still brings current what it can while another view is unresolved" {
              // One good view (a.txt present) + one whose source is absent.
              let yml =
                  "views:\n"
                  + "  - id: good\n    kind: baseline\n    output: a.out\n    sources:\n      - a.txt\n    generator: [\"cp\", \"a.txt\", \"a.out\"]\n    generatorBasis: g1\n"
                  + "  - id: bad\n    kind: baseline\n    output: b.out\n    sources:\n      - absent.txt\n    generator: [\"cp\", \"absent.txt\", \"b.out\"]\n    generatorBasis: g1\n"

              withTempRepo yml (fun d -> writeFile d "a.txt" "alpha\n") (fun repo ->
                  let m = runReal repo (requestFor repo)
                  Expect.equal m.Exit StaleUnresolved' "the run is blocked (exit 1) on the unresolved view"
                  Expect.equal (readFile repo "a.out") "alpha\n" "the resolvable view was still brought current")
          }

          test "a generator that fails ⇒ ToolError (exit 4), no partial view, distinct from 1/3" {
              withTempRepo refreshYmlOneView (fun d -> writeFile d "src.txt" "hello\n") (fun repo ->
                  let faulting =
                      { Interpreter.realPorts repo with Generate = fun _ -> Error "boom" }

                  let m = Interpreter.run faulting (requestFor repo)
                  Expect.equal m.Exit ToolError "generator failure ⇒ exit 4"
                  Expect.isFalse (fileExists repo "out.txt") "no partial view left behind")
          }

          test "bad argv ⇒ UsageError; mutually-exclusive selectors rejected" {
              Expect.isError (Loop.parse [ "--bogus" ]) "unknown flag"
              Expect.isError (Loop.parse [ "--view-kind"; "baseline"; "--view"; "doc" ]) "mutually exclusive selectors"
          } ]
