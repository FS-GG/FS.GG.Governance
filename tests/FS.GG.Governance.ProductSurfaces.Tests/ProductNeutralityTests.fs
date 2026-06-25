module FS.GG.Governance.ProductSurfaces.Tests.ProductNeutralityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// SC-007 product-neutrality guard: no product/surface/path/template-profile/generator identity is
// hardcoded in ProductSurfaces. Drive `classify` with TWO fixtures carrying different invented surface
// ids/paths and assert each report reflects the input verbatim — no string from one fixture leaks into
// the other's report unless that fixture supplied it.

let private allKinds =
    classifyPaths "product-surface-all-kinds" "standard"
        [ "docs/guide.md"; "skills/ship.md"; "design/tokens.json"; "samples/App/Program.fs"; "release/notes.md"; "src/Api.fsi"; "src/Internal.fs" ]

let private standalone =
    classifyPaths "generated-product-standalone" "standard" [ "src/Api.fsi"; "src/Internal.fs"; "docs/intro.md" ]

let private surfaceIds (rep: ProductSurfaceReport) =
    rep.Classifications |> List.map (fun c -> let (SurfaceId s) = c.Surface in s) |> Set.ofList

[<Tests>]
let tests =
    testList
        "ProductSurfaces.Neutrality.Polish"
        [ test "each report's surface ids are exactly those the fixture declared (no fabrication)" {
              let declaredOf name =
                  (factsOf name).Capabilities.Surfaces |> List.map (fun s -> let (SurfaceId i) = s.Id in i) |> Set.ofList

              Expect.isTrue (Set.isSubset (surfaceIds allKinds) (declaredOf "product-surface-all-kinds")) "all-kinds surfaces are declared"
              Expect.isTrue (Set.isSubset (surfaceIds standalone) (declaredOf "generated-product-standalone")) "standalone surfaces are declared"
          }

          test "no surface id unique to one fixture appears in the other's report (no leaked identity)" {
              // 'extra-api'/'tokens'/'sample' exist only in all-kinds; 'guide-docs'/'product-root'/'public-api'
              // exist in both, so compare a uniquely-all-kinds id against the standalone report.
              let standaloneIds = surfaceIds standalone
              for uniqueToAllKinds in [ "extra-api"; "tokens"; "sample"; "ship-skill" ] do
                  Expect.isFalse (standaloneIds.Contains uniqueToAllKinds) (sprintf "'%s' must not leak into the standalone report" uniqueToAllKinds)
          }

          test "the report carries no template-profile/baseline/generator string from the catalog" {
              // classify never echoes a TemplateProfile/Baseline/evidenceTag into its output — those stay in
              // the catalog; the report names only path/capability/surface/class/tier ids.
              let rendered = sprintf "%A" allKinds.Classifications
              for catalogOnly in [ "fsharp-lib"; "public-api.baseline.txt"; "api-surface"; "design-tokens" ] do
                  Expect.isFalse (rendered.Contains catalogOnly) (sprintf "catalog-only token '%s' must not appear in the classification" catalogOnly)
          } ]
