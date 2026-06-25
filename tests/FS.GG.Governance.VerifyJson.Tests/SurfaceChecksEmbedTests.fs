module FS.GG.Governance.VerifyJson.Tests.SurfaceChecksEmbedTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.VerifyJson.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

// T044 — the additive `surfaceChecks` section: byte-identical when empty; correct shape when non-empty.

let private finding code severity inputState (tag: string option) : SC.SurfaceFinding =
    { Domain = SC.PackageDomain
      Surface = SurfaceId "pkg"
      Code = code
      Location = { File = normalizePath "src/Foo.fsi"; Detail = "drift" }
      BaseSeverity = severity
      Maturity = BlockOnPr
      EvidenceTag = tag |> Option.map EvidenceTag
      IsInputState = inputState
      Message = "public surface drifted" }

let private parse (s: string) = JsonDocument.Parse s

[<Tests>]
let tests =
    testList
        "VerifyJson.surfaceChecks"
        [ test "empty findings ⇒ byte-identical to ofVerifyDecision (existing golden untouched)" {
              let withEmpty = VerifyJson.ofVerifyDecisionWithSurfaceChecks richDecision (Some mixedReport) mixedOutcomes []
              let baseline = VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes
              Expect.equal withEmpty baseline "empty ⇒ byte-identical"
              Expect.isFalse (withEmpty.Contains "surfaceChecks") "no surfaceChecks field when empty"
          }

          test "non-empty findings ⇒ a surfaceChecks array with the documented element shape" {
              let findings =
                  [ finding "package.baseline-drift" Blocking false (Some "pkg-evidence")
                    finding "docs.example-freshness" Advisory false None ]

              let json = VerifyJson.ofVerifyDecisionWithSurfaceChecks emptyCleanDecision None [] findings
              use doc = parse json
              let arr = doc.RootElement.GetProperty "surfaceChecks"
              Expect.equal (arr.GetArrayLength()) 2 "two entries"

              let first = arr.[0]
              Expect.equal (first.GetProperty("domain").GetString()) "package" "domain token"
              Expect.equal (first.GetProperty("code").GetString()) "package.baseline-drift" "code"
              Expect.equal (first.GetProperty("file").GetString()) "src/Foo.fsi" "file"
              Expect.equal (first.GetProperty("severity").GetString()) "blocking" "severity"
              Expect.isFalse (first.GetProperty("inputState").GetBoolean()) "inputState"
              Expect.equal (first.GetProperty("evidenceTag").GetString()) "pkg-evidence" "evidence tag present"

              // The advisory entry omits evidenceTag (none declared) and carries severity advisory.
              let second = arr.[1]
              Expect.equal (second.GetProperty("severity").GetString()) "advisory" "advisory severity surfaces"
              Expect.isFalse (second.TryGetProperty("evidenceTag") |> fst) "evidenceTag omitted when None"
          }

          test "schemaVersion is unchanged by the additive section" {
              let findings = [ finding "package.baseline-drift" Blocking false None ]
              let json = VerifyJson.ofVerifyDecisionWithSurfaceChecks emptyCleanDecision None [] findings
              use doc = parse json
              Expect.equal (doc.RootElement.GetProperty("schemaVersion").GetString()) VerifyJson.schemaVersion "schemaVersion unchanged"
          } ]
