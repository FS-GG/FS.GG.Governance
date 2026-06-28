module FS.GG.Governance.PackEvidence.Tests.ReuseGuardTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Tests.Support

// FR-003 / D5: the no-new-family guard. factContributions feeds ONLY the existing F53
// VersionBump/PackageMetadata/Provenance families — it never invents a new ReleaseRuleKind. A future
// ReleaseRuleKind case is a compile error in the exhaustive match below, never a silent new key.

let private bs = baselines [ "A", "1.0.0" ]

/// Exhaustive over the closed F53 ReleaseRuleKind — a new case is a compile error here (the guard).
let private isKnownFamily kind =
    match kind with
    | VersionBump
    | PackageMetadata
    | TemplatePins
    | PublishPlan
    | TrustedPublishing
    | Provenance
    | ApiCompatibility -> true

[<Tests>]
let tests =
    testList
        "reuse-guard"
        [ test "factContributions keys ⊆ { VersionBump; PackageMetadata; Provenance } — no new family" {
              let set = Pack.evaluatePack bs [ packed "A" "a.nupkg" "1.1.0" "dA"; packFailed "A" 1 ]

              let keys = Pack.factContributions set |> Map.toList |> List.map fst

              Expect.all keys isKnownFamily "every key is a known F53 family"

              Expect.all
                  keys
                  (fun k -> List.contains k [ VersionBump; PackageMetadata; Provenance ])
                  "pack evidence touches only the three packed-grounded families"
          } ]
