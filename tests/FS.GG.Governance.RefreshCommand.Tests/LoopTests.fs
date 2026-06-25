module FS.GG.Governance.RefreshCommand.Tests.LoopTests

open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel
open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshCommand.Tests.Support

// Pure MVU transitions (Constitution IV — emitted-effect assertions) and the exit/roll-up mapping.

let private isSense =
    function
    | Loop.SenseSource _ -> true
    | _ -> false

let private isReadRecorded =
    function
    | Loop.ReadRecorded _ -> true
    | _ -> false

let private isRegenerate =
    function
    | Loop.RegenerateView _ -> true
    | _ -> false

let private isRecordProv =
    function
    | Loop.RecordProvenance _ -> true
    | _ -> false

[<Tests>]
let tests =
    testList
        "Loop"
        [ test "init emits LoadManifest" {
              let _, effects = Loop.init (requestFor ".")
              Expect.equal effects [ Loop.LoadManifest "." ] "init requests the manifest read"
          }

          test "ManifestLoaded(Ok) emits SenseSource + ReadRecorded per in-scope entry" {
              let m0, _ = Loop.init (requestFor ".")
              let _, effects = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlTwoViews))) m0
              Expect.equal (effects |> List.filter isSense |> List.length) 2 "one SenseSource per view"
              Expect.equal (effects |> List.filter isReadRecorded |> List.length) 2 "one ReadRecorded per view"
          }

          test "a stale entry emits RegenerateView, then Regenerated'(Ok) emits RecordProvenance" {
              let m0, _ = Loop.init (requestFor ".")
              let m1, _ = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlOneView))) m0
              let m2, _ = Loop.update (Loop.Sensed("doc", Ok(digestsOf [ "d2" ], GeneratorVersion "g1"))) m1
              // Recorded differs ⇒ stale ⇒ decideSensing emits RegenerateView (write mode).
              let m3, effects3 = Loop.update (Loop.RecordedRead("doc", Some(digestsOf [ "d1" ], GeneratorVersion "g1"))) m2
              Expect.isTrue (effects3 |> List.exists isRegenerate) "a stale view dispatches RegenerateView"

              let _, effects4 = Loop.update (Loop.Regenerated'("doc", Ok(ArtifactHash "out"))) m3
              Expect.isTrue (effects4 |> List.exists isRecordProv) "a successful regeneration records provenance"
          }

          test "a non-stale entry emits neither RegenerateView nor RecordProvenance" {
              let m0, _ = Loop.init (requestFor ".")
              let m1, _ = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlOneView))) m0
              let m2, _ = Loop.update (Loop.Sensed("doc", Ok(digestsOf [ "d1" ], GeneratorVersion "g1"))) m1
              let _, effects3 = Loop.update (Loop.RecordedRead("doc", Some(digestsOf [ "d1" ], GeneratorVersion "g1"))) m2
              Expect.isFalse (effects3 |> List.exists isRegenerate) "a current view never regenerates"
          }

          test "exitCode maps every outcome (cli.md exit-code table)" {
              Expect.equal (Loop.exitCode NothingToRefresh) 0 "ok"
              Expect.equal (Loop.exitCode StaleUnresolved') 1 "stale-unresolved"
              Expect.equal (Loop.exitCode UsageError') 2 "usage"
              Expect.equal (Loop.exitCode InputUnavailable) 3 "input-unavailable"
              Expect.equal (Loop.exitCode ToolError) 4 "tool-error"
              Expect.equal (Loop.exitCode ViewsRegenerated) 5 "regenerated"
          }

          test "roll-up precedence: any unresolved ⇒ StaleUnresolved' even alongside a would-regenerate" {
              // Two views: one stale-unresolved (sense error), one would-regenerate (dry-run stale).
              let req = { requestFor "." with DryRun = true }
              let m0, _ = Loop.init req
              let m1, _ = Loop.update (Loop.ManifestLoaded(Ok(parseYml refreshYmlTwoViews))) m0
              let m2, _ = Loop.update (Loop.Sensed("doc", Error "source not found: a.txt")) m1
              let m3, _ = Loop.update (Loop.Sensed("cat", Ok(digestsOf [ "x" ], GeneratorVersion "g1"))) m2
              let m4, _ = Loop.update (Loop.RecordedRead("doc", None)) m3
              let m5, _ = Loop.update (Loop.RecordedRead("cat", None)) m4
              Expect.equal m5.Exit StaleUnresolved' "unresolved outranks would-regenerate"
          } ]
