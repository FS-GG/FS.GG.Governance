module FS.GG.Governance.ProductSurfaces.Tests.StandaloneTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US3 — a generated product checked out standalone (no monorepo) routes + classifies using only its own
// declared sources (SC-004, FR-009). Loaded through the F014 per-directory Loader (reads only the `.fsgg`
// parent), routed, and classified — nothing outside the product root is consulted.

let private probe = [ "src/Api.fsi"; "docs/intro.md" ]
let private report = classifyPaths "generated-product-standalone" "standard" probe

[<Tests>]
let tests =
    testList
        "ProductSurfaces.Standalone.US3"
        [ test "the standalone product loads, routes, and classifies from its own sources" {
              let pkg = forPath report "src/Api.fsi"
              Expect.isSome pkg "package surface classifies standalone"
              Expect.equal pkg.Value.Class PackageSurface "package class"

              let docs = forPath report "docs/intro.md"
              Expect.isSome docs "docs surface classifies standalone"
              Expect.equal docs.Value.Class DocsSurface "docs class"
          }

          test "the standalone result depends on nothing outside the product root (deterministic re-run)" {
              let again = classifyPaths "generated-product-standalone" "standard" probe
              Expect.equal again report "identical from the product's own sources alone"
          } ]
