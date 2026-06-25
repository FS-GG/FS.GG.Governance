module FS.GG.Governance.PackEvidence.Tests.EvaluatePackTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.PackEvidence.Tests.Support

// SC-001 / SC-008: evaluatePack builds the deterministic PackEvidenceSet — one PackVerdict per packable
// project (sorted by (SurfaceId, ArtifactPath)), every recorded Pack run present in Runs (a failed pack never
// dropped), empty outcomes ⇒ NoPackableProjects = true.

let private bs = baselines [ "A", "1.0.0"; "B", "1.0.0" ]

[<Tests>]
let tests =
    testList
        "evaluatePack"
        [ test "one PackVerdict per outcome, sorted by (SurfaceId, ArtifactPath)" {
              let outcomes =
                  [ packed "B" "out/B.nupkg" "1.1.0" "dB"
                    packed "A" "out/A.nupkg" "1.1.0" "dA" ]

              let result = Pack.evaluatePack bs outcomes
              let surfaces = result.Verdicts |> List.map (fun v -> let (SurfaceId s) = v.Surface in s)
              Expect.equal surfaces [ "A"; "B" ] "verdicts sorted by surface id"
              Expect.equal (List.length result.Verdicts) 2 "one per packable project"
          }

          test "empty outcomes ⇒ NoPackableProjects = true, no verdicts, no runs (vacuously satisfied)" {
              let result = Pack.evaluatePack bs []
              Expect.isTrue result.NoPackableProjects ""
              Expect.isEmpty result.Verdicts ""
              Expect.isEmpty result.Runs ""
          }

          test "a failed pack's run is present in Runs with its sentinel exit — never dropped" {
              let outcomes = [ packed "A" "out/A.nupkg" "1.1.0" "dA"; packFailed "B" 137 ]
              let result = Pack.evaluatePack bs outcomes
              Expect.equal (List.length result.Runs) 2 "both runs recorded"
              // the failed run carries exit code 137
              let exits =
                  result.Runs
                  |> List.map (fun r -> let (FS.GG.Governance.CommandRecord.Model.ExitCode c) = r.Record.Reproducible.ExitCode in c)
              Expect.contains exits 137 "the sentinel exit is retained"
          }

          test "Runs preserve carried order (order-significant for the snapshot)" {
              let outcomes = [ packFailed "B" 1; packed "A" "out/A.nupkg" "1.1.0" "dA" ]
              let result = Pack.evaluatePack bs outcomes
              let exits =
                  result.Runs
                  |> List.map (fun r -> let (FS.GG.Governance.CommandRecord.Model.ExitCode c) = r.Record.Reproducible.ExitCode in c)
              Expect.equal exits [ 1; 0 ] "runs in carried order, NOT verdict order"
          }

          test "PackedNoArtifact (NoArtifactEmitted) surfaces the closed reason, never a fabricated artifact" {
              let result = Pack.evaluatePack bs [ packedNoArtifact "A" NoArtifactEmitted ]
              let v = List.head result.Verdicts
              Expect.equal v.Version NotPackable "no artifact ⇒ not packable"
              Expect.stringContains v.Reason "no artifact emitted" "reason names the input signal"
          }

          test "ArtifactUnreadable carries its message in the reason" {
              let result = Pack.evaluatePack bs [ packedNoArtifact "A" (ArtifactUnreadable "bad zip") ]
              let v = List.head result.Verdicts
              Expect.stringContains v.Reason "bad zip" "reason carries the unreadable message"
          }

          test "reorder-invariant: reordering outcomes yields byte-identical Verdicts" {
              let a = packed "A" "out/A.nupkg" "1.1.0" "dA"
              let b = packed "B" "out/B.nupkg" "1.2.0" "dB"
              let r1 = Pack.evaluatePack bs [ a; b ]
              let r2 = Pack.evaluatePack bs [ b; a ]
              Expect.equal r1.Verdicts r2.Verdicts "verdicts are reorder-invariant"
          }

          test "the reason names the project and version basis" {
              let result = Pack.evaluatePack bs [ packed "A" "out/A.nupkg" "1.1.0" "dA" ]
              let v = List.head result.Verdicts
              Expect.stringContains v.Reason "A" "names the project"
              Expect.stringContains v.Reason "1.1.0" "names the packed version"
          } ]
