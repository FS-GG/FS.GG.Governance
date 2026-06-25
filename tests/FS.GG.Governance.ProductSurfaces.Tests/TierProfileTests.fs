module FS.GG.Governance.ProductSurfaces.Tests.TierProfileTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US2 — profile escalation (positive-match only, FR-006): a release-oriented profile raises a
// ReleaseSurface target to ReleaseValidation; a strict profile raises the target by ONE rank; a profile
// never lowers below baseline and never raises a kind that did not match. Profiles are read from
// PolicyFacts.Profiles — an undeclared name never escalates.

let private tierUnder profile path =
    match forPath (classifyPaths "product-surface-all-kinds" profile [ path ]) path with
    | Some c -> c.SelectedTier
    | None -> failtestf "expected a classification for '%s' under '%s'" path profile

[<Tests>]
let tests =
    testList
        "ProductSurfaces.TierProfile.US2"
        [ test "release profile raises a ReleaseSurface to ReleaseValidation" {
              Expect.equal (tierUnder "release" "release/notes.md") ReleaseValidation "release-oriented escalation"
          }

          test "release profile does NOT raise a kind that did not match (docs unchanged)" {
              Expect.equal (tierUnder "release" "docs/guide.md") StructuralScan "docs baseline unchanged under release"
          }

          test "strict profile raises the target by one rank (docs StructuralScan → RestoreBuild)" {
              Expect.equal (tierUnder "strict" "docs/guide.md") RestoreBuild "strict +1 rank"
          }

          test "strict profile raises skill RestoreBuild → FocusedTests" {
              Expect.equal (tierUnder "strict" "skills/ship.md") FocusedTests "strict +1 rank on skill"
          }

          test "a non-escalating profile (light) keeps the baseline" {
              Expect.equal (tierUnder "light" "docs/guide.md") StructuralScan "light does not escalate"
          }

          test "an UNDECLARED profile name never escalates (read from PolicyFacts.Profiles)" {
              Expect.equal (tierUnder "not-a-real-profile" "docs/guide.md") StructuralScan "unknown profile ⇒ baseline"
          } ]
