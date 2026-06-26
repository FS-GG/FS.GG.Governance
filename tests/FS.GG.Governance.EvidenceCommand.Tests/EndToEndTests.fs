module FS.GG.Governance.EvidenceCommand.Tests.EndToEndTests

open System.IO
open System.Text.Json
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceCommand
open FS.GG.Governance.EvidenceCommand.Tests.Support

// US1/US3 — the REAL interpreter (realPorts) over the real `tests/golden-fixture/` tree writes a deterministic,
// versioned, well-formed document; a second run is byte-identical (SC-002). A malformed-graph report (fed
// through the real interpreter + real projection) writes a named graphFailure with no per-node map (SC-003).

let private runReal (out: string) =
    let request: Loop.RunRequest =
        { Repo = goldenFixture
          Out = out
          Format = Loop.Json
          ExplicitPlain = false }

    Interpreter.run (Interpreter.realPorts goldenFixture) request

[<Tests>]
let tests =
    testList
        "EndToEnd"
        [ test "realPorts over golden-fixture writes a versioned, well-formed evidence.json" {
              let out = tempOut "e2e"
              let model = runReal out

              Expect.equal model.Exit Loop.Success "operational success"
              Expect.isTrue (File.Exists out) "artifact written"

              let root = JsonDocument.Parse(File.ReadAllText out).RootElement
              Expect.equal (root.GetProperty("schemaVersion").GetString()) "fsgg.evidence/v1" "versioned"
              Expect.equal (root.GetProperty("graphFailure").ValueKind) JsonValueKind.Null "well-formed (no graph failure)"
              Expect.isGreaterThan (root.GetProperty("nodes").GetArrayLength()) 0 "the evidence world is non-empty"

              // SC-004: each node's effective state is the closure result the host computed — for this fixture
              // every node is well-formed and self-consistent (declared present, effective present).
              for n in root.GetProperty("nodes").EnumerateArray() do
                  Expect.isTrue (n.TryGetProperty("declared") |> fst) "declared present"
                  Expect.isTrue (n.TryGetProperty("effective") |> fst) "effective present"
          }

          test "a second real run is byte-identical to the first (SC-002)" {
              let out1 = tempOut "e2e-a"
              let out2 = tempOut "e2e-b"
              runReal out1 |> ignore
              runReal out2 |> ignore
              Expect.equal (File.ReadAllText out1) (File.ReadAllText out2) "byte-identical re-run"
          }

          test "a malformed-graph report writes a named graphFailure and NO per-node map (SC-003)" {
              // SYNTHETIC: a hand-built cyclic report drives the real interpreter + real projection; the real
              // golden-fixture graph is well-formed, so the malformed path is exercised with a disclosed input.
              let cyclic = report [ reportNode "a" Real Real None "speckit" ] [ "a", "a" ]
              let out = tempOut "e2e-malformed"
              let ports, _ = fakePorts (Ok cyclic)

              let request: Loop.RunRequest =
                  { Repo = "."; Out = out; Format = Loop.Json; ExplicitPlain = false }

              // Use the real Write/Out via realPorts but the faked SenseReport: compose a hybrid that writes for real.
              let realWrite = Interpreter.realPorts "."
              let hybrid: Interpreter.Ports = { ports with Write = realWrite.Write }
              let model = Interpreter.run hybrid request

              Expect.equal model.Exit Loop.Success "operational success even for a malformed graph"
              let root = JsonDocument.Parse(File.ReadAllText out).RootElement
              Expect.equal (root.GetProperty("graphFailure").GetProperty("kind").GetString()) "cycle" "named cycle failure"
              Expect.isFalse (root.TryGetProperty("nodes") |> fst) "no per-node map on a malformed graph"
          } ]
