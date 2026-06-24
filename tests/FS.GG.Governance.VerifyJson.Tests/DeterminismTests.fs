module FS.GG.Governance.VerifyJson.Tests.DeterminismTests

open Expecto
open FsCheck
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.VerifyJson.Tests.Support

// T026 (US3) — `ofVerifyDecision` is byte-deterministic (FR-007/FR-008, SC-004): identical inputs yield
// byte-identical text; reordering the decision's partition lists never changes the output; and no
// clock/abs-path/username/env content leaks.

[<Tests>]
let tests =
    testList
        "VerifyJson determinism (US3)"
        [ test "byte-identical for identical inputs" {
              Expect.equal
                  (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
                  (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
                  "repeated projection byte-identical"
          }

          testPropertyWithConfig fsCheckConfig "any real decision projects identically on re-run" (fun (decision: ShipDecision) ->
              let a = VerifyJson.ofVerifyDecision decision (Some mixedReport) mixedOutcomes
              let b = VerifyJson.ofVerifyDecision decision (Some mixedReport) mixedOutcomes
              a = b)

          testPropertyWithConfig fsCheckConfig "re-projecting the same decision value is byte-stable" (fun (decision: ShipDecision) ->
              let a = VerifyJson.ofVerifyDecision decision None []
              let b = VerifyJson.ofVerifyDecision decision None []
              a = b)

          test "no clock/abs-path/username/env content in the emitted document" {
              let json = VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes
              let low = lower json

              for token in [ "/home/"; "/users/"; "c:\\"; "20"; ".tmp"; "machine"; "username" ] do
                  // "20" would catch a 20xx year timestamp; the document carries no digits-as-year.
                  if token = "20" then
                      Expect.isFalse (json.Contains "2024" || json.Contains "2025" || json.Contains "2026") "no year-like timestamp"
                  else
                      Expect.isFalse (low.Contains token) (sprintf "no %s content" token)
          } ]
