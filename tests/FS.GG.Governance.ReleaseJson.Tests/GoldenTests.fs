module FS.GG.Governance.ReleaseJson.Tests.GoldenTests

open System
open System.IO
open Expecto
open FS.GG.Governance.ReleaseJson
open FS.GG.Governance.ReleaseJson.Tests.Support

// `ofRelease` over a fixed fixture equals the committed golden baseline (SC-007). The baseline is produced
// by running this once with BLESS_RELEASE_GOLDEN=1 (the surface-bless idiom) and committed.

[<Tests>]
let tests =
    testList
        "Golden"
        [ test "ofRelease over the mixed fixture equals the committed golden baseline" {
              let actual = ReleaseJson.ofRelease decisionMixed sensedMixed

              if Environment.GetEnvironmentVariable "BLESS_RELEASE_GOLDEN" = "1" then
                  match Path.GetDirectoryName goldenPath with
                  | null -> ()
                  | dir -> Directory.CreateDirectory dir |> ignore

                  File.WriteAllText(goldenPath, actual)

              let golden = File.ReadAllText goldenPath
              Expect.equal actual golden "release.json drifted from the golden baseline — if intended, regenerate with BLESS_RELEASE_GOLDEN=1"
          } ]
