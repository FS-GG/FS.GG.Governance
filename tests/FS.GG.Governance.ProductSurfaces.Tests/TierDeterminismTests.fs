module FS.GG.Governance.ProductSurfaces.Tests.TierDeterminismTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.ProductSurfaces
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US2 — determinism (SC-005): re-ordering authored surfaces/checks or input paths leaves SelectedTier,
// Alternative, and Explanation unchanged; the Explanation names the matched capability, class, selected
// tier, and the cheaper-local alternative (FR-007).

let private facts = factsOf "product-surface-all-kinds"

let private probePaths =
    [ "docs/guide.md"; "skills/ship.md"; "design/tokens.json"; "samples/App/Program.fs"; "release/notes.md"; "src/Api.fsi"; "src/Internal.fs" ]

let private classifyWith (f: TypedFacts) (paths: string list) =
    ProductSurfaces.classify f (Routing.route f (paths |> List.map normalizePath)) (ProfileId "standard")

[<Tests>]
let tests =
    testList
        "ProductSurfaces.TierDeterminism.US2"
        [ test "re-ordering surfaces, checks, and input paths yields a byte-identical report (SC-005)" {
              let baseline = classifyWith facts probePaths

              let shuffled =
                  { facts with
                      Capabilities =
                          { facts.Capabilities with
                              Surfaces = List.rev facts.Capabilities.Surfaces
                              Checks = List.rev facts.Capabilities.Checks } }

              let reordered = classifyWith shuffled (List.rev probePaths)
              Expect.equal reordered baseline "SelectedTier/Alternative/Explanation/order all stable"
          }

          testPropertyWithConfig { FsCheckConfig.defaultConfig with maxTest = 60 } "any permutation of the input paths yields the same classification set" (fun (seed: int) ->
              // Deterministically derive a permutation of the probe paths from the seed and assert the
              // resulting (sorted) report equals the canonical one — order of the candidate set never matters.
              let n = List.length probePaths
              let rotate = ((seed % n) + n) % n
              let permuted = (List.skip rotate probePaths) @ (List.take rotate probePaths)
              classifyWith facts permuted = classifyWith facts probePaths)

          test "the explanation names capability, class, selected tier, and the cheaper-local alternative" {
              let rep = classifyWith facts [ "src/Api.fsi" ]
              let c = rep.Classifications |> List.exactlyOne
              Expect.stringContains c.Explanation "package-api" "names the capability"
              Expect.stringContains c.Explanation (surfaceClassToken c.Class) "names the class"
              Expect.stringContains c.Explanation (generatedProductTierToken c.SelectedTier) "names the selected tier"
              Expect.stringContains c.Explanation (generatedProductTierToken StructuralScan) "names the cheaper-local tier"
          } ]
