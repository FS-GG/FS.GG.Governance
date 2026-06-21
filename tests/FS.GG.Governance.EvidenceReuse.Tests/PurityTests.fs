module FS.GG.Governance.EvidenceReuse.Tests.PurityTests

open System.IO
open Expecto
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuse.Tests.Support

// Purity (SC-006, FR-009): a fixed `decide` / `record` result is identical regardless of working directory,
// wall-clock time, or unrelated filesystem state. The core reads no clock/cwd/filesystem/git/network.

[<Tests>]
let tests =
    testList
        "Purity"
        [ test "decide / record are identical across a changed cwd and an unrelated temp file (SC-006)" {
              let candidate = { baseInputs with RuleHash = RuleHash "r2" }
              let store = storeOf [ baseInputs, E1 ]
              let expectedDecision = EvidenceReuse.decide candidate store
              let expectedEntries = EvidenceReuse.entries (EvidenceReuse.record candidate E2 store)

              let originalCwd = Directory.GetCurrentDirectory()
              let tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tempDir |> ignore
              let tempFile = Path.Combine(tempDir, "unrelated.tmp")

              try
                  Directory.SetCurrentDirectory tempDir
                  File.WriteAllText(tempFile, "noise")
                  let afterCwdChange = EvidenceReuse.decide candidate store
                  let afterCwdEntries = EvidenceReuse.entries (EvidenceReuse.record candidate E2 store)

                  File.Delete tempFile
                  let afterDelete = EvidenceReuse.decide candidate store

                  Expect.equal afterCwdChange expectedDecision "changing cwd / creating a file must not change the decision"
                  Expect.equal afterCwdEntries expectedEntries "changing cwd / creating a file must not change the record result"
                  Expect.equal afterDelete expectedDecision "deleting a file must not change the decision"
              finally
                  Directory.SetCurrentDirectory originalCwd
                  if Directory.Exists tempDir then Directory.Delete(tempDir, true)
          }

          test "repeated decision over time is stable (SC-006)" {
              let candidate = { baseInputs with Head = Revision "zzz" }
              let store = storeOf [ baseInputs, E1 ]
              let runs = [ for _ in 1..50 -> EvidenceReuse.decide candidate store ]
              Expect.equal (runs |> List.distinct |> List.length) 1 "every recomputation is identical"
          } ]
