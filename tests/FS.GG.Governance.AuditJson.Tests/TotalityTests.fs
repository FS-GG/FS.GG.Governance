module FS.GG.Governance.AuditJson.Tests.TotalityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.AuditJson
open FS.GG.Governance.AuditJson.Tests.Support

// US4 — `ofShipDecision` returns a document for EVERY well-typed ShipDecision and never throws. The
// empty/clean decision is a valid success; no section's items leak into another. The FsCheck property
// drives the genuine `Ship.rollup`, so every generated input is a real upstream-assembled value.

[<Tests>]
let tests =
    testList
        "Totality (US4)"
        [ test "the empty/clean decision projects to a valid three-empty-arrays document, never throwing (AS1, FR-009, SC-006)" {
              use doc = parse (AuditJson.ofShipDecision emptyCleanDecision)

              Expect.equal (strField doc.RootElement "verdict") "pass" "verdict:pass"
              Expect.equal (strField doc.RootElement "exitCodeBasis") "clean" "exitCodeBasis:clean"
              Expect.isEmpty (section doc "blockers") "blockers present and empty"
              Expect.isEmpty (section doc "warnings") "warnings present and empty"
              Expect.isEmpty (section doc "passing") "passing present and empty"
          }

          test "single-section decisions render the populated section and present, empty others — no leakage (AS2, FR-005)" {
              // A blockers-only decision: one BlockOnShip gate at Gate/Standard.
              let blockersOnly =
                  decisionOf (mkRoute [ mkSelectedGate (mkGate (GateId "build:only") BlockOnShip) ] []) Gate Standard
              use d1 = parse (AuditJson.ofShipDecision blockersOnly)
              Expect.isNonEmpty (section d1 "blockers") "blockers populated"
              Expect.isEmpty (section d1 "warnings") "warnings empty"
              Expect.isEmpty (section d1 "passing") "passing empty"

              // A passing-only decision: one Observe gate (base Advisory) — never escalates.
              let passingOnly =
                  decisionOf (mkRoute [ mkSelectedGate (mkGate (GateId "docs:only") Observe) ] []) Gate Standard
              use d2 = parse (AuditJson.ofShipDecision passingOnly)
              Expect.isEmpty (section d2 "blockers") "blockers empty"
              Expect.isEmpty (section d2 "warnings") "warnings empty"
              Expect.isNonEmpty (section d2 "passing") "passing populated"

              // A warnings-only decision: one BlockOnRelease gate at Gate/Standard — relaxed to Advisory.
              let warningsOnly =
                  decisionOf (mkRoute [ mkSelectedGate (mkGate (GateId "build:rel") BlockOnRelease) ] []) Gate Standard
              use d3 = parse (AuditJson.ofShipDecision warningsOnly)
              Expect.isEmpty (section d3 "blockers") "blockers empty"
              Expect.isNonEmpty (section d3 "warnings") "warnings populated"
              Expect.isEmpty (section d3 "passing") "passing empty"
          }

          testPropertyWithConfig fsCheckConfig "ofShipDecision always returns a parseable string and never throws (AS3, SC-006)" (fun d ->
              // Generator provenance: each ShipDecision is produced by the REAL Ship.rollup over a
              // generated RouteResult × RunMode × Profile (Support.genDecision) — no synthetic value.
              let json = AuditJson.ofShipDecision d
              use doc = parse json
              // A well-formed top-level object with the three always-present sections.
              hasField doc.RootElement "blockers"
              && hasField doc.RootElement "warnings"
              && hasField doc.RootElement "passing")
        ]
