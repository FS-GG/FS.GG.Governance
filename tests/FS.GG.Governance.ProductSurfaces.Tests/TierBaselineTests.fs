module FS.GG.Governance.ProductSurfaces.Tests.TierBaselineTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US2 — the baseline tier per winning kind (contracts/classification.md §4 table). A cheap change never
// pulls in a deeper tier without a positive match (SC-003). `standard` is a non-escalating profile, so the
// selected tier reflects the baseline (snapped to declared tiers where present).

let private report =
    classifyPaths "product-surface-all-kinds" "standard"
        [ "docs/guide.md"; "samples/App/Program.fs"; "skills/ship.md"; "design/tokens.json"; "src/Internal.fs"; "src/Api.fsi"; "release/notes.md" ]

let private tierOf path =
    match forPath report path with
    | Some c -> c.SelectedTier
    | None -> failtestf "expected a classification for '%s'" path

[<Tests>]
let tests =
    testList
        "ProductSurfaces.TierBaseline.US2"
        [ test "docs → StructuralScan" { Expect.equal (tierOf "docs/guide.md") StructuralScan "docs baseline" }
          test "sampleApp → StructuralScan" { Expect.equal (tierOf "samples/App/Program.fs") StructuralScan "sampleApp baseline" }
          test "skill → RestoreBuild" { Expect.equal (tierOf "skills/ship.md") RestoreBuild "skill baseline" }
          test "design → RestoreBuild" { Expect.equal (tierOf "design/tokens.json") RestoreBuild "design baseline" }
          test "generatedProduct → FocusedTests (snapped to the declared FocusedTests)" {
              Expect.equal (tierOf "src/Internal.fs") FocusedTests "generatedProduct baseline"
          }
          test "package → FocusedTests" { Expect.equal (tierOf "src/Api.fsi") FocusedTests "package baseline" }
          test "release → FullVerify" { Expect.equal (tierOf "release/notes.md") FullVerify "release baseline" }

          test "a cheap docs change does NOT pull in a deeper tier (SC-003)" {
              Expect.equal (tierOf "docs/guide.md") StructuralScan "no deeper tier without a positive match"
          } ]
