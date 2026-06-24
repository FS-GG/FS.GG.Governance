module FS.GG.Governance.RouteCommand.Tests.CacheInvariantTests

open System.Text.Json
open System.Text.Json.Nodes
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.Findings
open FS.GG.Governance.Gates
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteCommand.Tests.Support

// L1 (information, not verdict) + FR-013 (no derivation) for `fsgg route` (US1 sc.2, FR-008, SC-004): the
// SAME RouteResult projected with `Some report` vs the pre-wiring `None` differs ONLY in the cache section;
// every other field is byte/value-identical and no raw freshness input/hash/key leaks into route.json.

// Recursively drop the F045 cache fields so the rest of the document can be compared.
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
    | null -> failwith "route.json did not parse"
    | node ->
        stripCache node
        node.ToJsonString()

// Build a real RouteResult over the valid catalog + a src change, plus the genuine cache report.
let private fixture () =
    let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
    let snap = snapshotOf git defaultOpts
    let facts = factsOf validCatalog
    let candidates = candidatesOf git defaultOpts
    let report = Routing.route facts candidates
    let registry = Gates.buildRegistry facts
    let findings = Findings.findUnknownGovernedPaths facts report
    let result = Route.select registry report findings
    let selectedGates = result.SelectedGates |> List.map (fun sg -> sg.Gate)
    let cacheReport = expectedCacheReport selectedGates (baseHeadOfSnap (Some snap))
    result, cacheReport

[<Tests>]
let tests =
    testList
        "CacheInvariant"
        [ test "Some report vs None: every NON-cache field of route.json is byte-identical (L1, SC-004)" {
              let result, cacheReport = fixture ()
              let withSome = RouteJson.ofRouteResult result (Some cacheReport) []
              let withNone = RouteJson.ofRouteResult result None []

              Expect.notEqual withSome withNone "the cache section IS a real delta (Some ≠ None)"
              Expect.equal (withoutCache withSome) (withoutCache withNone) "all NON-cache fields are identical — the cache section is the only delta"
          }

          test "the cache section flips evaluated false→true; schemaVersion stays fsgg.route/v2 (L1)" {
              let result, cacheReport = fixture ()
              let withSome = RouteJson.ofRouteResult result (Some cacheReport) []
              let withNone = RouteJson.ofRouteResult result None []

              Expect.stringContains withNone "\"cacheEligibilityEvaluated\":false" "None ⇒ not evaluated"
              Expect.stringContains withSome "\"cacheEligibilityEvaluated\":true" "Some ⇒ evaluated"
              Expect.stringContains withSome "fsgg.route/v2" "schema unchanged (no bump)"
          }

          test "FR-013: no raw freshness input / hash / freshness key leaks into route.json (C1, L6)" {
              let result, cacheReport = fixture ()
              let withSome = RouteJson.ofRouteResult result (Some cacheReport) []

              // The faked sensor's literal digests must NEVER appear — the commands render no raw freshness input.
              for tok in [ "rule-synthetic"; "gen-synthetic"; "art-synthetic"; "cmd-synthetic" ] do
                  Expect.isFalse (withSome.Contains tok) (sprintf "raw freshness input %s must not appear in route.json" tok)

              // No freshness-key fingerprint vocabulary leaks (the embed renders verdicts, not inputs).
              Expect.isFalse (withSome.Contains "generatorVersion") "no raw generatorVersion field"
              Expect.isFalse (withSome.Contains "coveredArtifacts") "no raw coveredArtifacts field"
          } ]
