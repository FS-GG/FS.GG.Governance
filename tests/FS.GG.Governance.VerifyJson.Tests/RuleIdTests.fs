module FS.GG.Governance.VerifyJson.Tests.RuleIdTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.VerifyJson
open FS.GG.Governance.AuditJson
open FS.GG.Governance.VerifyJson.Tests.Support

module SC = FS.GG.Governance.SurfaceChecks.Model

// 068 (T009/T013/T014): the additive per-finding `ruleId` on verify.json enforced items and surfaceChecks
// elements, plus the cross-surface (verify vs ship) id-match. Every decision under test is a REAL
// `Ship.rollup` output (Support `richDecision`); surface findings are real `SurfaceFinding` records; the
// emitted bytes are inspected via JsonDocument. No mocks (Principle V).

let private surfaceFinding (code: string) : SC.SurfaceFinding =
    { Domain = SC.PackageDomain
      Surface = SurfaceId "pkg"
      Code = code
      Location = { File = normalizePath "src/Foo.fsi"; Detail = "drift" }
      BaseSeverity = Blocking
      Maturity = BlockOnPr
      EvidenceTag = None
      IsInputState = false
      Message = "public surface drifted" }

let private allItems (doc: JsonDocument) =
    section doc "blockers" @ section doc "warnings" @ section doc "passing"

let private ruleIdsOf (json: string) : string list =
    use doc = parse json
    allItems doc |> List.map (fun i -> strField i "ruleId") |> List.sort

[<Tests>]
let tests =
    testList
        "RuleId"
        [ test "every enforced item carries ruleId right after the id object (gate:/boundary:) (C2)" {
              use doc = parse (VerifyJson.ofVerifyDecision richDecision (Some mixedReport) mixedOutcomes)
              let items = allItems doc
              Expect.isNonEmpty items "the fixture produces enforced items"

              for item in items do
                  let order = fieldOrder item
                  Expect.equal (List.findIndex ((=) "ruleId") order) (List.findIndex ((=) "id") order + 1) "ruleId after id object"

                  let idObj = item.GetProperty "id"
                  let ruleId = strField item "ruleId"

                  match strField idObj "kind" with
                  | "gate" -> Expect.equal ruleId ("gate:" + strField idObj "gate") "gate item → gate:<id>"
                  | "finding" -> Expect.equal ruleId ("boundary:" + strField idObj "finding") "finding item → boundary:<token>"
                  | k -> failtestf "unexpected item kind %s" k

                  Expect.isFalse (ruleId.StartsWith "unattributed:") "no unattributed: marker (T014)"
          }

          test "every surfaceChecks element carries ruleId (surface:<domain>:<code>) right after code (C2)" {
              let findings = [ surfaceFinding "package.baseline-drift"; surfaceFinding "package.export-removed" ]
              use doc = parse (VerifyJson.ofVerifyDecisionWithSurfaceChecks emptyCleanDecision None [] findings)

              let elements =
                  [ for e in doc.RootElement.GetProperty("surfaceChecks").EnumerateArray() -> e ]

              Expect.equal (List.length elements) 2 "two surface findings"

              for e in elements do
                  let order = fieldOrder e
                  Expect.equal (List.findIndex ((=) "ruleId") order) (List.findIndex ((=) "code") order + 1) "ruleId right after code"

                  let expected = "surface:" + strField e "domain" + ":" + strField e "code"
                  Expect.equal (strField e "ruleId") expected "surface ruleId = surface:<domain>:<code>"
                  Expect.isFalse ((strField e "ruleId").StartsWith "unattributed:") "no unattributed: marker (T014)"
          }

          test "CrossSurfaceRuleId: a finding in both verify.json and audit.json carries an identical ruleId (T013, FR-006, SC-005)" {
              // The SAME real ShipDecision, projected through both surfaces. Each finding's ruleId derives
              // from the same source value through the same constructor, so the id SETS match exactly.
              let verifyIds = ruleIdsOf (VerifyJson.ofVerifyDecision richDecision None [])
              let auditIds = ruleIdsOf (AuditJson.ofShipDecision richDecision None [])

              Expect.isNonEmpty verifyIds "the fixture produces items on the verify surface"
              Expect.equal verifyIds auditIds "the per-finding ruleId set is identical across verify and ship"
          } ]
