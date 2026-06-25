module FS.GG.Governance.HumanText.Tests.SmokeSnapshotTests

open System
open System.IO
open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.Tests.Support

// T017 [US1]: a committed plain-text smoke snapshot per command for a fixed fixture; rendering twice
// is identical and matches the snapshot. Non-contractual but stable. Bless via BLESS_SNAPSHOT=1 (or
// auto-write on first run when the baseline is absent).

let private snapshotDir =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.HumanText.Tests", "snapshots")

let private cases: (string * string) list =
    [ "route", HumanText.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes
      "explain", HumanText.ofRouteExplanation explanation
      "ship", HumanText.ofShipDecision blockedDecision None []
      "verify", HumanText.ofVerifyDecision blockedDecision None []
      "release", HumanText.ofReleaseReport blockedReleaseReport
      "evidence", HumanText.ofCacheEligibilityReport evidenceReport ]

let private normalize (s: string) = s.Replace("\r\n", "\n")

[<Tests>]
let tests =
    testList
        "SmokeSnapshot"
        [ for name, text in cases ->
              test (sprintf "%s matches its committed smoke snapshot" name) {
                  Directory.CreateDirectory snapshotDir |> ignore
                  let path = Path.Combine(snapshotDir, name + ".txt")
                  let bless = Environment.GetEnvironmentVariable "BLESS_SNAPSHOT" = "1"

                  if bless || not (File.Exists path) then
                      File.WriteAllText(path, normalize text)

                  let baseline = File.ReadAllText path
                  Expect.equal (normalize text) (normalize baseline) "render must match the committed snapshot"

                  // rendering twice is identical (stability)
                  let again =
                      cases |> List.find (fun (n, _) -> n = name) |> snd

                  Expect.equal (normalize text) (normalize again) "render is stable across calls"
              } ]
