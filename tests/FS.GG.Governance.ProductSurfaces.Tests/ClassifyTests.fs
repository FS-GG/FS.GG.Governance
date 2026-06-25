module FS.GG.Governance.ProductSurfaces.Tests.ClassifyTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US1 — `classify` membership: a change under each declared surface kind yields exactly one
// ProductClassification with the expected Class + matched Capability; a routed path under no declared
// surface yields NO entry (FR-004). Exercised over the real `product-surface-all-kinds` catalog.

let private probe =
    [ "docs/guide.md"
      "skills/ship.md"
      "design/tokens.json"
      "samples/App/Program.fs"
      "release/notes.md"
      "src/Api.fsi"
      "src/Internal.fs"
      "misc/thing.txt"
      "random-unmatched.txt" ]

let private report = classifyPaths "product-surface-all-kinds" "standard" probe

let private classOf path =
    match forPath report path with
    | Some c -> c
    | None -> failtestf "expected a classification for '%s'" path

[<Tests>]
let tests =
    testList
        "ProductSurfaces.Classify.US1"
        [ testList
              "each declared kind routes + classifies (SC-001)"
              [ test "docs/** → DocsSurface · docs" {
                    let c = classOf "docs/guide.md"
                    Expect.equal c.Class DocsSurface "class"
                    Expect.equal c.Capability (DomainId "docs") "capability is the routed domain"
                    Expect.equal c.Surface (SurfaceId "guide-docs") "matched surface"
                }
                test "skills/** → SkillSurface · skills" {
                    let c = classOf "skills/ship.md"
                    Expect.equal c.Class SkillSurface "class"
                    Expect.equal c.Capability (DomainId "skills") "capability"
                    Expect.equal c.Surface (SurfaceId "ship-skill") "surface"
                }
                test "design/** → DesignSurface · design" {
                    let c = classOf "design/tokens.json"
                    Expect.equal c.Class DesignSurface "class"
                    Expect.equal c.Capability (DomainId "design") "capability"
                }
                test "samples/** → SampleAppSurface · samples" {
                    let c = classOf "samples/App/Program.fs"
                    Expect.equal c.Class SampleAppSurface "class"
                    Expect.equal c.Capability (DomainId "samples") "capability"
                }
                test "release/** → ReleaseSurface · release" {
                    let c = classOf "release/notes.md"
                    Expect.equal c.Class ReleaseSurface "class"
                    Expect.equal c.Capability (DomainId "release") "capability"
                    Expect.equal c.Surface (SurfaceId "rel") "release surface wins over the generated-product root"
                }
                test "src/**/*.fsi → PackageSurface · package-api" {
                    let c = classOf "src/Api.fsi"
                    Expect.equal c.Class PackageSurface "class"
                    Expect.equal c.Capability (DomainId "package-api") "capability"
                }
                test "generated-product root (src non-fsi) → GeneratedProductRoot" {
                    let c = classOf "src/Internal.fs"
                    Expect.equal c.Class GeneratedProductRoot "only the generatedProduct root covers a non-.fsi src path"
                    Expect.equal c.Surface (SurfaceId "product-root") "surface"
                } ]

          test "a routed path under NO declared surface yields no entry (FR-004, light-by-default)" {
              // misc/** routes to the declared `misc` domain but no surface covers it.
              Expect.isNone (forPath report "misc/thing.txt") "routed-but-unsurfaced ⇒ no classification"
          }

          test "an in-root path matching no glob and no surface yields no entry" {
              Expect.isNone (forPath report "random-unmatched.txt") "unrouted, unsurfaced ⇒ no classification"
          }

          test "every classification names only declared ids — no fabricated vocabulary" {
              for c in report.Classifications do
                  let (DomainId cap) = c.Capability
                  Expect.isNonEmpty cap "capability id present"
                  Expect.stringContains c.Explanation cap "explanation names the capability"
          } ]
