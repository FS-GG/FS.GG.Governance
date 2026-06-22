module FS.GG.Governance.CacheEligibilityCommand.Tests.ExitInformationTests

open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T025 (FR-009, SC-006, L8/L10) — cache eligibility is INFORMATION: exit 0 when every gate must recompute
// AND when some gates are unresolved. The command assigns NO ship/severity/profile/mode/enforcement/
// provenance, and writes nothing toward route.json/audit.json.

let private git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

let private run sensor store =
    let req = requestFor Loop.DefaultRange Loop.Human
    let cap = newCapture ()
    let model = Interpreter.run (fakePorts validCatalog git sensor store cap req) req
    cap, model

[<Tests>]
let tests =
    testList
        "ExitInformation"
        [ test "all-must-recompute (empty store) ⇒ exit 0 (SC-006)" {
              let _, model = run fixedSensor (storeReaderOf (Ok None))
              Expect.equal model.Exit Loop.Success "must-recompute is information, not failure"
          }

          test "some-unresolved (a fact unsensed) ⇒ exit 0 (SC-006)" {
              let _, model = run sensorNoCovered (storeReaderOf (Ok None))
              Expect.equal model.Exit Loop.Success "unresolved is information, not failure"
          }

          test "no ship/verdict/severity vocabulary leaks into the artifacts or summary (L10)" {
              let cap, _ = run fixedSensor (storeReaderOf (Ok None))
              let cache = writtenOf cap Loop.CacheArtifact |> Option.map snd |> Option.defaultValue ""
              let sidecar = writtenOf cap Loop.UnresolvedArtifact |> Option.map snd |> Option.defaultValue ""
              let summary = String.concat "\n" cap.Emits
              let blob = (cache + "\n" + sidecar + "\n" + summary).ToLowerInvariant()

              for token in [ "severity"; "profile"; "enforcement"; "shipverdict"; "blockers"; "provenance"; "audit.json"; "route.json" ] do
                  Expect.isFalse (blob.Contains token) (sprintf "excluded token '%s' must not appear" token)
          }

          test "only the two cache documents are written — nothing toward route/audit (SC-008)" {
              let cap, _ = run fixedSensor (storeReaderOf (Ok None))
              let kinds = cap.Writes |> List.map (fun (k, _, _) -> k) |> List.distinct |> List.sort
              Expect.equal kinds [ Loop.CacheArtifact; Loop.UnresolvedArtifact ] "exactly the two cache artifacts, nothing else"
          } ]
