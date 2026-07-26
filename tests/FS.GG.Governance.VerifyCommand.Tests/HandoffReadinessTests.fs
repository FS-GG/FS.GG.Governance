module FS.GG.Governance.VerifyCommand.Tests.HandoffReadinessTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CommandHost
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// F081 US3 (SC-005): the handoff's merge-boundary readiness participates as a first-class gate through
// `fsgg verify`. A blocking readiness declaration (non-shippable disposition + blocking diagnostics) ⇒
// a SELECTED blocking readiness gate in `Blockers` contributing to Fail UNDER the Strict profile (where
// `BlockOnShip` is verify-blocking, research D5); a clean readiness ⇒ a present, non-blocking gate.
// The handoff is the sole verdict driver (ExplicitPaths [] selects no routed gate).

let private blockingReadinessJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
         "readiness": { "shipDisposition": "blocked", "verificationReadiness": "incomplete",
                        "blockingDiagnosticIds": [ "VIEW_STALE" ], "counts": { "blocking": 1 }, "perViewState": { "ledger": "stale" } } }"""

let private cleanReadinessJson =
    """{ "contractVersion": "1.0.0", "schemaVersion": 1,
         "evidence": { "nodes": [ { "id": "build:lib", "state": "real" } ], "dependencies": [] },
         "readiness": { "shipDisposition": "shippable", "verificationReadiness": "complete",
                        "blockingDiagnosticIds": [], "counts": { "blocking": 0 }, "perViewState": { "ledger": "fresh" } } }"""

let private handoffRead json : FS.GG.Governance.Adapters.SddHandoff.Reader.HandoffRead =
    { Source = "readiness/wi-1/governance-handoff.json"; Json = json }

let private runVerify profile json =
    let req = requestForProfile (Loop.ExplicitPaths []) Loop.Text profile
    let cap = newCapture ()
    let ports = { fakePorts validCatalog gitSrcChange cap with Handoffs = fun _ -> [ handoffRead json ] }
    Interpreter.run ports req

let private selectedGateIds (m: Loop.Model) = m.SelectedGates |> List.map (fun g -> gateIdValue g.Id)

let private blockerGateIds (d: ShipDecision) =
    d.Blockers
    |> List.choose (fun i ->
        match i.Id with
        | GateItem g -> Some(gateIdValue g)
        | FindingItem _ -> None)

let private withLockedHandoff body =
    let repo = Path.Combine(Path.GetTempPath(), "fsgg-verify-handoff-" + Guid.NewGuid().ToString("N"))
    let dir = Path.Combine(repo, "readiness", "locked")
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "governance-handoff.json")
    File.WriteAllText(path, cleanReadinessJson)

    try
        use _lock = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        body repo
    finally
        Directory.Delete(repo, true)

[<Tests>]
let tests =
    testList
        "HandoffReadiness"
        [ test "blocking readiness ⇒ a selected blocking readiness gate in Blockers ⇒ Fail under Strict (SC-005)" {
              let m = runVerify Strict blockingReadinessJson

              Expect.exists (selectedGateIds m) (fun id -> id.Contains "sdd-handoff:readiness") "the readiness gate is selected"
              Expect.equal (Option.get m.Decision).Verdict Fail "blocking readiness under verify/strict ⇒ Fail"

              Expect.exists
                  (blockerGateIds (Option.get m.Decision))
                  (fun id -> id.Contains "sdd-handoff:readiness")
                  "the readiness gate is a blocker"
          }

          test "clean readiness ⇒ a present, non-blocking readiness gate (Pass)" {
              let m = runVerify Strict cleanReadinessJson

              Expect.exists (selectedGateIds m) (fun id -> id.Contains "sdd-handoff:readiness") "the readiness gate is still selected"
              Expect.equal (Option.get m.Decision).Verdict Pass "clean readiness ⇒ Pass"

              Expect.isFalse
                  (blockerGateIds (Option.get m.Decision) |> List.exists (fun id -> id.Contains "sdd-handoff:readiness"))
                  "a clean readiness gate is NOT a blocker"
          }

          test "unreadable readiness emits an integrity diagnostic and exits non-zero" {
              withLockedHandoff (fun repo ->
                  let req =
                      { requestForProfile (Loop.ExplicitPaths []) Loop.Text Strict with
                          Repo = repo }

                  let cap = newCapture ()

                  let ports =
                      { fakePorts validCatalog gitSrcChange cap with
                          Handoffs = CommandHost.realHandoffs }

                  let m = Interpreter.run ports req

                  let integrityGate =
                      m.SelectedGates
                      |> List.tryFind (fun gate -> gateIdValue gate.Id = "sdd-handoff:integrity:locked")

                  Expect.isSome integrityGate "unreadable state emits the pre-selected integrity gate"

                  Expect.stringContains
                      (Option.get integrityGate).Description
                      "unreadable handoff state"
                      "the gate carries a descriptive diagnostic"

                  Expect.equal (Loop.exitCode m.Exit) 1 "strict verify fails closed with exit 1")
          } ]
