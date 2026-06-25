module FS.GG.Governance.ProductSurfaces.Tests.PrecedenceTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.ProductSurfaces
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US1 — multi-match precedence (FR-008, D6): a path under more than one surface wins by the documented
// SurfaceClass total order; co-kind ties break by ordinal-first SurfaceId; a single cover is OnlySurface;
// re-ordering the authored surfaces does not change the winner (SC-005). Tier value is asserted in US2 —
// here only Class/Surface/Reason.

let private facts = factsOf "product-surface-all-kinds"
let private report = classifyPaths "product-surface-all-kinds" "standard" [ "release/notes.md"; "src/Api.fsi"; "docs/guide.md" ]

let private classOf path =
    match forPath report path with
    | Some c -> c
    | None -> failtestf "expected a classification for '%s'" path

[<Tests>]
let tests =
    testList
        "ProductSurfaces.Precedence.US1"
        [ test "cross-kind win: release/** under {release, generatedProduct} → ReleaseSurface (HighestPrecedenceKind)" {
              // product-root declares paths ["src", "release"], so release/notes.md falls under BOTH the
              // release surface `rel` and the generatedProduct root. Release outranks generatedProduct.
              let c = classOf "release/notes.md"
              Expect.equal c.Class ReleaseSurface "release outranks the generated-product root"
              Expect.equal c.Surface (SurfaceId "rel") "winning surface"
              Expect.equal c.Reason HighestPrecedenceKind "won on the SurfaceClass total order"
          }

          test "co-kind tie: src/**/*.fsi under two package surfaces → ordinal-first SurfaceId (OrdinalSurfaceTiebreak)" {
              // extra-api and public-api both declare src/**/*.fsi; both are PackageSurface. The ordinal-first
              // SurfaceId ('extra-api' < 'public-api') wins.
              let c = classOf "src/Api.fsi"
              Expect.equal c.Class PackageSurface "package is the highest-precedence covering kind"
              Expect.equal c.Surface (SurfaceId "extra-api") "ordinal-first SurfaceId wins the co-kind tie"
              Expect.equal c.Reason OrdinalSurfaceTiebreak "reason"
          }

          test "single cover → OnlySurface" {
              let c = classOf "docs/guide.md"
              Expect.equal c.Reason OnlySurface "exactly one surface covered the path"
          }

          test "re-ordering the authored surfaces does not change any winner (SC-005)" {
              // classify is a pure function of the (order-normalized) facts. Reverse the surface list and
              // re-classify the same paths through real routing: every winner/reason is unchanged.
              let reversed =
                  { facts with
                      Capabilities = { facts.Capabilities with Surfaces = List.rev facts.Capabilities.Surfaces } }

              let paths = [ "release/notes.md"; "src/Api.fsi"; "src/Internal.fs"; "docs/guide.md" ]
              let baseRep = ProductSurfaces.classify facts (Routing.route facts (paths |> List.map normalizePath)) (ProfileId "standard")
              let revRep = ProductSurfaces.classify reversed (Routing.route reversed (paths |> List.map normalizePath)) (ProfileId "standard")
              Expect.equal revRep baseRep "winner/reason/tier are independent of authored surface order"
          } ]
