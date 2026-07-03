module FS.GG.Governance.JsonTokens.Tests.JsonTokensTests

open Expecto
open FS.GG.Governance.JsonTokens
open FS.GG.Governance.Config.Model              // Cost, Maturity, EnvironmentClass
open FS.GG.Governance.GateRun.Model             // GateDisposition
open FS.GG.Governance.CommandRecord.Model        // ExitCode (disposition payload)
open FS.GG.Governance.Enforcement.Enforcement   // Severity, Profile
open FS.GG.Governance.Ship.Model                // ExitCodeBasis

// Semantic tests for the 073 closed-enum token leaf, exercising the PUBLIC surface over REAL,
// literally-constructed DU values (Principle V — real values, no mocks). The verbatim strings asserted
// here are exactly what the *Json projection goldens depend on; this table is the single source of truth
// the projections used to hand-copy.

[<Tests>]
let tests =
    testList
        "JsonTokens"
        [ test "costToken emits the verbatim cost strings" {
              Expect.equal (JsonTokens.costToken Cheap) "cheap" "Cheap"
              Expect.equal (JsonTokens.costToken Medium) "medium" "Medium"
              Expect.equal (JsonTokens.costToken High) "high" "High"
              Expect.equal (JsonTokens.costToken Exhaustive) "exhaustive" "Exhaustive"
          }

          test "maturityToken emits the verbatim maturity strings" {
              Expect.equal (JsonTokens.maturityToken Observe) "observe" "Observe"
              Expect.equal (JsonTokens.maturityToken Warn) "warn" "Warn"
              Expect.equal (JsonTokens.maturityToken BlockOnPr) "blockOnPr" "BlockOnPr"
              Expect.equal (JsonTokens.maturityToken BlockOnShip) "blockOnShip" "BlockOnShip"
              Expect.equal (JsonTokens.maturityToken BlockOnRelease) "blockOnRelease" "BlockOnRelease"
          }

          test "severityToken emits the verbatim severity strings" {
              Expect.equal (JsonTokens.severityToken Advisory) "advisory" "Advisory"
              Expect.equal (JsonTokens.severityToken Blocking) "blocking" "Blocking"
          }

          test "environmentToken emits the verbatim environment strings" {
              Expect.equal (JsonTokens.environmentToken Local) "local" "Local"
              Expect.equal (JsonTokens.environmentToken Ci) "ci" "Ci"
              Expect.equal (JsonTokens.environmentToken LocalOrCi) "localOrCi" "LocalOrCi"
              Expect.equal (JsonTokens.environmentToken EnvironmentClass.Release) "release" "Release"
          }

          test "dispositionToken emits the verbatim disposition strings (camelCase notExecuted)" {
              Expect.equal (JsonTokens.dispositionToken (Executed(ExitCode 0, true))) "executed" "Executed"
              Expect.equal (JsonTokens.dispositionToken (Reused(ExitCode 0, true))) "reused" "Reused"
              Expect.equal (JsonTokens.dispositionToken NotExecuted) "notExecuted" "NotExecuted"
          }

          test "basisToken emits the verbatim exit-code-basis strings" {
              Expect.equal (JsonTokens.basisToken Clean) "clean" "Clean"
              Expect.equal (JsonTokens.basisToken Blocked) "blocked" "Blocked"
          }

          test "profileToken emits the verbatim profile strings" {
              Expect.equal (JsonTokens.profileToken Light) "light" "Light"
              Expect.equal (JsonTokens.profileToken Standard) "standard" "Standard"
              Expect.equal (JsonTokens.profileToken Strict) "strict" "Strict"
              Expect.equal (JsonTokens.profileToken Profile.Release) "release" "Release"
          } ]
