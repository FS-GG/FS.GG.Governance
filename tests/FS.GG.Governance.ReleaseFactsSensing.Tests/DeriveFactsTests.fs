module FS.GG.Governance.ReleaseFactsSensing.Tests.DeriveFactsTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// US1 (acc. 1–2, FR-001/FR-003/FR-009): the PURE `deriveFacts` over hand-built `RecoveredEvidence` (no disk)
// classifies exactly one `FactState` per family and always returns all six.

let private statesOf (r: SensedRelease) = r.Facts.States

[<Tests>]
let tests =
    testList
        "DeriveFactsTests"
        [ test "releaseFamilies is exactly the six kinds in declaration order, all distinct" {
              Expect.equal
                  Sensing.releaseFamilies
                  [ VersionBump; PackageMetadata; TemplatePins; PublishPlan; TrustedPublishing; Provenance ]
                  "releaseFamilies must be the six ReleaseRuleKind in declaration order"

              Expect.equal Sensing.releaseFamilies.Length 6 "exactly six families"

              Expect.equal
                  (Sensing.releaseFamilies |> List.distinct |> List.length)
                  6
                  "all six families distinct"
          }

          test "all-satisfying evidence ⇒ all six families Met, exactly six states" {
              let sensed = Sensing.deriveFacts expectations recoveredMet
              let states = statesOf sensed

              Expect.equal states.Count 6 "exactly six families (FR-009)"
              Expect.isTrue (states |> Map.forall (fun _ s -> s = Met)) "every family Met"
          }

          test "version not bumped past baseline ⇒ VersionBump Unmet, the other five Met" {
              // Declared equals the baseline ⇒ not strictly bumped past (US1.2 / SC-005).
              let recovered = { recoveredMet with Version = Ok { Declared = "1.2.0" } }
              let states = statesOf (Sensing.deriveFacts expectations recovered)

              Expect.equal states.Count 6 "still six families"
              Expect.equal states.[VersionBump] Unmet "VersionBump Unmet (equal version is not a bump)"

              for k in allFamilies |> List.filter (fun k -> k <> VersionBump) do
                  Expect.equal states.[k] Met (sprintf "%A still Met" k)
          }

          test "all-violating evidence ⇒ every family Unmet" {
              let states = statesOf (Sensing.deriveFacts expectations recoveredAllUnmet)

              Expect.equal states.Count 6 "exactly six families"
              Expect.isTrue (states |> Map.forall (fun _ s -> s = Unmet)) "every family Unmet (recovered-but-unsatisfied)"
          } ]
