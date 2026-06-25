module FS.GG.Governance.HumanText.Tests.NonContractualTextTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.RouteJson
open FS.GG.Governance.HumanText.Tests.Support

// T048 [SC-008]: the plain text is NON-CONTRACTUAL — it is a separate projection from the JSON
// contract. The JSON projection does not depend on HumanText, so a wording/layout change to the
// human text can never perturb a JSON golden; the JSON stays byte-identical and deterministic.

[<Tests>]
let tests =
    testList
        "NonContractualText"
        [ test "the JSON contract is independent of and distinct from the human text" {
              let report = routeWithFindings
              let text = HumanText.ofRouteResult report (Some evidenceReport) mixedOutcomes
              let json = RouteJson.ofRouteResult report (Some evidenceReport) mixedOutcomes

              // two genuinely different projections of one report value.
              Expect.notEqual text json "human text and JSON are distinct projections"
              // the JSON carries the versioned schema header (the contract); the text does not.
              Expect.stringContains json RouteJson.schemaVersion "json carries the schemaVersion contract header"
              Expect.isFalse (text.Contains RouteJson.schemaVersion) "human text carries no schemaVersion contract token"
          }

          test "the JSON projection is byte-identical across repeated renders (the contract is stable)" {
              let a = RouteJson.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes
              let b = RouteJson.ofRouteResult routeWithFindings (Some evidenceReport) mixedOutcomes
              Expect.equal a b "JSON contract is deterministic regardless of human-text rendering"
          } ]
