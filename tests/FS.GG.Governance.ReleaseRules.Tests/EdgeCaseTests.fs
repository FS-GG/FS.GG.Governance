module FS.GG.Governance.ReleaseRules.Tests.EdgeCaseTests

open Expecto
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// Spec edge cases: the EMPTY rule set ⇒ no findings and a clean Pass decision; an all-advisory-violation
// set ⇒ Pass with the violations visible (none in Blockers).

[<Tests>]
let tests =
    testList
        "EdgeCaseTests"
        [ test "the empty rule set ⇒ zero findings and a clean Pass decision" {
              let d = Release.evaluateRelease [] { States = Map.empty }
              Expect.isEmpty (Release.evaluate [] { States = Map.empty }) "no rules ⇒ no findings"
              Expect.equal d.Verdict Pass "empty ⇒ Pass"
              Expect.equal d.ExitCodeBasis Clean "empty ⇒ Clean"
              Expect.isEmpty d.Blockers "empty ⇒ no blockers"
              Expect.isEmpty d.Warnings "empty ⇒ no warnings"
              Expect.isEmpty d.Passing "empty ⇒ no passing"
          }

          test "an all-advisory-violation set ⇒ Pass with the violations visible, none in Blockers" {
              let rules = allKinds |> List.map (fun k -> advisory k "pkg")
              // Every fact unmet ⇒ every rule violated, but all base-advisory.
              let facts = factsOf (rules |> List.map (fun r -> r.Kind, Unmet))
              let d = Release.evaluateRelease rules facts
              Expect.equal d.Verdict Pass "all-advisory violations ⇒ Pass"
              Expect.isEmpty d.Blockers "no advisory violation blocks"
              Expect.equal d.Passing.Length rules.Length "base-advisory violations land in Passing, visible"
          } ]
