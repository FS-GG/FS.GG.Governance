module FS.GG.Governance.DependencyFences.Tests.BoundaryClassificationTests

open System
open System.IO
open Expecto
open FS.GG.Governance.DependencyFences.Tests.ProjectGraph

let private repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

[<Tests>]
let tests =
    testList
        "dependency-fences · boundary-classification"
        [ test "every source assembly boundary has a reviewed classification" {
              let unclassified =
                  load ()
                  |> sourceNodes
                  |> List.filter (classifyBoundary >> Option.isNone)
                  |> List.map (fun n -> n.Name)

              Expect.isEmpty
                  unclassified
                  (sprintf
                      "every source assembly must be classified as security/purity, packaging, or organizational; review: %s"
                      (String.concat ", " unclassified))
          }

          test "the duplicate built-in adapter seam stays consolidated" {
              let nodes = load () |> sourceNodes
              let names = nodes |> List.map (fun n -> n.Name) |> Set.ofList

              Expect.contains
                  names
                  "FS.GG.Governance.Adapters.BuiltIn"
                  "one organizational assembly carries both preserved adapter namespaces"

              Expect.isFalse
                  (names.Contains "FS.GG.Governance.Adapters.SpecKit")
                  "SpecKit is no longer a separate restore/reference/release unit"

              Expect.isFalse
                  (names.Contains "FS.GG.Governance.Adapters.DesignSystem")
                  "DesignSystem is no longer a separate restore/reference/release unit"

              let builtIn = nodes |> List.find (fun n -> n.Name = "FS.GG.Governance.Adapters.BuiltIn")
              Expect.equal
                  (classifyBoundary builtIn)
                  (Some BoundaryKind.Organizational)
                  "the merged boundary is explicitly classified as organizational"
          }

          test "the generated classified graph equals the committed artifact" {
              let artifactPath =
                  Path.Combine(
                      repoRoot,
                      "tests",
                      "FS.GG.Governance.DependencyFences.Tests",
                      "dependency-graph.dot"
                  )

              let actual = renderClassifiedDot (load ())

              if Environment.GetEnvironmentVariable "BLESS_DEPENDENCY_GRAPH" = "1" then
                  File.WriteAllText(artifactPath, actual)

              let expected = File.ReadAllText artifactPath
              Expect.equal actual expected "regenerate the classified graph intentionally with BLESS_DEPENDENCY_GRAPH=1"
          } ]
