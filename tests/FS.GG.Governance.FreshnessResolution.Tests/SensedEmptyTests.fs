module FS.GG.Governance.FreshnessResolution.Tests.SensedEmptyTests

open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 2 edge — SENSED-EMPTY vs UNSENSED (FR-003, outcome-contract example D). A gate whose covered set
// was SENSED AS EMPTY (a PRESENT key mapping to `[]`) resolves to `Resolved { CoveredArtifacts = []; … }` — a
// legitimate resolved empty set, never `MissingCoveredArtifacts`. The SAME gate ABSENT from the map is
// `Unresolved [MissingCoveredArtifacts]`. The two are structurally distinct and never conflated.

let private onlyOutcome (gates: Gate list) (s: SensedFacts) : ResolutionOutcome =
    match FreshnessResolution.entries (FreshnessResolution.resolve gates s) with
    | [ e ] -> e.Outcome
    | other -> failwithf "expected exactly one entry, got %d" (List.length other)

[<Tests>]
let tests =
    testList
        "SensedEmpty"
        [ test "present-empty covered key ⇒ Resolved with CoveredArtifacts = [] (a legitimate sensed value)" {
              let presentEmpty =
                  { senseFully gBuildTests with CoveredArtifacts = Map.ofList [ gBuildTests.Id, [] ] }

              match onlyOutcome [ gBuildTests ] presentEmpty with
              | Resolved i -> Expect.equal i.CoveredArtifacts [] "an explicitly-empty sensed set resolves to []"
              | Unresolved facts -> failtestf "present-empty must resolve, got Unresolved %A" facts
          }

          test "absent covered key ⇒ Unresolved [MissingCoveredArtifacts] (not sensed)" {
              let absent =
                  { senseFully gBuildTests with CoveredArtifacts = Map.empty }

              Expect.equal (onlyOutcome [ gBuildTests ] absent) (Unresolved [ MissingCoveredArtifacts ]) "an absent key is unresolved on covered artifacts only"
          }

          test "present-empty and absent are NEVER conflated — one Resolved, one Unresolved for the same gate" {
              let presentEmpty =
                  { senseFully gBuildTests with CoveredArtifacts = Map.ofList [ gBuildTests.Id, [] ] }

              let absent =
                  { senseFully gBuildTests with CoveredArtifacts = Map.empty }

              let oEmpty = onlyOutcome [ gBuildTests ] presentEmpty
              let oAbsent = onlyOutcome [ gBuildTests ] absent
              Expect.isTrue (FreshnessResolution.isResolved oEmpty) "sensed-empty resolves"
              Expect.isFalse (FreshnessResolution.isResolved oAbsent) "unsensed does not resolve"
              Expect.notEqual oEmpty oAbsent "the two outcomes are structurally distinct"
          } ]
