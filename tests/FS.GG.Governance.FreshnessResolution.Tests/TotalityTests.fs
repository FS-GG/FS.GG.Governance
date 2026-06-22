module FS.GG.Governance.FreshnessResolution.Tests.TotalityTests

open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 3 (part) — TOTALITY (SC-004, L-total). `resolve` returns a well-formed report and NEVER throws over
// the full cross-product of gate counts (zero / one / many, incl. duplicates) × sensed-facts states (all
// present / partial / all absent). The empty report is a valid success; a gate missing EVERY required fact
// yields one well-formed `Unresolved` entry naming all gaps, never a dropped gate.

/// A bundle that senses NOTHING — every repo-wide fact absent, no covered keys, no command versions.
let private emptySensed: SensedFacts =
    { RuleHash = None
      GeneratorVersion = None
      Base = None
      Head = None
      CoveredArtifacts = Map.empty
      CommandVersions = Map.empty }

[<Tests>]
let tests =
    testList
        "Totality"
        [ testPropertyWithConfig fscheckConfig "resolve returns a well-formed report and never throws over the full cross-product (SC-004, L-total)"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              let es = FreshnessResolution.entries (FreshnessResolution.resolve gs s)
              // Forcing the entries (count + each outcome classified) proves total evaluation without throwing.
              List.length es = List.length gs
              && es |> List.forall (fun e -> FreshnessResolution.isResolved e.Outcome || not (List.isEmpty (FreshnessResolution.missingFacts e.Outcome)))

          test "empty gate list ⇒ empty report, a valid success not an error (Edge)" {
              Expect.equal (FreshnessResolution.resolve [] fullSensed) (FreshnessResolutionReport []) "resolve [] sensed = FreshnessResolutionReport []"
              Expect.equal (FreshnessResolution.entries (FreshnessResolution.resolve [] emptySensed)) [] "entries of the empty report is []"
          }

          test "a gate missing EVERY required fact ⇒ one Unresolved entry naming all six gaps (no dropped gate)" {
              match FreshnessResolution.entries (FreshnessResolution.resolve [ gBuildTests ] emptySensed) with
              | [ e ] ->
                  Expect.equal
                      (FreshnessResolution.missingFacts e.Outcome)
                      [ MissingRuleHash
                        MissingCoveredArtifacts
                        MissingCommandVersion
                        MissingGeneratorVersion
                        MissingBaseRevision
                        MissingHeadRevision ]
                      "all six gaps named for a command-bearing gate sensed against nothing"
              | other -> failtestf "expected exactly one entry, got %d" (List.length other)
          }

          test "single gate path equals the many-gate path (one gate ⇒ one-entry report)" {
              let one = FreshnessResolution.entries (FreshnessResolution.resolve [ gBuildTests ] fullSensed)
              Expect.equal (List.length one) 1 "one gate ⇒ one entry"
          } ]
