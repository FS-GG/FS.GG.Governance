module FS.GG.Governance.RefreshCommand.Tests.ProductNeutralityTests

open Expecto
open FS.GG.Governance.RefreshJson
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// SC-006 / FR-011 — no product/view/path/generator/renderer identity is hardcoded: every value flows from
// `.fsgg/refresh.yml` through `Declaration.parse`/`Loop`/`RefreshJson` to the output verbatim. Two manifests
// carrying DIFFERENT invented ids/paths/kinds yield outputs reflecting the input, with no spec-example
// renderer string appearing unless the manifest supplied it.

let private manifest (id: string) (kind: string) (output: string) =
    "views:\n"
    + sprintf "  - id: %s\n    kind: %s\n    output: %s\n    generator: [\"my-gen\", \"--x\"]\n    generatorBasis: ver-7\n" id kind output

let private decisionFor (m: GenerationManifest) =
    { Outcome = NothingToRefresh
      DryRun = false
      Views = m.Entries |> List.map (fun e -> { Entry = e; Status = Current; Drifted = [] })
      RegeneratedCount = 0
      CurrentCount = m.Entries.Length
      UnresolvedCount = 0
      NotEvaluatedCount = 0 }

[<Tests>]
let tests =
    testList
        "ProductNeutrality"
        [ test "parsed entries reflect the invented ids/paths/kinds verbatim" {
              match Declaration.parse (ymlLines (manifest "alpha-view" "weird-kind" "some/where/x.txt")) with
              | Ok m ->
                  let e = m.Entries.Head
                  Expect.equal e.ViewId "alpha-view" "id verbatim"
                  Expect.equal e.Kind (Other "weird-kind") "unknown kind ⇒ Other verbatim"
                  Expect.equal e.OutputPath "some/where/x.txt" "path verbatim"
                  Expect.equal e.Generator [ "my-gen"; "--x" ] "generator verbatim"
                  Expect.equal e.GeneratorBasis "ver-7" "basis verbatim"
              | Error e -> failtestf "expected Ok, got %s" e.Reason
          }

          test "the projection contains exactly the supplied strings (two different manifests)" {
              let docA = RefreshJson.ofRefreshDecision (decisionFor (parseYml (manifest "view-one" "baseline" "a/one.txt")))
              let docB = RefreshJson.ofRefreshDecision (decisionFor (parseYml (manifest "view-two" "totally-custom" "b/two.txt")))

              Expect.stringContains docA "view-one" "A id"
              Expect.stringContains docA "a/one.txt" "A path"
              Expect.stringContains docB "view-two" "B id"
              Expect.stringContains docB "totally-custom" "B custom kind verbatim"
              Expect.stringContains docB "b/two.txt" "B path"

              // The output reflects the input only — A's strings never leak into B.
              Expect.isFalse (docB.Contains "view-one") "no cross-contamination of ids"
              Expect.isFalse (docB.Contains "a/one.txt") "no cross-contamination of paths"
          } ]
