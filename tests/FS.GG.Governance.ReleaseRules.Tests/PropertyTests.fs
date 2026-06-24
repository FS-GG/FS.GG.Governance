module FS.GG.Governance.ReleaseRules.Tests.PropertyTests

open Expecto
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// FsCheck properties (SC-001/FR-006): over random rule lists × facts, one-in-one-out, no-drop partition,
// the verdict/exit-basis equivalences, and the composition law `evaluateRelease = rollup ∘ evaluate`.

[<Tests>]
let tests =
    testList
        "PropertyTests"
        [ testPropertyWithConfig fsCheckConfig "|evaluate rules facts| = |rules| (one-in-one-out)"
          <| fun (rules: ReleaseRule list) (facts: ReleaseFacts) ->
              (Release.evaluate rules facts).Length = rules.Length

          testPropertyWithConfig fsCheckConfig "|Blockers|+|Warnings|+|Passing| = |findings| (no drop)"
          <| fun (rules: ReleaseRule list) (facts: ReleaseFacts) ->
              let findings = Release.evaluate rules facts
              let d = Release.rollup findings
              d.Blockers.Length + d.Warnings.Length + d.Passing.Length = findings.Length

          testPropertyWithConfig fsCheckConfig "Verdict = Fail ⟺ Blockers ≠ []"
          <| fun (rules: ReleaseRule list) (facts: ReleaseFacts) ->
              let d = Release.evaluateRelease rules facts
              (d.Verdict = Fail) = (not (List.isEmpty d.Blockers))

          testPropertyWithConfig fsCheckConfig "ExitCodeBasis = Blocked ⟺ Verdict = Fail"
          <| fun (rules: ReleaseRule list) (facts: ReleaseFacts) ->
              let d = Release.evaluateRelease rules facts
              (d.ExitCodeBasis = Blocked) = (d.Verdict = Fail)

          testPropertyWithConfig fsCheckConfig "evaluateRelease = rollup ∘ evaluate (composition law)"
          <| fun (rules: ReleaseRule list) (facts: ReleaseFacts) ->
              Release.evaluateRelease rules facts = Release.rollup (Release.evaluate rules facts) ]
