module FS.GG.Governance.FreshnessKey.Tests.InspectionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.FreshnessKey.Tests.Support

// The no-hide explainer (SC-005, FR-007, US3 #1–#2): `diff` names exactly the differing categories, is
// consistent with `matches`, compares covered artifacts as a set, and `categoryToken` is the committed,
// total, injective readable vocabulary.

// Single-category variants from `baseInputs` — each flips EXACTLY one category (so `diff` should return
// exactly that one). Distinct from Support.allCategories, whose Command variant flips both Command and
// CommandVersion to model present↔absent.
let private singleFieldVariants: (InputCategory * FreshnessInputs) list =
    [ CheckIdentity, { baseInputs with Check = CheckId "build:other" }
      DomainIdentity, { baseInputs with Domain = DomainId "release" }
      CommandIdentity, { baseInputs with Command = Some(CommandId "msbuild") }
      EnvironmentClassCat, { baseInputs with Environment = Ci }
      RuleHashCat, { baseInputs with RuleHash = RuleHash "r2" }
      CoveredArtifactsCat, { baseInputs with CoveredArtifacts = [ ArtifactHash "h9" ] }
      CommandVersionCat, { baseInputs with CommandVersion = Some(CommandVersion "9.0") }
      GeneratorVersionCat, { baseInputs with GeneratorVersion = GeneratorVersion "g2" }
      BaseRevisionCat, { baseInputs with Base = Revision "ccc" }
      HeadRevisionCat, { baseInputs with Head = Revision "ddd" } ]

// The committed categoryToken table (contracts/freshness-key-api.md).
let private tokenTable: (InputCategory * string) list =
    [ CheckIdentity, "check"
      DomainIdentity, "domain"
      CommandIdentity, "command"
      EnvironmentClassCat, "environmentClass"
      RuleHashCat, "ruleHash"
      CoveredArtifactsCat, "coveredArtifacts"
      CommandVersionCat, "commandVersion"
      GeneratorVersionCat, "generatorVersion"
      BaseRevisionCat, "baseRevision"
      HeadRevisionCat, "headRevision" ]

[<Tests>]
let tests =
    testList
        "Inspection"
        [ test "diff x x = [] (reflexive, SC-005)" {
              Expect.equal (FreshnessKey.diff baseInputs baseInputs) [] "equal inputs differ in nothing"
          }

          testList
              "diff names exactly the single changed category"
              [ for (category, variant) in singleFieldVariants ->
                    test (Model.categoryToken category) {
                        Expect.equal
                            (FreshnessKey.diff baseInputs variant)
                            [ category ]
                            (sprintf "diff must report exactly [%A]" category)
                    } ]

          test "a multi-field variant returns exactly the changed categories in fixed order" {
              let variant =
                  { baseInputs with
                      RuleHash = RuleHash "rX"
                      Base = Revision "bX"
                      Check = CheckId "check:X" }
              // Fixed key-encoding order: Check, then RuleHash, then Base.
              Expect.equal
                  (FreshnessKey.diff baseInputs variant)
                  [ CheckIdentity; RuleHashCat; BaseRevisionCat ]
                  "diff lists exactly the changed categories in the fixed order"
          }

          test "covered artifacts compared as a set: reordered/duplicated ⇒ not reported by diff" {
              let reordered =
                  { baseInputs with CoveredArtifacts = [ ArtifactHash "h1"; ArtifactHash "h2"; ArtifactHash "h2" ] }
              Expect.equal
                  (FreshnessKey.diff baseInputs reordered)
                  []
                  "a same-set reorder/dup must not appear in diff"
          }

          testPropertyWithConfig fscheckConfig "matches a b = (diff a b = []) (predicate/diff agreement)"
          <| fun (a: FreshnessInputs) (b: FreshnessInputs) ->
              FreshnessKey.matches a b = (FreshnessKey.diff a b = [])

          test "categoryToken equals the committed table for all 10 cases (T018)" {
              for (category, token) in tokenTable do
                  Expect.equal (Model.categoryToken category) token (sprintf "%A token" category)
          }

          test "categoryToken is total and injective over all 10 cases" {
              let tokens = allCategoryCases |> List.map Model.categoryToken
              Expect.equal tokens.Length 10 "all 10 categories produce a token"
              Expect.equal
                  (tokens |> List.distinct |> List.length)
                  10
                  "categoryToken is injective — no two categories share a token"
          } ]
