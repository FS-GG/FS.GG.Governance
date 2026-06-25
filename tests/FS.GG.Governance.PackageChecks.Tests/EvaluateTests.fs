module FS.GG.Governance.PackageChecks.Tests.EvaluateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.PackageChecks
open FS.GG.Governance.PackageChecks.Model
open FS.GG.Governance.PackageChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "pkg" "src/Foo.fsi" (Some "pkg-evidence")

let private factsWith baseline transcripts =
    { BaselineSource = normalizePath "src/Foo.fsi"
      Baseline = baseline
      Transcripts = transcripts }

[<Tests>]
let tests =
    testList
        "PackageChecks.evaluate"
        [ test "BaselineMatches + no transcripts ⇒ zero findings" {
              Expect.isEmpty (PackageChecks.evaluate req (factsWith BaselineMatches [])) "clean surface yields no findings"
          }

          test "BaselineDrift ⇒ one Blocking package.baseline-drift naming the members" {
              let facts = factsWith (BaselineDrift([ "val added: int" ], [ "val gone: string" ])) []
              let findings = PackageChecks.evaluate req facts
              Expect.hasLength findings 1 "exactly one drift finding"
              let f = List.head findings
              Expect.equal f.Code "package.baseline-drift" "code"
              Expect.equal f.BaseSeverity Blocking "deterministic drift is Blocking"
              Expect.isFalse f.IsInputState "drift is a rule violation, not an input state"
              Expect.stringContains f.Message "val added: int" "names the added member"
              Expect.stringContains f.Message "val gone: string" "names the removed member"
              Expect.equal f.EvidenceTag (Some(EvidenceTag "pkg-evidence")) "carries the declared tag"
          }

          test "BaselineAbsent ⇒ IsInputState package.baseline-absent (never a silent pass)" {
              let facts = factsWith (BaselineAbsent(SurfaceTokens [ "val a"; "val b" ])) []
              let findings = PackageChecks.evaluate req facts
              Expect.hasLength findings 1 "one input-state finding"
              let f = List.head findings
              Expect.equal f.Code "package.baseline-absent" "code"
              Expect.isTrue f.IsInputState "absent baseline is an input state"
          }

          test "BaselineUnreadable ⇒ IsInputState package.baseline-unreadable naming the source" {
              let facts = factsWith (BaselineUnreadable "src/Foo.fsi.baseline: denied") []
              let f = List.head (PackageChecks.evaluate req facts)
              Expect.equal f.Code "package.baseline-unreadable" "code"
              Expect.isTrue f.IsInputState "unreadable is an input state"
              Expect.stringContains f.Message "denied" "names the source detail"
          }

          test "TranscriptCompileFailed ⇒ Blocking package.transcript-compile naming the example" {
              let facts =
                  factsWith
                      BaselineMatches
                      [ { ExampleId = "ex1"
                          Source = normalizePath "src/transcripts/ex1.fsx"
                          Outcome = TranscriptCompileFailed "parse error" } ]

              let f = List.head (PackageChecks.evaluate req facts)
              Expect.equal f.Code "package.transcript-compile" "code"
              Expect.equal f.BaseSeverity Blocking "Blocking"
              Expect.stringContains f.Message "ex1" "names the example"
          }

          test "TranscriptResultChanged ⇒ Blocking package.transcript-result naming both values" {
              let facts =
                  factsWith
                      BaselineMatches
                      [ { ExampleId = "ex2"
                          Source = normalizePath "src/transcripts/ex2.fsx"
                          Outcome = TranscriptResultChanged("42", "43") } ]

              let f = List.head (PackageChecks.evaluate req facts)
              Expect.equal f.Code "package.transcript-result" "code"
              Expect.stringContains f.Message "42" "names expected"
              Expect.stringContains f.Message "43" "names actual"
          }

          test "TranscriptUnlocatable ⇒ IsInputState package.transcript-unlocatable" {
              let facts =
                  factsWith
                      BaselineMatches
                      [ { ExampleId = "ex3"
                          Source = normalizePath "src/transcripts/ex3.fsx"
                          Outcome = TranscriptUnlocatable "not found" } ]

              let f = List.head (PackageChecks.evaluate req facts)
              Expect.equal f.Code "package.transcript-unlocatable" "code"
              Expect.isTrue f.IsInputState "unlocatable is an input state"
          } ]
