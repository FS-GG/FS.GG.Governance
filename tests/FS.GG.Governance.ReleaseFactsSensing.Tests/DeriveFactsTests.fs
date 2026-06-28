module FS.GG.Governance.ReleaseFactsSensing.Tests.DeriveFactsTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// US1 (acc. 1–2, FR-001/FR-003/FR-009): the PURE `deriveFacts` over hand-built `RecoveredEvidence` (no disk)
// classifies exactly one `FactState` per family and always returns all seven. 088: ApiCompatibility is
// host-overlaid (cross-package, mirroring the F065 pack join), so deriveFacts emits it Unrecoverable.

let private statesOf (r: SensedRelease) = r.Facts.States

[<Tests>]
let tests =
    testList
        "DeriveFactsTests"
        [ test "releaseFamilies is exactly the seven kinds in declaration order, all distinct" {
              Expect.equal
                  Sensing.releaseFamilies
                  [ VersionBump
                    PackageMetadata
                    TemplatePins
                    PublishPlan
                    TrustedPublishing
                    Provenance
                    ApiCompatibility ]
                  "releaseFamilies must be the seven ReleaseRuleKind in declaration order"

              Expect.equal Sensing.releaseFamilies.Length 7 "exactly seven families"

              Expect.equal
                  (Sensing.releaseFamilies |> List.distinct |> List.length)
                  7
                  "all seven families distinct"
          }

          test "all-satisfying evidence ⇒ the six repo-sensed Met, ApiCompatibility Unrecoverable (host-overlaid)" {
              let sensed = Sensing.deriveFacts expectations recoveredMet
              let states = statesOf sensed

              Expect.equal states.Count 7 "exactly seven families (FR-009)"
              Expect.equal states.[ApiCompatibility] Unrecoverable "ApiCompatibility host-overlaid ⇒ Unrecoverable here"

              for k in allFamilies do
                  Expect.equal states.[k] Met (sprintf "%A Met" k)
          }

          test "version not bumped past baseline ⇒ VersionBump Unmet, the other repo-sensed Met" {
              // Declared equals the baseline ⇒ not strictly bumped past (US1.2 / SC-005).
              let recovered = { recoveredMet with Version = Ok { Declared = "1.2.0" } }
              let states = statesOf (Sensing.deriveFacts expectations recovered)

              Expect.equal states.Count 7 "still seven families"
              Expect.equal states.[VersionBump] Unmet "VersionBump Unmet (equal version is not a bump)"
              Expect.equal states.[ApiCompatibility] Unrecoverable "ApiCompatibility host-overlaid"

              for k in allFamilies |> List.filter (fun k -> k <> VersionBump) do
                  Expect.equal states.[k] Met (sprintf "%A still Met" k)
          }

          test "all-violating evidence ⇒ every repo-sensed family Unmet (ApiCompatibility Unrecoverable)" {
              let states = statesOf (Sensing.deriveFacts expectations recoveredAllUnmet)

              Expect.equal states.Count 7 "exactly seven families"
              Expect.equal states.[ApiCompatibility] Unrecoverable "ApiCompatibility host-overlaid"

              for k in allFamilies do
                  Expect.equal states.[k] Unmet (sprintf "%A Unmet (recovered-but-unsatisfied)" k)
          } ]
