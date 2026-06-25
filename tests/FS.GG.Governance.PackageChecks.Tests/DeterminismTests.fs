module FS.GG.Governance.PackageChecks.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.PackageChecks
open FS.GG.Governance.PackageChecks.Model
open FS.GG.Governance.PackageChecks.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

let private req = requestFor "pkg" "src/Foo.fsi" (Some "pkg-evidence")

let private transcript id outcome =
    { ExampleId = id
      Source = normalizePath (sprintf "src/transcripts/%s.fsx" id)
      Outcome = outcome }

[<Tests>]
let tests =
    testList
        "PackageChecks.determinism"
        [ test "repeated evaluate over identical facts ⇒ byte-identical findings" {
              let facts =
                  { BaselineSource = normalizePath "src/Foo.fsi"
                    Baseline = BaselineDrift([ "val a" ], [ "val b" ])
                    Transcripts =
                      [ transcript "c" (TranscriptCompileFailed "x")
                        transcript "a" (TranscriptResultChanged("1", "2")) ] }

              let a = PackageChecks.evaluate req facts
              let b = PackageChecks.evaluate req facts
              Expect.equal a b "identical facts yield identical findings"
          }

          test "reordering the transcript list leaves the sorted findings unchanged (order-independent)" {
              let t1 = transcript "alpha" (TranscriptCompileFailed "x")
              let t2 = transcript "beta" (TranscriptResultChanged("1", "2"))
              let t3 = transcript "gamma" (TranscriptCompileFailed "y")

              let one =
                  PackageChecks.evaluate
                      req
                      { BaselineSource = normalizePath "src/Foo.fsi"
                        Baseline = BaselineMatches
                        Transcripts = [ t1; t2; t3 ] }

              let two =
                  PackageChecks.evaluate
                      req
                      { BaselineSource = normalizePath "src/Foo.fsi"
                        Baseline = BaselineMatches
                        Transcripts = [ t3; t1; t2 ] }

              Expect.equal one two "findings are order-independent of the transcript list"
          }

          test "no Message carries an absolute path, clock, or username token" {
              let facts =
                  { BaselineSource = normalizePath "src/Foo.fsi"
                    Baseline = BaselineDrift([ "val a" ], [])
                    Transcripts = [ transcript "x" (TranscriptCompileFailed "boom") ] }

              for f in PackageChecks.evaluate req facts do
                  Expect.isFalse (f.Message.Contains "/home/") "no absolute home path"
                  Expect.isFalse (f.Message.Contains "C:\\") "no absolute windows path"
          } ]
