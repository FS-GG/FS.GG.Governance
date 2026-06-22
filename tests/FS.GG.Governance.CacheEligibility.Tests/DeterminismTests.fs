module FS.GG.Governance.CacheEligibility.Tests.DeterminismTests

open System.IO
open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CacheEligibility.Tests.Support

// User Story 3 (part) — determinism / purity (SC-005, L-T2/L-E6). Equal candidates + store ⇒ byte-identical
// report regardless of working directory, wall-clock time, or unrelated filesystem state. The core reads no
// clock/cwd/filesystem/git/environment/network and computes no freshness key.

[<Tests>]
let tests =
    testList
        "Determinism"
        [ testPropertyWithConfig fscheckConfig "evaluate / evaluateGate are referentially stable (SC-005, L-T2)"
          <| fun (cs: CandidateGate list) (c: CandidateGate) (s: ReuseStore) ->
              CacheEligibility.evaluate cs s = CacheEligibility.evaluate cs s
              && CacheEligibility.evaluateGate c s = CacheEligibility.evaluateGate c s

          test "repeated evaluation over time is stable (SC-005)" {
              let cs = [ candidate (gid "a" "a") baseInputs; candidate (gid "b" "b") { baseInputs with RuleHash = RuleHash "r2" } ]
              let store = storeOf [ baseInputs, refA ]
              let runs = [ for _ in 1..50 -> CacheEligibility.evaluate cs store ]
              Expect.equal (runs |> List.distinct |> List.length) 1 "every recomputation is identical"
          }

          test "report is identical across a changed cwd and an unrelated temp file (SC-005, L-E6)" {
              let cs =
                  [ candidate (gid "z" "a") baseInputs
                    candidate (gid "a" "a") { baseInputs with Head = Revision "zzz" }
                    candidate (gid "a" "a") baseInputs ]
              let store = storeOf [ baseInputs, refA ]
              let expected = CacheEligibility.evaluate cs store

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore
              let tempFile = Path.Combine(tempDir, "unrelated.tmp")

              try
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "noise")
                  let afterCwdChange = CacheEligibility.evaluate cs store
                  File.Delete tempFile
                  let afterDelete = CacheEligibility.evaluate cs store

                  Expect.equal afterCwdChange expected "changing cwd / creating a file must not change the report"
                  Expect.equal afterDelete expected "deleting a file must not change the report"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if Directory.Exists tempDir then Directory.Delete(tempDir, true)
          } ]
