module FS.GG.Governance.PackEvidence.Tests.FactContributionsTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.PackEvidence.Tests.Support

// SC-001 / FR-003: factContributions derives the Met/Unmet contributions for the EXISTING F53
// VersionBump/PackageMetadata/Provenance families — no new family — and composes with the REAL
// Release.evaluateRelease so an unbumped/failed project yields a BLOCKED decision.

let private bs = baselines [ "A", "1.0.0"; "B", "1.0.0" ]

/// A blocking-at-release rule per family (the F53 declaration that turns an Unmet fact into a blocker).
let private blockingRule kind : ReleaseRule =
    { Kind = kind
      Surface = SurfaceId "release"
      BaseSeverity = Blocking
      Maturity = BlockOnRelease }

let private allFamilies =
    [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]

/// Sensed facts with every family Met, then merged with the pack contributions (packed evidence wins).
let private mergedFacts (set: PackEvidenceSet) : ReleaseFacts =
    let baseStates = allFamilies |> List.map (fun k -> k, Met) |> Map.ofList
    let merged =
        Pack.factContributions set
        |> Map.fold (fun (acc: Map<ReleaseRuleKind, FactState>) k v -> Map.add k v acc) baseStates
    { States = merged }

let private decisionFor (outcomes: PackOutcome list) =
    let set = Pack.evaluatePack bs outcomes
    let rules = allFamilies |> List.map blockingRule
    Release.evaluateRelease rules (mergedFacts set)

[<Tests>]
let tests =
    testList
        "factContributions"
        [ test "all projects Packed+Bumped ⇒ the three families Met" {
              let set =
                  Pack.evaluatePack bs [ packed "A" "a.nupkg" "1.1.0" "dA"; packed "B" "b.nupkg" "1.1.0" "dB" ]

              let c = Pack.factContributions set
              Expect.equal (Map.tryFind VersionBump c) (Some Met) ""
              Expect.equal (Map.tryFind PackageMetadata c) (Some Met) ""
              Expect.equal (Map.tryFind Provenance c) (Some Met) ""
          }

          test "an Unbumped project ⇒ VersionBump Unmet (metadata/provenance stay Met)" {
              let set = Pack.evaluatePack bs [ packed "A" "a.nupkg" "1.0.0" "dA" ]
              let c = Pack.factContributions set
              Expect.equal (Map.tryFind VersionBump c) (Some Unmet) "unbumped blocks VersionBump"
              Expect.equal (Map.tryFind PackageMetadata c) (Some Met) "artifact present ⇒ metadata met"
          }

          test "a PackFailed ⇒ all three families Unmet (no artifact)" {
              let set = Pack.evaluatePack bs [ packFailed "A" 1 ]
              let c = Pack.factContributions set
              Expect.equal (Map.tryFind VersionBump c) (Some Unmet) ""
              Expect.equal (Map.tryFind PackageMetadata c) (Some Unmet) ""
              Expect.equal (Map.tryFind Provenance c) (Some Unmet) ""
          }

          test "NoPackableProjects ⇒ Map.empty (vacuously satisfied — nothing blocks on packing)" {
              let set = Pack.evaluatePack bs []
              Expect.isTrue (Map.isEmpty (Pack.factContributions set)) ""
          }

          test "composes with REAL evaluateRelease: all bumped ⇒ Pass" {
              let d = decisionFor [ packed "A" "a.nupkg" "1.1.0" "dA"; packed "B" "b.nupkg" "1.1.0" "dB" ]
              Expect.equal d.Verdict Pass "a fully-bumped product is not blocked on packing"
          }

          test "composes with REAL evaluateRelease: an unbumped project ⇒ Fail (blocked)" {
              let d = decisionFor [ packed "A" "a.nupkg" "1.0.0" "dA" ]
              Expect.equal d.Verdict Fail "an unbumped pack blocks the release"
              Expect.isNonEmpty d.Blockers "the VersionBump family is a blocker"
          }

          test "composes with REAL evaluateRelease: a failed pack ⇒ Fail (blocked)" {
              let d = decisionFor [ packFailed "A" 137 ]
              Expect.equal d.Verdict Fail "a failed pack blocks the release"
          } ]
