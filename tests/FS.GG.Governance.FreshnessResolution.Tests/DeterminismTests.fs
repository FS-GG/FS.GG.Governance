module FS.GG.Governance.FreshnessResolution.Tests.DeterminismTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 3 (part) — ORDER-INDEPENDENCE + PURITY (SC-005, L-order / L-pure). Two input orders of the same
// gates yield value-equal reports, entries ordered by `gateIdValue` ordinal with the structural tiebreak;
// identical inputs yield a byte-identical report regardless of working directory, wall-clock time, or unrelated
// filesystem state. The core performs no I/O.

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fscheckConfig "order-independence: any permutation (List.rev) yields a byte-identical report (SC-005, L-order)"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              FreshnessResolution.resolve (List.rev gs) s = FreshnessResolution.resolve gs s

          testPropertyWithConfig fscheckConfig "entries are in non-decreasing GateId ordinal order (L-order)"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              FreshnessResolution.entries (FreshnessResolution.resolve gs s)
              |> List.pairwise
              |> List.forall (fun (a, b) -> String.CompareOrdinal(gateIdValue a.Gate, gateIdValue b.Gate) <= 0)

          testPropertyWithConfig fscheckConfig "resolve is referentially stable (SC-005, L-pure)"
          <| fun (gs: Gate list) (s: SensedFacts) -> FreshnessResolution.resolve gs s = FreshnessResolution.resolve gs s

          test "worked example C: repeated resolution over time is stable" {
              let gates = [ gBuildTests; gLintStyle; gDocsCheck ]
              let runs = [ for _ in 1..50 -> FreshnessResolution.resolve gates fullSensed ]
              Expect.equal (runs |> List.distinct |> List.length) 1 "every recomputation is identical"
          }

          test "report is identical across a changed cwd and an unrelated temp file (no I/O, L-pure)" {
              let gates = [ gLintStyle; gBuildTests; gDocsCheck ]
              let expected = FreshnessResolution.resolve gates fullSensed

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore
              let tempFile = Path.Combine(tempDir, "unrelated.tmp")

              try
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "noise")
                  let afterCwdChange = FreshnessResolution.resolve gates fullSensed
                  File.Delete tempFile
                  let afterDelete = FreshnessResolution.resolve gates fullSensed

                  Expect.equal afterCwdChange expected "changing cwd / creating a file must not change the report"
                  Expect.equal afterDelete expected "deleting a file must not change the report"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if Directory.Exists tempDir then Directory.Delete(tempDir, true)
          } ]
