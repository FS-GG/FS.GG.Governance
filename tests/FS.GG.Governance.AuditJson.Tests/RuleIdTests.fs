module FS.GG.Governance.AuditJson.Tests.RuleIdTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// 068 (T008/T011/T014): the additive per-finding `ruleId` on each audit.json enforced item. Every
// decision under test is a REAL `Ship.rollup` output over a real `RouteResult` (Support `decisionOf`);
// the emitted bytes are inspected via JsonDocument. No mocks (Principle V).
//
// SCOPE NOTE on the SC-002/SC-004 rule-hash anchor (contract C4): the projection holds no catalog and
// senses no `RuleHash`, so this suite proves the per-finding `ruleId` SET is profile/mode-invariant and
// that NO finding is dropped (the feature's payload). The run-level catalog `RuleHash` is content-of-
// rule-pack and therefore profile/mode-invariant by construction — a property of the existing
// FreshnessKey/freshness layer, not re-derived here (data-model §5, research D5).

// A finding-bearing route: three gates of varying maturity + two boundary findings (one escalated).
let private ruleIdRoute =
    mkRoute
        [ mkSelectedGate (mkGate (GateId "build:ship") BlockOnShip)
          mkSelectedGate (mkGate (GateId "build:rel") BlockOnRelease)
          mkSelectedGate (mkGate (GateId "docs:lint") Observe) ]
        [ mkFinding
              UnknownProtectedBoundaryPath
              (GovernedPath "src/boundary/Api.fs")
              (ProtectedBoundaryUnknown(SurfaceId "api"))
          mkFinding UnknownGovernedPath (GovernedPath "src/new/Thing.fs") GovernedRootUnknown ]

let private emit (decision: ShipDecision) : string = AuditJson.ofShipDecision decision None []

let private allItems (doc: System.Text.Json.JsonDocument) =
    section doc "blockers" @ section doc "warnings" @ section doc "passing"

let private ruleIdSet (decision: ShipDecision) : string list =
    use doc = parse (emit decision)
    allItems doc |> List.map (fun i -> strField i "ruleId") |> List.sort

[<Tests>]
let tests =
    testList
        "RuleId"
        [ test "every enforced item carries ruleId as the field right after id (contract C2)" {
              let decision = decisionOf ruleIdRoute Gate Standard
              use doc = parse (emit decision)
              let items = allItems doc
              Expect.isNonEmpty items "the fixture produces enforced items"

              for item in items do
                  let order = fieldOrder item
                  let idIdx = List.findIndex ((=) "id") order
                  let ruleIdx = List.findIndex ((=) "ruleId") order
                  Expect.equal ruleIdx (idIdx + 1) "ruleId is the first field after id"

                  let id = itemId item
                  let ruleId = strField item "ruleId"

                  match itemKind item with
                  | "gate" -> Expect.equal ruleId ("gate:" + id) "gate item → gate:<domain>:<check>"
                  | "finding" ->
                      Expect.equal ruleId ("boundary:" + id) "finding item → boundary:<token>"
                      let pathIdx = List.findIndex ((=) "path") order
                      Expect.isLessThan ruleIdx pathIdx "ruleId precedes path on a finding item"
                  | other -> failtestf "unexpected item kind %s" other
          }

          test "the nested enforcement object is unchanged (six fields, original order)" {
              let decision = decisionOf ruleIdRoute Gate Standard
              use doc = parse (emit decision)

              for item in allItems doc do
                  let names = enforcementFields item |> List.map fst

                  Expect.equal
                      names
                      [ "baseSeverity"; "maturity"; "mode"; "profile"; "effectiveSeverity"; "reason" ]
                      "enforcement field order intact"
          }

          test "two runs over identical inputs are byte-identical (determinism, SC-001, FR-002)" {
              let decision = decisionOf ruleIdRoute Gate Standard
              Expect.equal (emit decision) (emit decision) "deterministic ruleId emission"
          }

          test "no item emits an unattributed: marker; every ruleId is non-empty and source-prefixed (T014, FR-010, SC-006)" {
              let decision = decisionOf ruleIdRoute Gate Standard
              use doc = parse (emit decision)

              for item in allItems doc do
                  let ruleId = strField item "ruleId"
                  Expect.isFalse (ruleId = "") "ruleId is non-empty"
                  Expect.isFalse (ruleId.StartsWith "unattributed:") "no unattributed: marker on the normal path"

                  Expect.isTrue
                      (ruleId.StartsWith "gate:" || ruleId.StartsWith "boundary:")
                      (sprintf "ruleId %s is source-prefixed (gate/boundary)" ruleId)
          }

          test "RuleIdInvariance: the ruleId set is byte-identical across every profile and run mode, no finding dropped (T011, SC-002/SC-004)" {
              let baseline = ruleIdSet (decisionOf ruleIdRoute Gate Standard)
              // five items: three gates + two findings — all present in every cell, only the partition moves.
              Expect.equal (List.length baseline) 5 "all five items present in the baseline cell"

              for mode in allModes do
                  for profile in allProfiles do
                      let actual = ruleIdSet (decisionOf ruleIdRoute mode profile)

                      Expect.equal
                          actual
                          baseline
                          (sprintf "ruleId set invariant under %A/%A (no drop, no change)" mode profile)
          } ]
