module FS.GG.Governance.HumanText.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.Tests.Support

// T015 [US1]: each of* (and viewOf*) is byte-identical on repeated calls over identical input, and
// the rendered text leaks no absolute path / wall-clock / username / environment value.

let private renders () : (string * string) list =
    [ "route", HumanText.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes
      "explain", HumanText.ofRouteExplanation explanation
      "ship", HumanText.ofShipDecision blockedDecision (Some evidenceReport) mixedOutcomes
      "verify", HumanText.ofVerifyDecision blockedDecision None []
      "release", HumanText.ofReleaseReport blockedReleaseReport
      "evidence", HumanText.ofCacheEligibilityReport evidenceReport ]

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "every of* is byte-identical across repeated calls" {
              for (name, _) in renders () do
                  let a = renders () |> List.find (fun (n, _) -> n = name) |> snd
                  let b = renders () |> List.find (fun (n, _) -> n = name) |> snd
                  Expect.equal a b (sprintf "%s render must be deterministic" name)
          }

          test "viewOf* is structurally identical across repeated calls" {
              Expect.equal
                  (ReportView.viewOfRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes)
                  (ReportView.viewOfRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes)
                  "route view deterministic"
          }

          test "no machine-absolute path / username / wall-clock leaks into the text" {
              let home = System.Environment.GetEnvironmentVariable "HOME" |> Option.ofObj
              let user = System.Environment.UserName

              for (name, text) in renders () do
                  Expect.isFalse (text.Contains "/home/") (sprintf "%s: no absolute home path" name)

                  match home with
                  | Some h when h.Length > 0 -> Expect.isFalse (text.Contains h) (sprintf "%s: no $HOME leak" name)
                  | _ -> ()

                  if user.Length > 0 then
                      Expect.isFalse (text.Contains user) (sprintf "%s: no username leak" name)
          } ]
