module FS.GG.Governance.FreshnessResolution.Tests.CompletenessTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model
open FS.GG.Governance.FreshnessResolution.Tests.Support

// User Story 3 (part) — ATTRIBUTION + COMPLETENESS (SC-006, L-attribute / L-complete). For N supplied gates the
// report has exactly N entries, each carrying its originating `GateId`; no gate dropped, merged, or
// deduplicated; duplicate `GateId`s preserved as separate entries, deterministically ordered by the structural
// tiebreak — a genuine total order, not input-order-stable happenstance.

[<Tests>]
let tests =
    testList
        "Completeness"
        [ testPropertyWithConfig fscheckConfig "exactly one entry per input gate; the GateId multiset equals the input multiset (L-complete)"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              let es = FreshnessResolution.entries (FreshnessResolution.resolve gs s)
              List.length es = List.length gs
              && (es |> List.map (fun e -> e.Gate) |> List.sort) = (gs |> List.map (fun g -> g.Id) |> List.sort)

          test "worked example E: two gates sharing a GateId ⇒ TWO entries, neither merged nor dropped" {
              // Same id build:tests, different commands ⇒ distinct gates, but both attributed to build:tests.
              let g1 = gateWith "build" "tests" Medium Ci (Some dotnetCmd)
              let g2 = gateWith "build" "tests" High Local (Some eslintCmd)
              let es = FreshnessResolution.entries (FreshnessResolution.resolve [ g1; g2 ] fullSensed)
              Expect.equal (List.length es) 2 "two same-id gates ⇒ two entries"
              Expect.allEqual (es |> List.map (fun e -> e.Gate)) (gid "build" "tests") "both entries attributed to the shared GateId"
          }

          test "tiebreak is a total order: same GateId, DISTINCT outcomes ⇒ byte-identical report for either input order" {
              // Both gates are build:tests; one resolves (dotnet sensed), one does not (unknown command unsensed).
              let resolvedGate = gateWith "build" "tests" Medium Ci (Some dotnetCmd)
              let unresolvedGate = gateWith "build" "tests" Medium Ci (Some(CommandId "absent-cmd"))
              let a = FreshnessResolution.resolve [ resolvedGate; unresolvedGate ] fullSensed
              let b = FreshnessResolution.resolve [ unresolvedGate; resolvedGate ] fullSensed
              Expect.equal b a "the structural tiebreak orders same-id distinct-outcome entries deterministically"
              // Sanity: the two outcomes really are distinct (one Resolved, one Unresolved).
              let outcomes = FreshnessResolution.entries a |> List.map (fun e -> FreshnessResolution.isResolved e.Outcome)
              Expect.equal (List.sort outcomes) [ false; true ] "one Resolved, one Unresolved under the shared gate"
          }

          testPropertyWithConfig fscheckConfig "duplicate-inducing inputs are never deduplicated — entry count always equals input count"
          <| fun (gs: Gate list) (s: SensedFacts) ->
              List.length (FreshnessResolution.entries (FreshnessResolution.resolve gs s)) = List.length gs ]
