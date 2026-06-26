module FS.GG.Governance.RouteJson.Tests.RuleIdTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteJson
open FS.GG.Governance.RouteJson.Tests.Support

// 068 (T010/T012/T014): the additive per-finding `ruleId` on route.json selected gates and boundary
// findings. Every `RouteResult` under test is the genuine F015->F017->F018->F019 chain output (Support
// `resultOf`); the emitted bytes are inspected via JsonDocument. No mocks (Principle V).

// A real fixture: build/docs gates selected by routed paths + one unclassified in-root path → one finding.
let private fixtureFacts: TypedFacts =
    facts
        "src"
        [ "src/build/**", "build"; "src/docs/**", "docs" ]
        [ surface GovernedRoot "root" [ "src" ] ]
        [ check "build" "tests" (Some "dotnet-test") Medium Local BlockOnShip
          check "docs" "lint" None Cheap Local Warn ]
        [ command "dotnet-test" 600 ]

let private fixtureResult =
    resultOf fixtureFacts [ "src/build/a.fs"; "src/docs/Guide.md"; "src/loose/x.fs" ]

let private emit (result: RouteResult) : string = RouteJson.ofRouteResult result None []

[<Tests>]
let tests =
    testList
        "RuleId"
        [ test "every selected gate carries ruleId (gate:<domain>:<check>) as the field right after id (C2)" {
              use doc = parse (emit fixtureResult)
              let gates = selectedGates doc
              Expect.isNonEmpty gates "the fixture selects gates"

              for g in gates do
                  let order = fieldOrder g
                  Expect.equal (List.findIndex ((=) "ruleId") order) (List.findIndex ((=) "id") order + 1) "ruleId after id"
                  Expect.equal (strField g "ruleId") ("gate:" + strField g "id") "gate ruleId = gate:<id>"
          }

          test "Boundary: every finding carries ruleId (boundary:<token>) after id and before path, distinguishable from gate ids (T014, FR-008, SC-006)" {
              use doc = parse (emit fixtureResult)
              let fs = findings doc
              Expect.isNonEmpty fs "the fixture produces a boundary finding"

              let gateRuleIds = selectedGates doc |> List.map (fun g -> strField g "ruleId") |> Set.ofList

              for f in fs do
                  let order = fieldOrder f
                  let ruleIdx = List.findIndex ((=) "ruleId") order
                  Expect.equal ruleIdx (List.findIndex ((=) "id") order + 1) "ruleId after id"
                  Expect.isLessThan ruleIdx (List.findIndex ((=) "path") order) "ruleId before path"

                  let ruleId = strField f "ruleId"
                  Expect.equal ruleId ("boundary:" + strField f "id") "finding ruleId = boundary:<token>"
                  Expect.isTrue (ruleId.StartsWith "boundary:") "boundary-prefixed"
                  Expect.isFalse (ruleId = "") "non-empty"
                  Expect.isFalse (Set.contains ruleId gateRuleIds) "boundary id distinguishable from any gate id"
          }

          test "no route object emits an unattributed: ruleId for the standard fixture (T014, FR-010)" {
              use doc = parse (emit fixtureResult)

              let ids =
                  (selectedGates doc |> List.map (fun g -> strField g "ruleId"))
                  @ (findings doc |> List.map (fun f -> strField f "ruleId"))

              for ruleId in ids do
                  Expect.isFalse (ruleId.StartsWith "unattributed:") (sprintf "%s is not unattributed" ruleId)
          }

          test "message-perturbation: changing a finding's Message leaves its ruleId unchanged (T012, FR-009, C3.3)" {
              // Two findings identical but for `Message` (and path) → identical `ruleId` (derived from the
              // FindingId token alone, never the free-text message).
              let mk (msg: string) (p: string) : UnknownGovernedPathFinding =
                  { Id = UnknownGovernedPath
                    Path = GovernedPath p
                    Zone = GovernedRootUnknown
                    Message = msg }

              let resultOfFinding (f: UnknownGovernedPathFinding) : RouteResult =
                  { SelectedGates = []
                    Findings = { Findings = [ f ] }
                    Cost = { Cheap = 0; Medium = 0; High = 0; Exhaustive = 0 } }

              let ruleIdOf (f: UnknownGovernedPathFinding) =
                  use doc = parse (emit (resultOfFinding f))
                  strField (List.head (findings doc)) "ruleId"

              Expect.equal
                  (ruleIdOf (mk "first wording here" "src/a.fs"))
                  (ruleIdOf (mk "totally different message" "src/b.fs"))
                  "ruleId is message-invariant"
          } ]
