module FS.GG.Governance.ReleaseDeclaration.Tests.DeclarationTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseDeclaration
open FS.GG.Governance.ReleaseDeclaration.Tests.Support

// The F055 rules/expectations/layout parse, re-homed VERBATIM from the release host's `DeclarationTests`
// onto the shared leaf (065 / research D6): well-formed content → `Ok` with six F053 rules (normalized to
// the stable composite key), the F054 expectations + the F054 source layout; malformed/absent values →
// `Error DeclError`. Product-neutral: every value comes from the file. Behaviour is unchanged by the lift.

[<Tests>]
let tests =
    testList
        "Declaration.rehomed"
        [ test "a well-formed release.yml yields six rules in F053 composite order" {
              let d = okDecl releaseYmlAllBlocking

              Expect.equal
                  (d.Rules |> List.map (fun r -> r.Kind))
                  [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]
                  "six families in releaseRuleKindOrdinal order"

              Expect.isTrue (d.Rules |> List.forall (fun r -> r.BaseSeverity = Blocking)) "all blocking"
              Expect.isTrue (d.Rules |> List.forall (fun r -> r.Maturity = BlockOnRelease)) "all block-on-release"
              Expect.isTrue (d.Rules |> List.forall (fun r -> r.Surface = surfaceId)) "carry the declared surface"
          }

          test "088: an OPTIONAL advisory apiCompatibility rule parses as an additive seventh rule" {
              // The six required families plus a declared advisory ApiCompatibility rule (base blocking
              // relaxed to Warn ⇒ advisory). Recognized + allowed; not required of other declarations.
              let yml =
                  releaseYmlAllBlocking.Replace(
                      "expectations:\n",
                      "  - kind: api-compatibility\n    severity: blocking\n    maturity: warn\nexpectations:\n"
                  )

              let d = okDecl yml
              Expect.equal d.Rules.Length 7 "six required + one optional ApiCompatibility"

              let ac = d.Rules |> List.find (fun r -> r.Kind = ApiCompatibility)
              Expect.equal ac.Maturity Warn "declared advisory (base blocking relaxed to Warn)"
              Expect.equal (Release.releaseRuleKindToken ac.Kind) "apiCompatibility" "recognized kind token"

              // ApiCompatibility sorts LAST by the F053 composite key (ordinal 6).
              Expect.equal (d.Rules |> List.last |> fun r -> r.Kind) ApiCompatibility "sorts last (ordinal 6)"
          }

          test "088: declaring api-compatibility TWICE is rejected (one each)" {
              let yml =
                  releaseYmlAllBlocking.Replace(
                      "expectations:\n",
                      "  - kind: api-compatibility\n    severity: blocking\n    maturity: warn\n"
                      + "  - kind: api-compatibility\n    severity: blocking\n    maturity: warn\nexpectations:\n"
                  )

              match Declaration.parse (yml.Replace("\r\n", "\n").Split('\n') |> List.ofArray) with
              | Error _ -> ()
              | Ok _ -> failtest "expected a duplicate-family rejection"
          }

          test "expectations and layout are read verbatim from the file (product-neutral)" {
              let d = okDecl releaseYmlAllBlocking
              Expect.equal d.Expectations.Surface surfaceId "surface"
              Expect.equal d.Expectations.VersionBaseline (Some "1.2.0") "version baseline"
              Expect.equal d.Expectations.RequiredMetadataFields (Some [ "authors"; "license" ]) "metadata fields"
              Expect.equal d.Expectations.ExpectedPins (Some(Map [ "base", "9.0.0" ])) "expected pins"
              Expect.equal d.Layout.VersionPath "version.txt" "version path"
              Expect.equal d.Layout.ProvenancePath "provenance.txt" "provenance path"
          }

          test "kind tokens map onto F053 releaseRuleKindToken (kebab/camel tolerant)" {
              let d = okDecl releaseYmlAllBlocking
              let tokens = d.Rules |> List.map (fun r -> Release.releaseRuleKindToken r.Kind)
              Expect.contains tokens "versionBump" "versionBump present"
              Expect.contains tokens "trustedPublishing" "trustedPublishing present"
          }

          test "an absent expectation for a declared family is allowed (sensing resolves it later)" {
              let yml = releaseYmlAllBlocking.Replace("  requiredProvenance: [attestation]\n", "")
              let d = okDecl yml
              Expect.equal (d.Rules |> List.length) 6 "still six rules"
              Expect.equal d.Expectations.RequiredProvenance None "criterion absent"
          }

          test "a present-but-malformed list criterion (a scalar where a sequence was declared) is rejected, never a silent None (M-ADPT)" {
              // The old parser degraded a wrong-SHAPED criterion to `None` — indistinguishable from ABSENT
              // (the case above) — silently dropping a declared criterion (violates 'never partial facts').
              let yml =
                  releaseYmlAllBlocking.Replace("  requiredMetadataFields: [authors, license]\n", "  requiredMetadataFields: notalist\n")

              Expect.isTrue (isErr yml) "a scalar where a sequence was declared is malformed, not absent"
          }

          test "a present-but-malformed expectedPins (a scalar where a mapping was declared) is rejected (M-ADPT)" {
              let yml =
                  releaseYmlAllBlocking.Replace("  expectedPins:\n    base: \"9.0.0\"\n", "  expectedPins: notamapping\n")

              Expect.isTrue (isErr yml) "a scalar where a mapping was declared is malformed, not absent"
          }

          test "a present-but-non-mapping expectations section is rejected, not read as absent (M-ADPT)" {
              let block =
                  "expectations:\n"
                  + "  versionBaseline: \"1.2.0\"\n"
                  + "  requiredMetadataFields: [authors, license]\n"
                  + "  expectedPins:\n    base: \"9.0.0\"\n"
                  + "  requiredPublishPosture: [plan-present]\n"
                  + "  requiredTrustedPublishing: [oidc]\n"
                  + "  requiredProvenance: [attestation]\n"

              let yml = releaseYmlAllBlocking.Replace(block, "expectations: just-a-scalar\n")
              Expect.isTrue (isErr yml) "a scalar expectations section is malformed, not silently absent"
          }

          test "a missing family is a malformed declaration" {
              let yml =
                  releaseYmlAllBlocking.Replace(
                      "  - kind: provenance\n    severity: blocking\n    maturity: block-on-release\n",
                      ""
                  )

              Expect.isTrue (isErr yml) "missing provenance family rejected"
          }

          test "an unknown kind token is a malformed declaration" {
              let yml = releaseYmlAllBlocking.Replace("kind: version-bump", "kind: not-a-family")
              Expect.isTrue (isErr yml) "unknown kind rejected"
          }

          test "a non-mapping / empty document is a malformed declaration" {
              Expect.isTrue (isErr "") "empty"
              Expect.isTrue (isErr "- just\n- a\n- list\n") "non-mapping root"
          }

          test "a missing layout path is a malformed declaration" {
              let yml = releaseYmlAllBlocking.Replace("  provenancePath: provenance.txt\n", "")
              Expect.isTrue (isErr yml) "incomplete layout rejected"
          } ]
