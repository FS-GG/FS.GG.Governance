module FS.GG.Governance.PackageChecks.Tests.EvidenceTagTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.PackageChecks
open FS.GG.Governance.PackageChecks.Model
open FS.GG.Governance.PackageChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private facts =
    { BaselineSource = normalizePath "src/Foo.fsi"
      Baseline = BaselineDrift([ "val a" ], [])
      Transcripts =
        [ { ExampleId = "ex"
            Source = normalizePath "src/transcripts/ex.fsx"
            Outcome = TranscriptCompileFailed "x" } ] }

[<Tests>]
let tests =
    testList
        "PackageChecks.evidenceTag"
        [ test "declared tag ⇒ every finding carries it" {
              let req = requestFor "pkg" "src/Foo.fsi" (Some "pkg-evidence")
              let findings = PackageChecks.evaluate req facts
              Expect.isNonEmpty findings "findings produced"

              for f in findings do
                  Expect.equal f.EvidenceTag (Some(EvidenceTag "pkg-evidence")) "carries the declared tag"
          }

          test "no declared tag ⇒ every finding's EvidenceTag is None" {
              let req = requestFor "pkg" "src/Foo.fsi" None
              let findings = PackageChecks.evaluate req facts
              Expect.isNonEmpty findings "findings produced"

              for f in findings do
                  Expect.equal f.EvidenceTag None "None when the surface declared no tag"
          } ]
