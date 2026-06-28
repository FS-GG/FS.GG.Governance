module FS.GG.Governance.ReleaseRules.Tests.EvaluateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// US1 (acc. 1–2, FR-001/FR-002/FR-003): `evaluate` emits exactly one finding per declared rule, correctly
// classified from the per-kind `FactState`, carrying the declared base severity + maturity, with a
// self-explaining reason naming the kind token + surface. Plus the public lookups pinned directly (D4/D7).

let private surfaceText (SurfaceId s) = s

[<Tests>]
let tests =
    testList
        "EvaluateTests"
        [ test "one finding per rule of every kind, all Satisfied when every fact is Met" {
              let rules = allKinds |> List.map (fun k -> blocking k "pkg")
              let findings = Release.evaluate rules (allMet rules)

              Expect.equal findings.Length rules.Length "exactly one finding per declared rule"

              Expect.isTrue
                  (findings |> List.forall (fun f -> f.Outcome = Satisfied))
                  "every met fact ⇒ Satisfied"

              for f in findings do
                  Expect.stringContains f.Reason (Release.releaseRuleKindToken f.Kind) "reason names the kind token"
                  Expect.stringContains f.Reason (surfaceText f.Surface) "reason names the governed surface"
          }

          test "a blocking rule with an Unmet fact ⇒ one Violated finding carrying its declared levers" {
              let rule = blocking VersionBump "pkg"
              let findings = Release.evaluate [ rule ] (factsOf [ VersionBump, Unmet ])

              let f = List.exactlyOne findings
              Expect.equal f.Outcome Violated "an unmet fact ⇒ Violated"
              Expect.equal f.BaseSeverity Blocking "declared base severity carried through unchanged"
              Expect.equal f.Maturity BlockOnRelease "declared maturity carried through unchanged"
              Expect.stringContains f.Reason "not met" "reason names the unmet expectation"
          }

          test "the carried BaseSeverity + Maturity equal the rule's declared values (no re-derivation)" {
              let rule = advisory PackageMetadata "pkg"
              let f = List.exactlyOne (Release.evaluate [ rule ] (factsOf [ PackageMetadata, Met ]))
              Expect.equal f.BaseSeverity rule.BaseSeverity "base severity equals the declared value"
              Expect.equal f.Maturity rule.Maturity "maturity equals the declared value"
          }

          test "releaseRuleKindToken returns the exact seven wire tokens" {
              Expect.equal (Release.releaseRuleKindToken VersionBump) "versionBump" "versionBump token"
              Expect.equal (Release.releaseRuleKindToken PackageMetadata) "packageMetadata" "packageMetadata token"
              Expect.equal (Release.releaseRuleKindToken TemplatePins) "templatePins" "templatePins token"
              Expect.equal (Release.releaseRuleKindToken PublishPlan) "publishPlan" "publishPlan token"
              Expect.equal
                  (Release.releaseRuleKindToken TrustedPublishing)
                  "trustedPublishing"
                  "trustedPublishing token"
              Expect.equal (Release.releaseRuleKindToken Provenance) "provenance" "provenance token"
              Expect.equal (Release.releaseRuleKindToken ApiCompatibility) "apiCompatibility" "apiCompatibility token (088)"
          }

          test "releaseRuleKindOrdinal returns 0..6 in the closed declaration order, all distinct" {
              let ordinals = allKinds |> List.map Release.releaseRuleKindOrdinal
              Expect.equal ordinals [ 0; 1; 2; 3; 4; 5; 6 ] "ordinals are 0..6 in declaration order"
              Expect.equal (List.distinct ordinals).Length 7 "all seven ordinals are distinct"
          } ]
