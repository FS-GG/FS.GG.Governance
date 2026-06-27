module FS.GG.Governance.ShipCommand.Tests.GovernedRefRoutingTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F082 (SC-004): a declared governed surface promotes a domain gate into the ship verdict. A handoff
// declaring a path under a block-on-ship domain, with NO sensed change touching that domain, flips the
// verdict to non-shippable through the REAL Config→Gates→Routing→Route→Enforcement→Ship.rollup pipeline.
// US3 (SC-002/005): absent / empty / bad handoff is a byte-identical no-op on routing; a bad document
// still fires its blocking integrity gate (the unchanged F081 consume fold) while widening nothing.
//
// (See the route suite's GovernedRefRoutingTests for the catalog-naming deviation note: src/**→package-api.)

let private handoffRead json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

// A consumable, all-satisfied v1 handoff (its OWN evidence gate is advisory, not a verdict driver)
// declaring the given governedReferences paths.
let private declaringJson (paths: string) : string =
    sprintf
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
             "governedReferences": [ { "workItem": "WI-1", "paths": [ %s ] } ] }"""
        paths

let private malformedJson = """{ "contractVersion": "1.0.0", "schemaVersion": 1, "evidence": { "nodes": [ this is not json """

let private runWith scope handoffs =
    let req = requestFor scope Loop.Text
    let cap = newCapture ()
    let ports = { fakePorts validCatalog gitSrcChange cap req with Handoffs = fun _ -> handoffs }
    Interpreter.run ports req

let private selectedIds (m: Loop.Model) = m.SelectedGates |> List.map (fun g -> gateIdValue g.Id)

let private domainIds (m: Loop.Model) =
    selectedIds m |> List.filter (fun id -> not (id.StartsWith "sdd-handoff:"))

let private blockerGateIds (d: ShipDecision) =
    d.Blockers
    |> List.choose (fun i ->
        match i.Id with
        | GateItem g -> Some(gateIdValue g)
        | FindingItem _ -> None)

[<Tests>]
let tests =
    testList
        "GovernedRefRouting"
        [ // ── US1 V3 (SC-004): a declared surface flips the ship verdict to non-shippable ──
          test "V3 — a declared block-on-ship path with no sensed change flips ship to Fail (SC-004)" {
              // Baseline: the same satisfied handoff but declaring NO governed surface ⇒ no routed gate ⇒ Pass.
              let baseline = runWith (Loop.ExplicitPaths []) [ handoffRead (sprintf """{ "contractVersion": "1.0.0", "schemaVersion": 1, "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] }, "governedReferences": [] }""") ]
              Expect.equal (Option.get baseline.Decision).Verdict Pass "with no declared surface and no sensed change ⇒ Pass"

              // Declaring src/Lib/Thing.fs selects the block-on-ship package-api gates; they fail on the
              // default exec port ⇒ a verdict flip caused SOLELY by the declared surface.
              let m = runWith (Loop.ExplicitPaths []) [ handoffRead (declaringJson "\"src/Lib/Thing.fs\"") ]
              Expect.equal (Option.get m.Decision).Verdict Fail "the declared block-on-ship surface ⇒ Fail"

              Expect.contains
                  (blockerGateIds (Option.get m.Decision))
                  "package-api:build"
                  "the declared-driven block-on-ship gate is a blocker"
          }

          // ── US3 V5(a) (SC-002): no handoff ⇒ byte-identical audit bytes ──
          test "V5a — no handoff ⇒ the audit doc is byte-identical to the pre-F082 projection (SC-002)" {
              let m = runWith Loop.DefaultRange []
              let candidates = candidatesOf gitSrcChange defaultOpts
              let expected = auditExpected validCatalog candidates Gate Standard (Some(snapshotOf gitSrcChange defaultOpts))
              Expect.equal (Option.get m.AuditDoc) expected "the absent-handoff audit doc matches the pre-seam pipeline (the identity merge)"
          }

          // ── US3 V5(b) (SC-002): an empty-governedReferences handoff adds no routing candidate ──
          test "V5b — an empty governedReferences handoff contributes no routing candidate (SC-002)" {
              let baseline = runWith Loop.DefaultRange [] |> domainIds
              let m = runWith Loop.DefaultRange [ handoffRead (declaringJson "") ] // empty paths list

              Expect.equal (domainIds m) baseline "the empty-list handoff selects the same domain gates as no handoff"
              Expect.exists (selectedIds m) (fun id -> id.Contains "sdd-handoff:evidence") "the handoff's own evidence gate still pre-selects (F081 unchanged)"
          }

          // ── US3 V6 (SC-005): a bad document contributes zero candidates yet still fires its integrity gate ──
          test "V6 — a malformed handoff adds zero routing candidates yet keeps its blocking integrity gate (SC-005)" {
              let baseline = runWith Loop.DefaultRange [] |> domainIds
              let m = runWith Loop.DefaultRange [ handoffRead malformedJson ]

              Expect.equal (domainIds m) baseline "the bad document widens NO routing enforcement (zero candidates)"

              Expect.exists
                  (blockerGateIds (Option.get m.Decision))
                  (fun id -> id.Contains "sdd-handoff:integrity")
                  "its blocking integrity gate still appears in Blockers (consume, unchanged)"
          } ]
