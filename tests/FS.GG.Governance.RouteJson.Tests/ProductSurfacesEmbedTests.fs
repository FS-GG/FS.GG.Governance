module FS.GG.Governance.RouteJson.Tests.ProductSurfacesEmbedTests

open System.IO
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// F23 (US1, additive) — the productSurfaces section: emitted only when non-empty, sorted, with the
// documented per-entry fields; an EMPTY report is byte-identical to the F052-era `ofRouteResult` so all
// existing goldens and the wire contract are untouched. Inspects the EMITTED BYTES via JsonDocument.

let private emptyRoute: RouteResult =
    { SelectedGates = []
      Findings = { Findings = [] }
      Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

let private classification path capability surface cls tier declared alt reason : ProductClassification =
    { Path = GovernedPath path
      Capability = DomainId capability
      Surface = SurfaceId surface
      Class = cls
      SelectedTier = tier
      TierIsDeclared = declared
      Alternative = alt
      Reason = reason
      Explanation = sprintf "capability '%s' · classified" capability }

// Already in classify's deterministic order (by path then surface): "docs/guide.md" < "src/Api.fsi".
// RouteJson emits the report verbatim — it re-sorts nothing.
let private report =
    { Classifications =
        [ classification "docs/guide.md" "docs" "guide-docs" DocsSurface StructuralScan false NoCheaperLocalTier OnlySurface
          classification "src/Api.fsi" "package-api" "public-api" PackageSurface FocusedTests true (CheaperLocalTier StructuralScan) HighestPrecedenceKind ] }

[<Tests>]
let tests =
    testList
        "RouteJson.ProductSurfacesEmbed.F23"
        [ test "an EMPTY product-surface report ⇒ byte-identical to ofRouteResult (additive, non-breaking)" {
              let withEmpty = RouteJson.ofRouteResultWithProductSurfaces emptyRoute None [] { Classifications = [] }
              let legacy = RouteJson.ofRouteResult emptyRoute None []
              Expect.equal withEmpty legacy "empty report writes NO productSurfaces field — existing goldens untouched"
          }

          test "schemaVersion is unchanged at fsgg.route/v2" {
              use doc = parse (RouteJson.ofRouteResultWithProductSurfaces emptyRoute None [] report)
              Expect.equal (doc.RootElement.GetProperty("schemaVersion").GetString()) "fsgg.route/v2" "schemaVersion unchanged"
          }

          test "a non-empty report emits a productSurfaces array with the documented fields" {
              use doc = parse (RouteJson.ofRouteResultWithProductSurfaces emptyRoute None [] report)
              let arr = doc.RootElement.GetProperty "productSurfaces"
              Expect.equal (arr.GetArrayLength()) 2 "two entries"
              let e = arr.[0]
              Expect.equal (e.GetProperty("path").GetString()) "docs/guide.md" "sorted by path: docs first"
              Expect.equal (e.GetProperty("capability").GetString()) "docs" "capability"
              Expect.equal (e.GetProperty("surface").GetString()) "guide-docs" "surface"
              Expect.equal (e.GetProperty("class").GetString()) "docs" "class token"
              Expect.equal (e.GetProperty("tier").GetString()) "structuralScan" "tier token"
              Expect.isFalse (e.GetProperty("tierDeclared").GetBoolean()) "tierDeclared false (F24-pending)"
              Expect.equal (e.GetProperty("alternative").GetString()) "none" "no cheaper-local ⇒ 'none'"

              let pkg = arr.[1]
              Expect.equal (pkg.GetProperty("class").GetString()) "package" "package class"
              Expect.equal (pkg.GetProperty("tier").GetString()) "focusedTests" "tier"
              Expect.equal (pkg.GetProperty("alternative").GetString()) "structuralScan" "cheaper-local tier token"
          }

          test "deterministic: identical input ⇒ byte-identical output" {
              let a = RouteJson.ofRouteResultWithProductSurfaces emptyRoute None [] report
              let b = RouteJson.ofRouteResultWithProductSurfaces emptyRoute None [] report
              Expect.equal a b "byte-stable"
          }

          test "the productSurfaces golden is byte-stable (T041)" {
              let goldenDir = Path.Combine(repoRoot, "tests", "FS.GG.Governance.RouteJson.Tests", "golden")
              let goldenPath = Path.Combine(goldenDir, "route-product-surfaces.json")
              let actual = RouteJson.ofRouteResultWithProductSurfaces emptyRoute None [] report

              if System.Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  Directory.CreateDirectory goldenDir |> ignore
                  File.WriteAllText(goldenPath, actual)

              let golden = File.ReadAllText goldenPath
              Expect.equal actual golden "route.json with productSurfaces equals the committed golden"
          } ]
