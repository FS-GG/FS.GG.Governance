module FS.GG.Governance.EvidenceReuseStore.Tests.TotalityTests

open System.Text.Json
open Expecto
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.EvidenceReuseStore
open FS.GG.Governance.EvidenceReuseStore.Tests.Support

// SC-008, FR-009: every operation returns a value and NEVER throws over arbitrary well-typed stores and any
// `maxEntries` (incl. 0 and negatives), with no filesystem/clock/network access in the operations themselves
// (the round-trip's temp file is isolated to RoundTripTests/Support — these operations touch nothing).

[<Tests>]
let tests =
    testList
        "Totality"
        [ testPropertyWithConfig fscheckConfig "serialise always returns a parseable fsgg.evidence-reuse-store/v1 document" (fun (store: ReuseStore) ->
              let text = EvidenceReuseStore.serialise store
              use doc = JsonDocument.Parse text
              doc.RootElement.GetProperty("schemaVersion").GetString() = EvidenceReuseStore.schemaVersion
              && doc.RootElement.GetProperty("recorded").ValueKind = JsonValueKind.Array)

          testPropertyWithConfig fscheckConfig "retain always returns a well-formed ReuseStore, never throws (any n incl. negatives)" (fun (store: ReuseStore) (n: int) ->
              let (ReuseStore es) = EvidenceReuseStore.retain n store
              // value returned; length within the requested bound
              List.length es <= max 0 n)

          testPropertyWithConfig fscheckConfig "prune always returns a well-formed ReuseStore, never throws" (fun (store: ReuseStore) ->
              let (ReuseStore es) = EvidenceReuseStore.prune store
              // value returned; never larger than the input
              List.length es <= (EvidenceReuse.entries store |> List.length))

          test "operations are total over the empty / singleton edges" {
              Expect.equal (EvidenceReuseStore.serialise EvidenceReuse.empty) """{"schemaVersion":"fsgg.evidence-reuse-store/v1","recorded":[]}""" "empty serialises"
              Expect.equal (EvidenceReuseStore.retain 0 EvidenceReuse.empty) EvidenceReuse.empty "empty retains"
              Expect.equal (EvidenceReuseStore.prune EvidenceReuse.empty) EvidenceReuse.empty "empty prunes"
          } ]
