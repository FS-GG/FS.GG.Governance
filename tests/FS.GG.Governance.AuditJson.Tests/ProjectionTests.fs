module FS.GG.Governance.AuditJson.Tests.ProjectionTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// US1 — project a real rolled-up `ShipDecision` so the document records the whole-change verdict/basis
// and lists every blocker/warning/passing item by identity with full six-field enforcement detail.
// Every fixture is a REAL `Ship.rollup` output (Support.decisionOf); the EMITTED BYTES are inspected
// via a read-only JsonDocument parse, never private helpers (Principle V).

/// The identity string each section item carries (a gate by its `id`; a finding by `id`+`path`).
let private identitiesOf (items: System.Text.Json.JsonElement list) : Set<string> =
    items
    |> List.map (fun it ->
        match itemKind it with
        | "gate" -> "gate:" + itemId it
        | "finding" -> "finding:" + itemId it + "@" + itemPath it
        | k -> failwithf "unexpected kind %s" k)
    |> Set.ofList

[<Tests>]
let tests =
    testList
        "Projection (US1)"
        [ test "a decision with >=1 blocker projects verdict:fail / exitCodeBasis:blocked with every blocker by identity + enforcement (AS1, SC-001)" {
              let d = blockersDecision
              Expect.equal d.Verdict Fail "fixture must carry a blocker"
              use doc = parse (AuditJson.ofShipDecision d None)

              Expect.equal (strField doc.RootElement "verdict") "fail" "verdict:fail"
              Expect.equal (strField doc.RootElement "exitCodeBasis") "blocked" "exitCodeBasis:blocked"

              let blockers = section doc "blockers"
              Expect.equal blockers.Length d.Blockers.Length "every blocker rendered, none extra"

              // Every blocker carries its identity + a complete six-field enforcement object.
              for it in blockers do
                  Expect.isNonEmpty (itemId it) "blocker has an id"
                  Expect.equal (enforcementFields it |> List.length) 6 "six enforcement fields present"
          }

          test "a Pass decision with empty blockers projects verdict:pass / exitCodeBasis:clean with a present empty blockers array (AS2, FR-009)" {
              let d = emptyCleanDecision
              use doc = parse (AuditJson.ofShipDecision d None)

              Expect.equal (strField doc.RootElement "verdict") "pass" "verdict:pass"
              Expect.equal (strField doc.RootElement "exitCodeBasis") "clean" "exitCodeBasis:clean"
              Expect.isTrue (hasField doc.RootElement "blockers") "blockers present"
              Expect.isEmpty (section doc "blockers") "blockers empty, never an invented item"
          }

          test "warnings + passing each render in their own section; no item in more than one; union equals the decision's items (AS3, FR-005, SC-001)" {
              let d = richDecision
              use doc = parse (AuditJson.ofShipDecision d None)

              let blockers = section doc "blockers"
              let warnings = section doc "warnings"
              let passing = section doc "passing"

              Expect.equal blockers.Length d.Blockers.Length "blockers count matches the decision"
              Expect.equal warnings.Length d.Warnings.Length "warnings count matches the decision"
              Expect.equal passing.Length d.Passing.Length "passing count matches the decision"

              let bIds = identitiesOf blockers
              let wIds = identitiesOf warnings
              let pIds = identitiesOf passing

              // No item identity appears in more than one section (mutually exclusive partition).
              Expect.isEmpty (Set.intersect bIds wIds) "no item in both blockers and warnings"
              Expect.isEmpty (Set.intersect bIds pIds) "no item in both blockers and passing"
              Expect.isEmpty (Set.intersect wIds pIds) "no item in both warnings and passing"

              // Every item carries identity + full enforcement detail.
              for it in List.concat [ blockers; warnings; passing ] do
                  Expect.isNonEmpty (itemId it) "item has an id"
                  Expect.equal (enforcementFields it |> List.length) 6 "six enforcement fields"
          }

          test "verdict and exitCodeBasis are echoed VERBATIM from the value, never recomputed, and no numeric exitCode appears (FR-002/FR-003, SC-004)" {
              for d in [ emptyCleanDecision; blockersDecision; richDecision ] do
                  use doc = parse (AuditJson.ofShipDecision d None)

                  let expectedVerdict = match d.Verdict with | Pass -> "pass" | Fail -> "fail"
                  let expectedBasis = match d.ExitCodeBasis with | Clean -> "clean" | Blocked -> "blocked"

                  Expect.equal (strField doc.RootElement "verdict") expectedVerdict "verdict verbatim"
                  Expect.equal (strField doc.RootElement "exitCodeBasis") expectedBasis "basis verbatim"
                  Expect.isFalse (hasField doc.RootElement "exitCode") "no numeric exitCode field"
          }

          test "every real rollup reason round-trips through the writer/parser exactly — faithful carry (FR-012)" {
              let d = richDecision
              use doc = parse (AuditJson.ofShipDecision d None)

              let sourceReasons =
                  List.concat [ d.Blockers; d.Warnings; d.Passing ]
                  |> List.map (fun i -> i.Decision.Reason)
                  |> Set.ofList

              let renderedReasons =
                  List.concat [ section doc "blockers"; section doc "warnings"; section doc "passing" ]
                  |> List.map (fun it -> enforcement it "reason")
                  |> Set.ofList

              Expect.equal renderedReasons sourceReasons "every reason round-trips through the writer/parser exactly"
          }

          test "Synthetic: a reason with JSON-special characters round-trips exactly — escaping delegated to the writer (FR-012)" {
              // SYNTHETIC: the real F024 Ship.rollup never emits a reason containing `"`, `\`, or a
              // newline, so the JSON-escaping path cannot be reached from a real-chain fixture. This one
              // case builds a ShipDecision DIRECTLY with a crafted reason to prove the writer escapes
              // faithfully (the value is still a well-typed ShipDecision the projection must handle).
              // Real-evidence path: none — rollup's reason vocabulary is fixed lever-naming text.
              let craftedReason = "needs \"quote\", back\\slash, and\na newline"

              let decision: ShipDecision =
                  { Verdict = Pass
                    Blockers = []
                    Warnings = []
                    Passing =
                      [ { Id = GateItem(GateId "build:tests")
                          Decision =
                            { BaseSeverity = Advisory
                              Maturity = Observe
                              Mode = Gate
                              Profile = Standard
                              EffectiveSeverity = Advisory
                              Reason = craftedReason } } ]
                    ExitCodeBasis = Clean }

              use doc = parse (AuditJson.ofShipDecision decision None)
              let rendered = enforcement (section doc "passing" |> List.head) "reason"
              Expect.equal rendered craftedReason "JSON-special reason round-trips exactly (writer-escaped)"
          } ]
