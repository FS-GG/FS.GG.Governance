module FS.GG.Governance.VerifyCommand.Tests.GovernedRefRoutingTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// F082 (SC-004): a declared governed surface promotes a domain gate into the verify verdict. A handoff
// declaring a path under a domain whose gate is verify-blocking under Strict, with NO sensed change
// touching that domain, blocks `fsgg verify --strict` through the REAL pipeline. US3 (SC-002/005):
// absent / empty / bad handoff is a byte-identical no-op on routing; a bad document still fires its
// blocking integrity gate while widening nothing.
//
// (See the route suite's GovernedRefRoutingTests for the catalog-naming deviation note: src/**→package-api.)

let private handoffRead json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

let private declaringJson (paths: string) : string =
    sprintf
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
             "governedReferences": [ { "workItem": "WI-1", "paths": [ %s ] } ] }"""
        paths

let private malformedJson = """{ "contractVersion": "1.0.0", "schemaVersion": 1, "evidence": { "nodes": [ this is not json """

let private runWith scope profile handoffs =
    let req = requestForProfile scope Loop.Text profile
    let cap = newCapture ()
    let ports = { fakePorts validCatalog gitSrcChange cap with Handoffs = fun _ -> handoffs }
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
        [ // ── US1 V4 (SC-004): a declared surface blocks verify --strict ──
          test "V4 — a declared verify-blocking path with no sensed change blocks verify --strict (SC-004)" {
              let m = runWith (Loop.ExplicitPaths []) Strict [ handoffRead (declaringJson "\"src/Lib/Thing.fs\"") ]

              Expect.contains (selectedIds m) "package-api:build" "the declared src path selects the verify-blocking package-api:build gate"
              Expect.equal (Option.get m.Decision).Verdict Fail "the declared block-on-ship surface ⇒ Fail under Strict"

              Expect.contains
                  (blockerGateIds (Option.get m.Decision))
                  "package-api:build"
                  "the declared-driven gate blocks the strict verdict"
          }

          // ── US3 V5(a) (SC-002): no handoff ⇒ byte-identical verify bytes ──
          test "V5a — no handoff ⇒ the verify doc is byte-identical to the pre-F082 projection (SC-002)" {
              let m = runWith Loop.DefaultRange Standard []
              let candidates = candidatesOf gitSrcChange defaultOpts
              let expected = verifyExpected validCatalog candidates Standard (Some(snapshotOf gitSrcChange defaultOpts))
              Expect.equal (Option.get m.VerifyDoc) expected "the absent-handoff verify doc matches the pre-seam pipeline (the identity merge)"
          }

          // ── US3 V5(b) (SC-002): an empty-governedReferences handoff adds no routing candidate ──
          test "V5b — an empty governedReferences handoff contributes no routing candidate (SC-002)" {
              let baseline = runWith Loop.DefaultRange Standard [] |> domainIds
              let m = runWith Loop.DefaultRange Standard [ handoffRead (declaringJson "") ]

              Expect.equal (domainIds m) baseline "the empty-list handoff selects the same domain gates as no handoff"
              Expect.exists (selectedIds m) (fun id -> id.Contains "sdd-handoff:evidence") "the handoff's own evidence gate still pre-selects (F081 unchanged)"
          }

          // ── US3 V6 (SC-005): a bad document contributes zero candidates yet still fires its integrity gate ──
          test "V6 — a malformed handoff adds zero routing candidates yet keeps its blocking integrity gate (SC-005)" {
              let baseline = runWith Loop.DefaultRange Strict [] |> domainIds
              let m = runWith Loop.DefaultRange Strict [ handoffRead malformedJson ]

              Expect.equal (domainIds m) baseline "the bad document widens NO routing enforcement (zero candidates)"

              Expect.exists
                  (blockerGateIds (Option.get m.Decision))
                  (fun id -> id.Contains "sdd-handoff:integrity")
                  "its blocking integrity gate still appears in Blockers under Strict (consume, unchanged)"
          } ]
