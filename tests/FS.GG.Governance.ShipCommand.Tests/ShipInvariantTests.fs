module FS.GG.Governance.ShipCommand.Tests.ShipInvariantTests

open System.Text.Json
open System.Text.Json.Nodes
open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.FreshnessSensing
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// SC-003 (the safety-critical ship invariant, L1) + FR-013 (no derivation): the SAME ShipDecision projected
// with `Some report` vs the pre-wiring `None` differs ONLY in the cache section — the verdict, the
// blockers/warnings/passing partition, every per-item enforcement field, the `ExitCodeBasis`, and the numeric
// exit are value-identical. A `reusable` verdict on a base-blocking gate leaves it a blocker.

let rec private stripCache (node: JsonNode) : unit =
    match node with
    | :? JsonObject as o ->
        o.Remove "cacheEligibility" |> ignore
        o.Remove "cacheEligibilityEvaluated" |> ignore

        for kv in List.ofSeq o do
            match kv.Value with
            | null -> ()
            | v -> stripCache v
    | :? JsonArray as a ->
        for item in a do
            match item with
            | null -> ()
            | v -> stripCache v
    | _ -> ()

let private withoutCache (json: string) : string =
    match JsonNode.Parse json with
    | null -> failwith "audit.json did not parse"
    | node ->
        stripCache node
        node.ToJsonString()

// A base-blocking ShipDecision (src change under gate/standard) + its selected gates + base/head.
let private fixture () =
    let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
    let snap = snapshotOf git defaultOpts
    let candidates = candidatesOf git defaultOpts
    let result, decision = resultAndDecisionOf validCatalog candidates Gate Standard
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    decision, selectedGates, baseHeadOfSnap (Some snap)

[<Tests>]
let tests =
    testList
        "ShipInvariant"
        [ test "SC-003: Some report vs None — every NON-cache byte of audit.json is identical (verdict/partition/enforcement/basis)" {
              let decision, selectedGates, baseHead = fixture ()
              let report = expectedCacheReport selectedGates baseHead
              let withSome = AuditJson.ofShipDecision decision (Some report)
              let withNone = AuditJson.ofShipDecision decision None

              Expect.equal decision.Verdict Fail "the fixture decision is a fail (base-blocking gates)"
              Expect.isNonEmpty decision.Blockers "the base-blocking gates are blockers"
              Expect.notEqual withSome withNone "the cache section IS a real delta"
              Expect.equal (withoutCache withSome) (withoutCache withNone) "every non-cache byte (verdict/partition/enforcement/basis) is identical (SC-003)"
          }

          test "SC-003: a REUSABLE verdict on a base-blocking gate leaves it a blocker (cache never moves the partition)" {
              let decision, selectedGates, baseHead = fixture ()

              // Build a store that EXACTLY matches the first resolved gate ⇒ that gate becomes `reusable`.
              let sensed =
                  match FreshnessSensing.senseFreshness fakeSensor selectedGates baseHead with
                  | Ok s -> s
                  | Error e -> failtestf "%s" e

              let candidates = FreshnessResolution.resolve selectedGates sensed |> FreshnessResolution.entries |> List.choose FreshnessResolution.candidate
              Expect.isNonEmpty candidates "at least one gate resolves"
              let store = EvidenceReuse.record (List.head candidates).Inputs (EvidenceRef "ev-reuse-1") EvidenceReuse.empty
              let report = expectedCacheReportWith fakeSensor store selectedGates baseHead

              let withReusable = AuditJson.ofShipDecision decision (Some report)
              let withNone = AuditJson.ofShipDecision decision None

              Expect.stringContains withReusable "reusable" "the matched gate renders a reusable verdict"
              Expect.stringContains withReusable "ev-reuse-1" "carrying its opaque evidence reference"
              // The verdict/partition/enforcement subtree is byte-identical to None ⇒ the reusable gate is STILL a blocker.
              Expect.equal (withoutCache withReusable) (withoutCache withNone) "a reusable verdict changes NOTHING the merge decision depends on (SC-003)"
          }

          test "SC-003: the numeric exit is unchanged by cache — a base-blocking change still exits 1 (Blocked)" {
              // The command's Emitted maps from decision.ExitCodeBasis, never from cache; a full run with the
              // (Some report) ports exits exactly as the pre-wiring verdict dictated.
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req = requestFor Loop.DefaultRange Loop.Text
              let cap = newCapture ()
              let model = Interpreter.run (fakePorts validCatalog git cap req) req
              Expect.equal model.Exit Loop.Blocked "base-blocking change ⇒ Blocked (cache never participates)"
              Expect.equal (Loop.exitCode model.Exit) 1 "exit code 1, unchanged by the cache wiring"
          }

          test "FR-013: no raw freshness input / hash / freshness key leaks into audit.json (C1, L6)" {
              let decision, selectedGates, baseHead = fixture ()
              let report = expectedCacheReport selectedGates baseHead
              let withSome = AuditJson.ofShipDecision decision (Some report)

              for tok in [ "rule-synthetic"; "gen-synthetic"; "art-synthetic"; "cmd-synthetic" ] do
                  Expect.isFalse (withSome.Contains tok) (sprintf "raw freshness input %s must not appear in audit.json" tok)

              Expect.isFalse (withSome.Contains "generatorVersion") "no raw generatorVersion field"
              Expect.isFalse (withSome.Contains "coveredArtifacts") "no raw coveredArtifacts field"
          } ]
