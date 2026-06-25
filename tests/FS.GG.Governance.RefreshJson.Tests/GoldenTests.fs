module FS.GG.Governance.RefreshJson.Tests.GoldenTests

open System
open System.IO
open Expecto
open FS.GG.Governance.RefreshJson
open FS.GG.Governance.RefreshJson.Tests.Support

// `ofRefreshDecision` over the mixed fixture equals the committed golden baseline (SC-004). The baseline is
// produced by running this once with BLESS_REFRESH_GOLDEN=1 and committed.

[<Tests>]
let tests =
    testList
        "Golden"
        [ test "ofRefreshDecision over the mixed fixture equals the committed golden baseline" {
              let actual = RefreshJson.ofRefreshDecision decisionMixed

              if Environment.GetEnvironmentVariable "BLESS_REFRESH_GOLDEN" = "1" then
                  match Path.GetDirectoryName goldenPath with
                  | null -> ()
                  | dir -> Directory.CreateDirectory dir |> ignore

                  File.WriteAllText(goldenPath, actual)

              let golden = File.ReadAllText goldenPath
              Expect.equal actual golden "refresh.json drifted from the golden baseline — if intended, regenerate with BLESS_REFRESH_GOLDEN=1"
          } ]
