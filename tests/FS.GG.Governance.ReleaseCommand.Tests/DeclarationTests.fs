module FS.GG.Governance.ReleaseCommand.Tests.DeclarationTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseCommand
open FS.GG.Governance.ReleaseCommand.Tests.Support

// `Declaration.parse` is the row-local `.fsgg/release.yml` adapter: well-formed content → `Ok` with six
// F053 rules (normalized to the stable composite key), the F054 expectations + the F054 source layout;
// malformed/absent values → `Error DeclError`. Product-neutral: every value comes from the file.

let private okDecl yml =
    match Declaration.parse (ymlLines yml) with
    | Ok d -> d
    | Error e -> failtestf "expected Ok, got Error: %s" e.Reason

let private isErr yml =
    match Declaration.parse (ymlLines yml) with
    | Error _ -> true
    | Ok _ -> false

[<Tests>]
let tests =
    testList
        "Declaration"
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
              // Each parsed kind round-trips through the F053 stable token.
              let d = okDecl releaseYmlAllBlocking
              let tokens = d.Rules |> List.map (fun r -> Release.releaseRuleKindToken r.Kind)
              Expect.contains tokens "versionBump" "versionBump present"
              Expect.contains tokens "trustedPublishing" "trustedPublishing present"
          }

          test "an absent expectation for a declared family is allowed (sensing resolves it later)" {
              // Drop the requiredProvenance criterion; the provenance RULE is still declared.
              let yml = releaseYmlAllBlocking.Replace("  requiredProvenance: [attestation]\n", "")
              let d = okDecl yml
              Expect.equal (d.Rules |> List.length) 6 "still six rules"
              Expect.equal d.Expectations.RequiredProvenance None "criterion absent"
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
